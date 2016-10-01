using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;

namespace MiniUDP
{
  internal class NetController
  {
    public void DeallocateEvent(NetEvent evnt)
    {
      this.eventPool.Deallocate(evnt);
    }

    #region Main Thread
    // This region should only be accessed by the MAIN thread

    /// <summary>
    /// Queues a notification to be sent to the given peer.
    /// Deep-copies the user data given.
    /// </summary>
    internal void SendNotification(NetPeer target, byte[] buffer, int length)
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

    internal bool TryReceiveEvent(out NetEvent received)
    {
      return this.eventOut.TryDequeue(out received);
    }

    internal void BeginConnect(IPEndPoint endpoint)
    {
      this.connectIn.Enqueue(endpoint);
    }

    internal void Start()
    {
      if (this.isStarted)
        throw new InvalidOperationException(
          "Controller has already been started");

      this.isStarted = true;
      this.isRunning = true;

      this.Run();
    }

    internal void Stop()
    {
      this.isRunning = false;
    }
    #endregion

    #region Background Thread
    // This region should only be accessed by the BACKGROUND thread

    private bool IsFull { get { return false; } }
    private long Timestamp { get { return this.timer.ElapsedMilliseconds; } }

    private readonly NetPipeline<IPEndPoint> connectIn;
    private readonly NetPipeline<NetEvent> notificationIn;
    private readonly NetPipeline<NetEvent> eventOut;

    private readonly NetPool<NetEvent> eventPool;
    private readonly Dictionary<IPEndPoint, NetPeer> peers;
    private readonly Stopwatch timer;

    private readonly NetSocket socket;
    private readonly NetVersion version;

    private readonly byte[] reusableBuffer;
    private readonly Queue<NetEvent> reusableQueue;

    private long nextTick;
    private long nextLongTick;
    private bool isStarted;
    private bool isRunning;
    private bool acceptConnections;

    public NetController(
      Socket socket,
      NetVersion version,
      bool acceptConnections)
    {
      this.connectIn = new NetPipeline<IPEndPoint>();
      this.notificationIn = new NetPipeline<NetEvent>();
      this.eventOut = new NetPipeline<NetEvent>();

      this.eventPool = new NetPool<NetEvent>();
      this.peers = new Dictionary<IPEndPoint, NetPeer>();
      this.timer = new Stopwatch();
      this.socket = new NetSocket(socket);
      this.version = version;

      this.reusableBuffer = new byte[NetConst.SOCKET_BUFFER_SIZE];
      this.reusableQueue = new Queue<NetEvent>();

      this.nextTick = 0;
      this.nextLongTick = 0;
      this.isStarted = false;
      this.isRunning = false;
      this.acceptConnections = acceptConnections;
    }

    private void Run()
    {
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
        this.ReadConnectRequests();
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
      while (this.notificationIn.TryDequeue(out notification))
        if (this.CanSendToPeer(notification.Peer))
          notification.Peer.QueueNotification(notification);
    }

    /// <summary>
    /// Read all connection requests and instantiate them as connecting peers.
    /// </summary>
    private void ReadConnectRequests()
    {
      IPEndPoint endpoint;
      while (this.connectIn.TryDequeue(out endpoint))
        if (this.peers.ContainsKey(endpoint) == false)
          this.peers.Add(
            endpoint, 
            new NetPeer(endpoint, false, this.Timestamp));
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

    private void UpdateConnecting(NetPeer peer)
    {
      this.SendConnectionRequest(peer);
    }

    private void UpdateConnected(NetPeer peer, bool longTick)
    {
      // TODO: Tick connected peers (ping, notifications)
    }

    private void UpdateClosed(NetPeer peer)
    {
      // The peer must have been closed by the main thread, because if
      // we closed it on this thread it would have been removed immediately
      this.peers.Remove(peer.EndPoint);
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
        IPEndPoint source;
        byte[] buffer;
        int length;
        if (this.socket.TryReceive(out source, out buffer, out length) == false)
          return;
        this.ReadPacket(source, buffer, length);
      }
    }

    /// <summary>
    /// Reads a fresh packet and dispatches it based on type.
    /// </summary>
    private void ReadPacket(IPEndPoint source, byte[] buffer, int length)
    {
      //NetPacketType type = NetIO.GetPacketType(buffer);

      //// Special case for connect request since no peer exists
      //if (type == NetPacketType.ConnectRequest)
      //{
      //  this.HandleConnectRequest(source, buffer, length);
      //  return;
      //}

      //NetPeer peer;
      //if (this.peers.TryGetValue(source, out peer))
      //{
      //  switch (type)
      //  {
      //    case NetPacketType.ConnectAccept:
      //      this.HandleConnectAccept(peer, buffer, length);
      //      break;

      //    case NetPacketType.ConnectReject:
      //      this.HandleConnectReject(peer, buffer, length);
      //      break;

      //    case NetPacketType.Disconnect:
      //      this.HandleDisconnect(peer, buffer, length);
      //      break;

      //    case NetPacketType.Ping:
      //      this.HandlePing(peer, buffer, length);
      //      break;

      //    case NetPacketType.Pong:
      //      this.HandlePong(peer, buffer, length);
      //      break;

      //      //case NetPacketType.Payload:
      //      //  this.HandlePayload(source, buffer, length);
      //      //  break;

      //      //case NetPacketType.Notification:
      //      //  this.HandleNotification(source, buffer, length);
      //      //  break;
      //  }
      //}
    }
    #endregion

