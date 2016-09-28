using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System;

namespace MiniUDP
{
  internal class NetIOThread
  {
    private enum SendResult
    {
      Succeeded,
      Failed,
      Skipped,
    }

    public void DeallocateEvent(NetEvent evnt)
    {
      this.eventPool.Deallocate(evnt);
    }

    internal uint UniqueId { get { return this.uid; } }

    #region Main Thread
    // This region should only be accessed by the MAIN thread

    /// <summary>
    /// Queues a notification to be sent to the given peer.
    /// Deep-copies the user data given.
    /// </summary>
    internal void SendNotification(NetPeer target, NetByteBuffer userData)
    {
      NetEvent notification =
        this.CreateEvent(
          NetEventType.Notification,
          target,
          0, // Set by peer
          0,
          userData);
      this.notifyIn.Enqueue(notification);
    }

    internal bool TryReceiveEvent(out NetEvent received)
    {
      return this.eventOut.TryDequeue(out received);
    }

    //internal void BeginConnect(
    //  IPEndPoint hostEndPoint, 
    //  NetByteBuffer userData)
    //{
    //  if (this.isStarted)
    //    throw new ApplicationException("Session thread already running");

    //  NetDebug.Assert(this.isStarted == false);
    //  NetDebug.Assert(this.isClient);

    //  this.connectTarget = hostEndPoint;
    //  this.connectionData.Append(userData);

    //  // TODO: Start/Run
    //}

    //internal void BeginListen()
    //{
    //  NetDebug.Assert(this.isStarted == false);
    //  NetDebug.Assert(this.isClient == false);

    //  this.connectTarget = null;
    //  this.connectionData = null; // Trash it, we don't need it

    //  // TODO: Bind the socket? Or should it already be done first?
    //  // TODO: Start/Run
    //}
    #endregion

    #region Background Thread
    // This region should only be accessed by the BACKGROUND thread

    private readonly NetPipeline<NetEvent> notifyIn;
    private readonly NetPipeline<NetEvent> eventOut;

    private readonly NetPool<NetEvent> eventPool;
    private readonly HashSet<IPEndPoint> pendingConnections;
    private readonly Dictionary<uint, NetPeer> peers;
    private readonly NetSocket socket;
    private readonly uint uid;
    private readonly Stopwatch timer;
    private readonly NetApprover approver;

    private readonly NetPayloadPacket payloadIn;
    private readonly NetProtocolPacket protocolIn;
    private readonly NetSessionPacket sessionIn;
    private readonly NetProtocolPacket protocolOut;
    private readonly NetSessionPacket sessionOut;

    private bool IsFull { get { return false; } }
    private NetByteBuffer connectionData;

    private long nextTick;
    private long nextLongTick;
    private bool isStarted;
    private bool isRunning;
    private bool acceptConnections;

    public NetIOThread(
      Socket socket, 
      NetApprover approver, 
      bool acceptConnections)
    {
      this.notifyIn = new NetPipeline<NetEvent>();
      this.eventOut = new NetPipeline<NetEvent>();

      this.eventPool = new NetPool<NetEvent>();
      this.pendingConnections = new HashSet<IPEndPoint>();
      this.peers = new Dictionary<uint, NetPeer>();
      this.socket = new NetSocket(socket);
      this.uid = NetUtil.CreateUniqueID();
      this.timer = new Stopwatch();
      this.approver = approver;

      this.payloadIn = new NetPayloadPacket();
      this.protocolIn = new NetProtocolPacket();
      this.sessionIn = new NetSessionPacket();
      this.protocolOut = new NetProtocolPacket();
      this.sessionOut = new NetSessionPacket();

      this.connectionData = 
        new NetByteBuffer(NetConst.MAX_PROTOCOL_DATA_SIZE);

      this.nextTick = 0;
      this.nextLongTick = 0;
      this.isStarted = false;
      this.isRunning = false;
      this.acceptConnections = acceptConnections;
    }

    private void Run()
    {
      this.isStarted = true;
      this.isRunning = true;
      this.timer.Start();

      while (this.isRunning)
      {
        this.Update();
        Thread.Sleep(NetConst.THREAD_SLEEP_TIME);
      }

      // TODO: Close all remaining peers?
    }

    #region Update Loop
    private void Update()
    {
      bool longTick;
      if (this.TickAvailable(out longTick))
      {
        // Time to do a tick
        this.ReadPackets();
        this.ReadNotifications();
        foreach (NetPeer peer in this.peers.Values)
          this.UpdatePeer(peer, longTick);
      }
    }

