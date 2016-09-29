using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System;

namespace MiniUDP
{
  internal class NetController
  {
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

    private bool IsFull { get { return false; } }
    private long Timestamp { get { return this.timer.ElapsedMilliseconds; } }

    private readonly NetPipeline<NetEvent> notifyIn;
    private readonly NetPipeline<NetEvent> eventOut;

    private readonly NetPool<NetEvent> eventPool;
    private readonly HashSet<IPEndPoint> pendingConnections;
    private readonly Dictionary<uint, NetPeer> peers;
    private readonly Stopwatch timer;

    private readonly NetSocket socket;
    private readonly uint uid;
    private readonly ushort version;

    private readonly NetByteBuffer reusableBuffer;
    private readonly Queue<NetEvent> reusableQueue;

    private long nextTick;
    private long nextLongTick;
    private bool isStarted;
    private bool isRunning;
    private bool acceptConnections;

    public NetController(
      Socket socket, 
      ushort version,
      bool acceptConnections)
    {
      this.notifyIn = new NetPipeline<NetEvent>();
      this.eventOut = new NetPipeline<NetEvent>();

      this.eventPool = new NetPool<NetEvent>();
      this.pendingConnections = new HashSet<IPEndPoint>();
      this.peers = new Dictionary<uint, NetPeer>();
      this.timer = new Stopwatch();
      this.socket = new NetSocket(socket);
      this.uid = NetUtil.CreateUniqueID();
      this.version = version;

      this.reusableBuffer = new NetByteBuffer(NetConst.SOCKET_BUFFER_SIZE);
      this.reusableQueue = new Queue<NetEvent>();

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
      long currentTime = this.Timestamp;
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
      // TODO: Tick connecting peers (connect request)
    }

    private void UpdateConnected(NetPeer peer, bool longTick)
    {
      // TODO: Tick connected peers (ping, notifications)
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
      NetPacketType type = NetPacketType.INVALID;
      uint uid = 0;
      ushort data = 0;
      NetIO.Read(
        buffer, 
        out type, 
        out uid, 
        out data, 
        this.reusableBuffer);

      switch (type)
      {
        case NetPacketType.ConnectRequest:
          this.HandleConnectRequest(source, uid, data);
          break;

        case NetPacketType.ConnectAccept:
          break;

        case NetPacketType.ConnectReject:
          break;

        case NetPacketType.Disconnect:
          break;

        case NetPacketType.Ping:
          break;

        case NetPacketType.Pong:
          break;

        case NetPacketType.Payload:
          break;

        case NetPacketType.Notification:
          break;
      }
    }
    #endregion

    #region Protocol Handling
    private void HandleConnectRequest(
      IPEndPoint source, 
      uint sourceUid,
      ushort sourceVersion)
    {
      if (this.ShouldCreatePeer(source, sourceUid, sourceVersion) == false)
        return;

      // Create and add the new peer (just allocate, no pool needed)
      NetPeer newPeer = new NetPeer(source, this.Timestamp, sourceUid);
      this.peers.Add(sourceUid, newPeer);

      // Queue the event out to the main thread to receive the connection
      NetEvent connectedEvent = 
        this.CreateEvent(NetEventType.PeerConnected, newPeer, 0, null);
      this.eventOut.Enqueue(connectedEvent);
    }

    /// <summary>
    /// Whether or not we should accept a connection before consulting
    /// the application for the final verification step.
    /// </summary>
    private bool ShouldCreatePeer(
      IPEndPoint source,
      uint sourceUid,
      ushort sourceVersion)
    {
      NetPeer peer = null;
      if (this.TryGetPeer(sourceUid, false, out peer))
      {
        // ID matches but endpoint doesn't
        if (peer.EndPoint.Equals(source) == false)
          this.RejectConnection(source, NetRejectReason.BadUID);

        // Peer is already established, resend the accept if they're open
        if (peer.IsConnected)
          this.AcceptConnection(peer);
        return false;
      }

      if (this.acceptConnections == false)
      {
        this.RejectConnection(source, NetRejectReason.Closed);
        return false;
      }

      if (this.IsFull)
      {
        this.RejectConnection(source, NetRejectReason.Full);
        return false;
      }

      if (sourceVersion != this.version)
      {
        this.RejectConnection(source, NetRejectReason.BadVersion);
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
      NetRejectReason rejectReason)
    {
      // TODO: Send out the rejection packet
    }
    #endregion

    #region Packet Send
    #endregion

    #region Event Allocation
    private NetEvent CreateEvent(
      NetEventType type,
      NetPeer target,
      int additionalData,
      NetByteBuffer toAppend)
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.Initialize(
        type,
        target,
        additionalData,
        toAppend);
      return evnt;
    }

    private NetEvent AllocateEvent()
    {
      return this.eventPool.Allocate();
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
    #endregion

    #endregion
  }
}
