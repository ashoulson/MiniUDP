using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;

namespace MiniUDP
{
  internal struct NetPending
  {
    internal IPEndPoint EndPoint { get { return this.endPoint; } }
    internal string Token { get { return this.token; } }

    private readonly IPEndPoint endPoint;
    private readonly string token;

    internal NetPending(IPEndPoint endPoint, string token)
    {
      this.endPoint = endPoint;

      this.token = token;
      if (Encoding.UTF8.GetByteCount(token) > NetConfig.MAX_TOKEN_BYTES)
      {
        NetDebug.LogError("Token too long, will be omitted");
        this.token = "";
      }
    }
  }

  internal class NetController
  {
    /// <summary>
    /// Deallocates a pool-spawned event.
    /// </summary>
    internal void RecycleEvent(NetEvent evnt)
    {
      this.eventPool.Deallocate(evnt);
    }

    #region Main Thread
    // This region should only be accessed by the MAIN thread

    /// <summary>
    /// Queues a notification to be sent to the given peer.
    /// Deep-copies the user data given.
    /// </summary>
    internal void QueueNotification(NetPeer target, byte[] buffer, int length)
    {
      NetEvent notification =
        this.CreateEvent(
          NetEventType.Notification,
          target,
          0, // Set by peer
          buffer,
          0,
          length);
      this.notificationIn.Enqueue(notification);
    }

    /// <summary>
    /// Returns the first event on the background thread's outgoing queue.
    /// </summary>
    internal bool TryReceiveEvent(out NetEvent received)
    {
      return this.eventOut.TryDequeue(out received);
    }

    /// <summary>
    /// Queues up a request to connect to an endpoint.
    /// Returns the peer representing this pending connection.
    /// </summary>
    internal NetPeer BeginConnect(IPEndPoint endpoint, string token)
    {
      NetPeer peer = new NetPeer(endpoint, token, false, 0);
      this.connectIn.Enqueue(peer);
      return peer;
    }

    /// <summary>
    /// Signals the controller to begin.
    /// </summary>
    internal void Start()
    {
      if (this.isStarted)
        throw new InvalidOperationException(
          "Controller has already been started");

      this.isStarted = true;
      this.isRunning = true;

      this.Run();
    }

    /// <summary>
    /// Signals the controller to stop updating.
    /// </summary>
    internal void Stop()
    {
      this.isRunning = false;
    }
    #endregion

    #region Background Thread
    // This region should only be accessed by the BACKGROUND thread

    private bool IsFull { get { return false; } }
    private long Time { get { return this.timer.ElapsedMilliseconds; } }

    private readonly NetPipeline<NetPeer> connectIn;
    private readonly NetPipeline<NetEvent> notificationIn;
    private readonly NetPipeline<NetEvent> eventOut;

    private readonly NetPool<NetEvent> eventPool;
    private readonly Dictionary<IPEndPoint, NetPeer> peers;
    private readonly Stopwatch timer;

    private readonly INetSocketReader reader;
    private readonly NetSender sender;
    private readonly string version;

    private readonly Queue<NetEvent> reusableQueue;
    private readonly List<NetPeer> reusableList;

    private long nextTick;
    private long nextLongTick;
    private bool isStarted;
    private bool isRunning;
    private bool acceptConnections;

    public NetController(
      INetSocketReader reader,
      INetSocketWriter writer,
      string version,
      bool acceptConnections)
    {
      this.connectIn = new NetPipeline<NetPeer>();
      this.notificationIn = new NetPipeline<NetEvent>();
      this.eventOut = new NetPipeline<NetEvent>();

      this.eventPool = new NetPool<NetEvent>();
      this.peers = new Dictionary<IPEndPoint, NetPeer>();
      this.timer = new Stopwatch();
      this.sender = new NetSender(writer);
      this.reader = reader;

      this.reusableQueue = new Queue<NetEvent>();
      this.reusableList = new List<NetPeer>();

      this.nextTick = 0;
      this.nextLongTick = 0;
      this.isStarted = false;
      this.isRunning = false;
      this.acceptConnections = acceptConnections;

      this.version = version;
      if (Encoding.UTF8.GetByteCount(version) > NetConfig.MAX_VERSION_BYTES)
        throw new ApplicationException("Version string too long");
    }