    /// <summary>
    /// Returns true iff it's time for a tick, or a long tick.
    /// </summary>
    private bool TickAvailable(out bool longTick)
    {
      longTick = false;
      long currentTime = this.timer.ElapsedMilliseconds;
      if (currentTime >= this.nextTick)
      {
        this.nextTick = currentTime + NetConst.TICK_RATE;
        if (currentTime >= this.nextLongTick)
        {
          longTick = true;
          this.nextLongTick = currentTime + NetConst.LONG_TICK_RATE;
        }
        return true;
      }
      return false;
    }

    /// <summary>
    /// Receives pending outgoing notifications from the main thread 
    /// and assigns them to their recipient peers
    /// </summary>
    private void ReadNotifications()
    {
      NetEvent notification = null;
      while (this.notifyIn.TryDequeue(out notification))
        if (this.CanSendToPeer(notification.Peer))
          notification.Peer.QueueNotification(notification);
    }

    /// <summary>
    /// Dispatches an event to the main thread.
    /// </summary>
    private void ReportEvent(NetEvent evnt)
    {
      this.eventOut.Enqueue(evnt);
    }

    private void UpdatePeer(NetPeer peer, bool longTick)
    {
      if (peer.IsConnected)
        this.UpdateConnected(peer, longTick);
      else
        ; // TODO: Main thread closed peer, do cleanup
    }

    private void UpdateConnecting(NetPeer peer, bool longTick)
    {
      this.SendConnectPacket(peer.EndPoint);
    }

    private void UpdateConnected(NetPeer peer, bool longTick)
    {
      if (longTick || peer.HasNotifications)
        this.SendSessionPacket(peer);
    }
    #endregion

    #region Packet Read
    /// <summary>
    /// Polls the socket and receives all pending packet data.
    /// </summary>
    private void ReadPackets()
    {
      for (int i = 0; i < NetConst.MAX_PACKET_READS; i++)
      {
        IPEndPoint source = null;
        NetByteBuffer buffer = null;
        if (this.socket.TryReceive(out source, out buffer) == false)
          return;
        this.ReadPacket(source, buffer);
      }
    }

    /// <summary>
    /// Reads a fresh packet and dispatches it based on type.
    /// </summary>
    private void ReadPacket(IPEndPoint source, NetByteBuffer buffer)
    {
      NetPacketType type = (NetPacketType)buffer.PeekByte();
      switch (type)
      {
        case NetPacketType.Payload:
          this.ReadPayload(source, buffer);
          break;

        case NetPacketType.Protocol:
          this.ReadProtocol(source, buffer);
          break;

        case NetPacketType.Session:
          this.ReadSession(source, buffer);
          break;

        default:
          NetDebug.LogError("Unrecognized packet type");
          break;
      }
    }

    /// <summary>
    /// Reads in a payload packet.
    /// </summary>
    private void ReadPayload(
      IPEndPoint source, 
      NetByteBuffer buffer)
    {
      this.payloadIn.Read(buffer);

      NetPeer peer = null;
      if (this.TryGetPeer(this.payloadIn.uid, true, out peer))
      {
        // Notify the peer that it got a payload (must happen first!)
        peer.PayloadReceived(
          this.timer.ElapsedMilliseconds,
          this.payloadIn);

        // Send the payload out to the main thread (must happen second!)
        this.ReportEvent(
          this.CreatePayloadEvent(
            peer, 
            this.payloadIn.payload));
      }

      this.payloadIn.Reset();
    }

    /// <summary>
    /// Reads in a protocol packet.
    /// </summary>
    private void ReadProtocol(
      IPEndPoint source,
      NetByteBuffer buffer)
    {
      this.protocolIn.Read(buffer);

      switch (this.protocolIn.ProtocolType)
      {
        case NetProtocolType.ConnectRequest:
          this.HandleConnectRequest(source, this.protocolIn);
          break;

        // TODO: Other protocol types
      }

      this.protocolIn.Reset();
    }

    private void ReadSession(
      IPEndPoint source,
      NetByteBuffer buffer)
    {
      this.sessionIn.Read(buffer, this.CreateNotification);

      // TODO: Reliability stuff
      // TODO: Session header data stuff
      // TODO: Make sure each notification has its Target set to the peer

      this.sessionIn.Reset();
    }
    #endregion