    #region Protocol Handling
    //private void HandleConnectRequest(
    //  IPEndPoint source, 
    //  byte[] buffer, 
    //  int length)
    //{
    //  byte major;
    //  byte minor;
    //  ushort revision;
    //  NetIO.ReadConnectRequest(
    //    buffer,
    //    out major,
    //    out minor,
    //    out revision);
    //  NetVersion version = new NetVersion(major, minor, revision);

    //  if (this.ShouldCreatePeer(source, version) == false)
    //    return;

    //  // Create and add the new peer as a client
    //  NetPeer newPeer = new NetPeer(source, true, this.Timestamp);
    //  this.peers.Add(source, newPeer);

    //  // Queue the event out to the main thread to receive the connection
    //  NetEvent connectedEvent = 
    //    this.CreateEvent(NetEventType.PeerConnected, newPeer, 0);
    //  this.eventOut.Enqueue(connectedEvent);
    //}

    //private void HandleConnectAccept(
    //  NetPeer peer,
    //  byte[] buffer,
    //  int length)
    //{
    //  NetDebug.Assert(peer.IsClient == false, "Got accept from client");
    //  if (peer.IsConnected || peer.IsClient)
    //    return;

    //  peer.Connected();
    //  this.eventOut.Enqueue(
    //    this.CreateEvent(NetEventType.Connected, peer, 0));
    //}

    //private void HandleConnectReject(
    //  NetPeer peer,
    //  byte[] buffer,
    //  int length)
    //{
    //  NetDebug.Assert(peer.IsClient == false, "Got reject from client");
    //  if (peer.IsConnected || peer.IsClient)
    //    return;

    //  this.peers.Remove(peer.EndPoint);
    //  peer.Disconnected();

    //  byte reason;
    //  NetIO.ReadConnectReject(buffer, out reason);
    //  this.eventOut.Enqueue(
    //    this.CreateEvent(NetEventType.Rejected, peer, reason));
    //}

    //private void HandleDisconnect(
    //  NetPeer peer,
    //  byte[] buffer,
    //  int length)
    //{
    //  if (peer.IsConnected == false)
    //    return;

    //  this.peers.Remove(peer.EndPoint);
    //  peer.Disconnected();

    //  byte reason;
    //  NetIO.ReadDisconnect(buffer, out reason);
    //  this.eventOut.Enqueue(
    //    this.CreateEvent(NetEventType.PeerDisconnected, peer, reason));
    //}

    //private void HandlePing(
    //  NetPeer peer,
    //  byte[] buffer,
    //  int length)
    //{
    //  NetDebug.Assert(peer.IsClient == false, "Got ping from client");
    //  if ((peer.IsConnected == false) || peer.IsClient)
    //    return;

    //  byte sequence;
    //  byte loss;
    //  ushort remotePing;
    //  NetIO.ReadPing(buffer, out sequence, out loss, out remotePing);

    //  // TODO: Apply loss and remotePing

    //  this.SendPong(peer, sequence);
    //}

    //private void HandlePong(
    //  NetPeer peer,
    //  byte[] buffer,
    //  int length)
    //{
    //  NetDebug.Assert(peer.IsClient, "Got pong from host");
    //  if ((peer.IsConnected == false) || (peer.IsClient == false))
    //    return;

    //  byte sequence;
    //  byte loss;
    //  NetIO.ReadPong(buffer, out sequence, out loss);

    //  // TODO: Calculate ping
    //  // TODO: Apply loss
    //}
    #endregion

    #region Packet Send
    private void SendConnectionRequest(NetPeer peer)
    {
      //int length =
      //  NetIO.WriteConnectRequest(
      //    this.reusableBuffer,
      //    this.version.major,
      //    this.version.minor,
      //    this.version.revision);
      //this.socket.TrySend(peer.EndPoint, this.reusableBuffer, length);
    }

    private void SendAcceptConnection(NetPeer peer)
    {
      //int length = NetIO.WriteConnectAccept(this.reusableBuffer);
      //this.socket.TrySend(peer.EndPoint, this.reusableBuffer, length);
    }

    private void SendRejectConnection(
      IPEndPoint source,
      NetRejectReason rejectReason)
    {
      //int length =
      //  NetIO.WriteConnectReject(this.reusableBuffer, (byte)rejectReason);
      //this.socket.TrySend(source, this.reusableBuffer, length);
    }

    private void SendPong(NetPeer peer, byte sequence)
    {
      // TODO: Send the pong packet
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

    private NetEvent AllocateEvent()
    {
      return this.eventPool.Allocate();
    }
    #endregion

    #region Misc. Helpers
    private bool CanSendToPeer(NetPeer peer)
    {
      NetDebug.Assert(peer.EndPoint != null, "Peer with null EndPoint");
      if (peer.EndPoint == null)
        return false;
      if (peer.IsConnected == false)
        return false;
      if (this.peers.ContainsKey(peer.EndPoint) == false)
        return false;
      return true;
    }

    /// <summary>
    /// Whether or not we should accept a connection before consulting
    /// the application for the final verification step.
    /// </summary>
    private bool ShouldCreatePeer(
      IPEndPoint source,
      NetVersion version)
    {
      NetPeer peer;
      if (this.peers.TryGetValue(source, out peer))
      {
        if (peer.IsConnected)
          this.SendAcceptConnection(peer);
        return false;
      }

      if (this.acceptConnections == false)
      {
        this.SendRejectConnection(source, NetRejectReason.Closed);
        return false;
      }

      if (this.IsFull)
      {
        this.SendRejectConnection(source, NetRejectReason.Full);
        return false;
      }

      if (this.version.Equals(version) == false)
      {
        this.SendRejectConnection(source, NetRejectReason.BadVersion);
        return false;
      }

      return true;
    }
    #endregion

    #endregion
  }
}