    /// <summary>
    /// Controller's main update loop.
    /// </summary>
    private void Run()
    {
      this.timer.Start();
      while (this.isRunning)
      {
        this.Update();
        Thread.Sleep(NetConfig.SleepTime);
      }

      // Cleanup all peers since the loop was broken
      foreach (NetPeer peer in this.GetPeers())
      {
        bool sendEvent = peer.IsOpen;
        this.ClosePeer(peer, NetKickReason.Shutdown);

        if (sendEvent)
          this.eventOut.Enqueue(
            this.CreateEvent(NetEventType.PeerClosedShutdown, peer, 0));
      }
    }

    #region Peer Management
    /// <summary>
    /// Primary update logic. Iterates through and manages all peers.
    /// </summary>
    private void Update()
    {
      this.ReadPackets();
      this.ReadNotifications();
      this.ReadConnectRequests();

      bool longTick;
      if (this.TickAvailable(out longTick))
      {
        foreach (NetPeer peer in this.GetPeers())
        {
          switch (peer.Status)
          {
            case NetPeerStatus.Connecting:
              this.UpdateConnecting(peer);
              break;

            case NetPeerStatus.Connected:
              this.UpdateConnected(peer, longTick);
              break;

            case NetPeerStatus.Closed:
              this.UpdateClosed(peer);
              break;
          }
        }
      }
    }