    #region Protocol Handling
    private void HandleConnectRequest(
      IPEndPoint source, 
      NetProtocolPacket packet)
    {
      if (this.ShouldAccept(source, packet) == false)
        return;

      // Create and add the new peer
      NetPeer newPeer =
        new NetPeer(
          source,
          this.timer.ElapsedMilliseconds,
          packet.UID);
      this.peers.Add(packet.UID, newPeer);

      // Queue the event out to the main thread to receive the connection
      NetEvent connectedEvent = 
        this.CreateEvent(
          NetEventType.PeerConnected,
          newPeer,
          0,
          0,
          packet.data);
      this.eventOut.Enqueue(connectedEvent);
    }

    /// <summary>
    /// Whether or not we should accept a connection before consulting
    /// the application for the final verification step.
    /// </summary>
    private bool ShouldAccept(
      IPEndPoint source,
      NetProtocolPacket packet)
    {
      NetPeer peer = null;
      if (this.TryGetPeer(packet.UID, false, out peer))
      {
        // ID matches but endpoint doesn't
        if (peer.EndPoint.Equals(source) == false)
          this.RejectConnection(source, NetProtocolType.Reject_BadID, null);

        // Peer is already established, resend the accept if they're open
        if (peer.IsConnected)
          this.AcceptConnection(peer);
        return false;
      }

      if (this.acceptConnections == false)
      {
        this.RejectConnection(source, NetProtocolType.Reject_Closed, null);
        return false;
      }

      if (this.IsFull)
      {
        this.RejectConnection(source, NetProtocolType.Reject_Full, null);
        return false;
      }

      NetByteBuffer reason = null;
      if (this.Approve(source, packet, out reason) == false)
      {
        this.RejectConnection(source, NetProtocolType.Reject_BadData, reason);
        return false;
      }

      return true;
    }

    private void AcceptConnection(NetPeer peer)
    {
      // TODO: Send out the accept packet
    }

    private void RejectConnection(
      IPEndPoint source, 
      NetProtocolType connectReject,
      NetByteBuffer rejectReason)
    {
      // TODO: Send out the rejection packet
    }
    #endregion

    #region Packet Send
    /// <summary>
    /// Sends a session packet to the given peer.
    /// </summary>
    private bool SendConnectPacket(IPEndPoint endPoint)
    {
      this.protocolOut.Initialize(
        this.uid,
        NetProtocolType.ConnectRequest,
        this.connectionData);

      bool result = this.socket.TrySend(endPoint, this.protocolOut);
      this.protocolOut.Reset();
      return false;
    }

    /// <summary>
    /// Sends a session packet to the given peer.
    /// </summary>
    private bool SendSessionPacket(NetPeer peer)
    {
      this.sessionOut.Initialize(
        this.uid,
        0,  // TODO
        0,  // TODO
        0,  // TODO
        0,  // TODO
        0); // TODO

      foreach (NetEvent notification in peer.OutgoingNotifications)
        if (this.sessionOut.TryAdd(notification) == false)
          break;

      bool result = this.socket.TrySend(peer.EndPoint, this.sessionOut);
      this.sessionOut.Reset();
      return result;
    }
    #endregion

    #region Event Allocation
    private NetEvent CreateEvent(
      NetEventType type,
      NetPeer target,
      ushort sequence,
      int additionalData,
      NetByteBuffer toAppend)
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.Initialize(
        type,
        target,
        sequence,
        additionalData,
        toAppend);
      return evnt;
    }

    private NetEvent CreateNotification()
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.Initialize(NetEventType.Notification);
      return evnt;
    }

    private NetEvent CreatePayloadEvent(
      NetPeer target,
      NetByteBuffer userData)
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.Initialize(
        NetEventType.Payload,
        target,
        0,
        0,
        userData);
      return evnt;
    }
    #endregion

    #region Misc. Helpers
    private bool CanSendToPeer(NetPeer peer)
    {
      return peer.IsConnected;
    }

    private bool TryGetPeer(
      uint uid, 
      bool requireConnected, 
      out NetPeer peer)
    {
      peer = null;
      if (this.peers.TryGetValue(uid, out peer))
        if (peer.IsConnected || (requireConnected == false))
          return true;
      return false;
    }

    private bool Approve(
      IPEndPoint source, 
      NetProtocolPacket packet, 
      out NetByteBuffer rejectReason)
    {
      rejectReason = null;
      if (this.approver == null)
        return true;
      return this.approver.CheckApproval(source, packet, out rejectReason);
    }
    #endregion

    #endregion
  }
}