    /// <summary>
    /// Returns true iff it's time for a tick, or a long tick.
    /// </summary>
    private bool TickAvailable(out bool longTick)
    {
      longTick = false;
      long currentTime = this.Time;
      if (currentTime >= this.nextTick)
      {
        this.nextTick = currentTime + NetConfig.ShortTickRate;
        if (currentTime >= this.nextLongTick)
        {
          longTick = true;
          this.nextLongTick = currentTime + NetConfig.LongTickRate;
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
      while (this.notificationIn.TryDequeue(out notification))
        if (notification.Peer.IsConnected)
          notification.Peer.QueueNotification(notification);
    }

    /// <summary>
    /// Read all connection requests and instantiate them as connecting peers.
    /// </summary>
    private void ReadConnectRequests()
    {
      NetPeer pending;
      while (this.connectIn.TryDequeue(out pending))
      {
        if (this.peers.ContainsKey(pending.EndPoint))
          throw new ApplicationException("Connecting to existing peer");
        if (pending.IsClosed) // User closed peer before we could connect
          continue;

        this.peers.Add(pending.EndPoint, pending);
        pending.KeepAlive(this.Time);
      }
    }

    /// <summary>
    /// Updates a peer that is attempting to connect.
    /// </summary>
    private void UpdateConnecting(NetPeer peer)
    {
      if (peer.ExpireTick < this.Time)
      {
        this.ClosePeer(peer, NetKickReason.Timeout);
        this.eventOut.Enqueue(
          this.CreateEvent(NetEventType.ConnectTimedOut, peer, 0));
        return;
      }

      this.sender.SendConnect(peer, this.version);
    }

    /// <summary>
    /// Updates a peer with an active connection.
    /// </summary>
    private void UpdateConnected(NetPeer peer, bool longTick)
    {
      if (peer.ExpireTick < this.Time)
      {
        this.ClosePeer(peer, NetKickReason.Timeout);
        this.eventOut.Enqueue(
          this.CreateEvent(NetEventType.PeerClosedTimeout, peer, 0));
        return;
      }

      if (longTick || peer.HasNotifications || peer.AckRequested)
      {
        this.sender.SendCarrier(peer);
        peer.AckRequested = false;
      }
    }

    /// <summary>
    /// Updates a peer that has been closed.
    /// </summary>
    private void UpdateClosed(NetPeer peer)
    {
      // The peer must have been closed by the main thread, because if
      // we closed it on this thread it would have been removed immediately
      NetDebug.Assert(peer.ClosedByUser);
      this.peers.Remove(peer.EndPoint);
    }

    /// <summary>
    /// Closes a peer, sending out a best-effort notification and removing
    /// it from the dictionary of active peers.
    /// </summary>
    private void ClosePeer(
      NetPeer peer, 
      NetKickReason reason)
    {
      if (peer.IsOpen)
        this.sender.SendKick(peer, reason);
      this.ClosePeerSilent(peer);
    }

    /// <summary>
    /// Closes a peer without sending a network notification.
    /// </summary>
    private void ClosePeerSilent(NetPeer peer)
    {
      if (peer.IsOpen)
      {
        peer.Disconnected();
        this.peers.Remove(peer.EndPoint);
      }
    }
    #endregion

    #region Packet Read
    /// <summary>
    /// Polls the socket and receives all pending packet data.
    /// </summary>
    private void ReadPackets()
    {
      for (int i = 0; i < NetConfig.MAX_PACKET_READS; i++)
      {
        IPEndPoint source;
        byte[] buffer;
        int length;
        SocketError result = 
          this.reader.TryReceive(out source, out buffer, out length);
        if (NetSocket.Succeeded(result) == false)
          return;

        NetPacketType type = NetIO.GetType(buffer);
        if (type == NetPacketType.Connect)
        {
          // We don't have a peer yet -- special case
          this.HandleConnectRequest(source, buffer, length);
        }
        else
        {
          NetPeer peer;
          if (this.peers.TryGetValue(source, out peer))
          {
            switch (type)
            {
              case NetPacketType.ConnectAccept:
                this.HandleConnectAccept(peer, buffer, length);
                break;

              case NetPacketType.ConnectReject:
                this.HandleConnectReject(peer, buffer, length);
                break;

              case NetPacketType.Kick:
                this.HandleDisconnect(peer, buffer, length);
                break;

              case NetPacketType.Carrier:
                this.HandleCarrier(peer, buffer, length);
                break;

              case NetPacketType.Payload:
                this.HandlePayload(peer, buffer, length);
                break;
            }
          }
        }
      }
    }
    #endregion

    #region Protocol Handling
    /// <summary>
    /// Handles an incoming connection request from a remote peer.
    /// </summary>
    private void HandleConnectRequest(
      IPEndPoint source, 
      byte[] buffer, 
      int length)
    {
      NetDebug.LogMessage("Got connect");

      string version;
      string token;
      NetIO.ReadConnectRequest(
        buffer,
        out version,
        out token);

      if (this.ShouldCreatePeer(source, version))
      {
        // Create and add the new peer as a client
        NetPeer peer = new NetPeer(source, token, true, this.Time);
        this.peers.Add(source, peer);

        // Accept the connection over the network
        this.sender.SendAccept(peer);

        // Queue the event out to the main thread to receive the connection
        this.eventOut.Enqueue(
          this.CreateEvent(NetEventType.PeerConnected, peer, 0));
      }
    }

    private void HandleConnectAccept(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      NetDebug.LogMessage("Got accept");
      NetDebug.Assert(peer.IsClient == false, "Ignoring accept from client");
      if (peer.IsConnected || peer.IsClient)
        return;

      peer.KeepAlive(this.Time);
      peer.Connected();

      this.eventOut.Enqueue(
        this.CreateEvent(NetEventType.ConnectAccepted, peer, 0));
    }

    private void HandleConnectReject(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      NetDebug.LogMessage("Got reject");
      NetDebug.Assert(peer.IsClient == false, "Ignoring reject from client");
      if (peer.IsConnected || peer.IsClient)
        return;

      this.ClosePeerSilent(peer);

      byte reason;
      byte unused2;
      NetIO.ReadProtocolHeader(buffer, out reason, out unused2);

      this.eventOut.Enqueue(
        this.CreateEvent(NetEventType.ConnectRejected, peer, reason));
    }

    private void HandleDisconnect(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      NetDebug.LogMessage("Got disconnect");
      if (peer.IsClosed)
        return;

      bool isConnected = peer.IsConnected;
      this.ClosePeerSilent(peer);

      if (isConnected)
      {
        byte internalReason;
        byte userReason;
        NetIO.ReadProtocolHeader(buffer, out internalReason, out userReason);

        int reason = NetEvent.PackReason(internalReason, userReason);
        this.eventOut.Enqueue(
          this.CreateEvent(NetEventType.PeerClosedKicked, peer, reason));
      }
      else
      {
        // Edge case where we get a disconnect packet before being connected
        int reason = (byte)NetRejectReason.Disconnected;
        this.eventOut.Enqueue(
          this.CreateEvent(NetEventType.ConnectRejected, peer, reason));
        return;
      }
    }

    private void HandleCarrier(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      NetDebug.LogMessage("Got carrier");
      if (peer.IsConnected == false)
        return;

      byte pingSeq;
      byte pongSeq;
      byte loss;
      ushort processing;
      ushort notificationAck;
      ushort notificationSeq;
      int headerSize = 
        NetIO.ReadCarrierHeader(
          buffer,
          out pingSeq,
          out pongSeq,
          out loss,
          out processing,
          out notificationAck,
          out notificationSeq);

      this.reusableQueue.Clear();
      bool success = 
        NetIO.ReadNotifications(
          peer, 
          buffer, 
          headerSize, 
          length, 
          this.AllocateNotification, 
          this.reusableQueue);
      if (success == false)
        return;

      peer.KeepAlive(this.Time);
      peer.LogNotificationAck(notificationAck, this.RecycleEvent);
      foreach (NetEvent notification in this.reusableQueue)
        if (peer.LogNotificationSequence(notificationSeq++))
          this.eventOut.Enqueue(notification);

      // TODO: Traffic statistics
    }

    private void HandlePayload(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      NetDebug.LogMessage("Got payload");
      if (peer.IsConnected == false)
        return;

      ushort payloadSeq;
      int position = NetIO.ReadPayloadHeader(
        buffer,
        out payloadSeq);

      peer.KeepAlive(this.Time);
      if (peer.LogPayloadSequence(payloadSeq))
      {
        this.eventOut.Enqueue(
          this.CreateEvent(
            NetEventType.Payload, 
            peer,
            0,
            buffer,
            position,
            length - position));
      }
    }
    #endregion

    #region Event Allocation
    private NetEvent CreateEvent(
      NetEventType type,
      NetPeer target,
      int otherData)
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.Initialize(
        type,
        target,
        otherData);
      return evnt;
    }

    private NetEvent CreateEvent(
      NetEventType type,
      NetPeer target,
      int otherData,
      byte[] buffer,
      int position,
      int length)
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.Initialize(
        type,
        target,
        otherData,
        buffer,
        position,
        length);
      return evnt;
    }

    private NetEvent AllocateNotification()
    {
      return this.eventPool.Allocate();
    }
    #endregion

    #region Misc. Helpers
    /// <summary>
    /// Whether or not we should accept a connection before consulting
    /// the application for the final verification step.
    /// </summary>
    private bool ShouldCreatePeer(
      IPEndPoint source,
      string version)
    {
      NetPeer peer;
      if (this.peers.TryGetValue(source, out peer))
      {
        this.sender.SendAccept(peer);
        return false;
      }

      if (this.acceptConnections == false)
      {
        this.sender.SendReject(source, NetRejectReason.Closed);
        return false;
      }

      if (this.IsFull)
      {
        this.sender.SendReject(source, NetRejectReason.Full);
        return false;
      }

      if (this.version != version)
      {
        this.sender.SendReject(source, NetRejectReason.BadVersion);
        return false;
      }

      return true;
    }

    private IEnumerable<NetPeer> GetPeers()
    {
      this.reusableList.Clear();
      this.reusableList.AddRange(this.peers.Values);
      return this.reusableList;
    }
    #endregion

    #endregion
  }
}
