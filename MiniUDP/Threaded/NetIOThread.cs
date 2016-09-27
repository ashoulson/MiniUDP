using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

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

    internal uint UniqueId { get { return this.uniqueId; } }

    #region Main Thread
    // This region should only be accessed by the MAIN thread

    /// <summary>
    /// Queues a notification to be sent to the given peer.
    /// Deep-copies the user data given.
    /// </summary>
    internal void SendNotification(NetPeer target, NetByteBuffer userData)
    {
      NetEvent notification = this.CreateEmpty(NetEventType.Notification);
      notification.Target = target;
   // notification.sequence = **SET BY PEER**
      notification.userData.Append(userData);

      this.notifyIn.Enqueue(notification);
    }

    internal bool TryReceiveEvent(out NetEvent received)
    {
      return this.eventOut.TryDequeue(out received);
    }

    internal void BeginConnect(IPEndPoint endpoint, NetByteBuffer userData)
    {
      NetDebug.Assert(this.isStarted == false);

      this.isClient = true;
      this.connectTarget = endpoint;
      this.connectionHail.Append(userData);

      // TODO: Start/Run
    }

    internal void BeginListen()
    {
      NetDebug.Assert(this.isStarted == false);

      this.isClient = false;
      this.connectTarget = null;
      this.connectionHail = null; // Trash it, we don't need it

      // TODO: Bind the socket? Or should it already be done first?
      // TODO: Start/Run
    }
    #endregion

    #region Background Thread
    // This region should only be accessed by the BACKGROUND thread

    private readonly NetPipeline<NetEvent> notifyIn;
    private readonly NetPipeline<NetEvent> eventOut;

    private readonly NetPool<NetEvent> eventPool;
    // TODO: Use a unique ID instead of an IPEndPoint
    // https://github.com/lidgren/lidgren-network-gen3/search?utf8=%E2%9C%93&q=m_uniqueIdentifier
    private readonly Dictionary<IPEndPoint, NetPeer> peers;
    private readonly NetSocket socket;
    private readonly Stopwatch timer;
    private readonly uint uniqueId;

    private readonly NetPayloadPacket payloadReusable;
    private readonly NetProtocolPacket protocolReusable;
    private readonly NetSessionPacket sessionReusable;

    // TODO: How do we handle connecting vs not connecting?
    private bool isClient;
    private IPEndPoint connectTarget;
    private NetByteBuffer connectionHail;

    private long nextTick;
    private long nextLongTick;
    private bool isStarted;
    private bool isRunning;

    public NetIOThread(Socket socket)
    {
      this.notifyIn = new NetPipeline<NetEvent>();
      this.eventOut = new NetPipeline<NetEvent>();

      this.eventPool = new NetPool<NetEvent>();
      this.peers = new Dictionary<IPEndPoint, NetPeer>();
      this.socket = new NetSocket(socket);
      this.timer = new Stopwatch();
      this.uniqueId = NetUtil.CreateUniqueID();

      this.payloadReusable = new NetPayloadPacket();
      this.protocolReusable = new NetProtocolPacket();
      this.sessionReusable = new NetSessionPacket();

      this.isClient = false;
      this.connectTarget = null;
      this.connectionHail = 
        new NetByteBuffer(NetConst.MAX_PROTOCOL_DATA_SIZE);

      this.nextTick = 0;
      this.nextLongTick = 0;
      this.isStarted = false;
      this.isRunning = false;
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

    private void Update()
    {
      bool longTick;
      if (this.TickAvailable(out longTick))
      {
        this.ReadPackets();
        this.ReadNotifications();
        foreach (NetPeer peer in this.peers.Values)
          this.UpdatePeer(peer, longTick);
      }
    }

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
        if ((notification.Target != null) && notification.Target.IsConnected)
          notification.Target.QueueNotification(notification);
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
        case NetPeerStatus.Pending:
          // TODO: Send Connection request
          break;

        case NetPeerStatus.Connected:
          this.SendSessionPacket(peer, longTick);
          break;

        case NetPeerStatus.Closed:
          // TODO: Main thread closed peer?
          break;
      }
    }

    #region Packet Read
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

    private void ReadPacket(IPEndPoint source, NetByteBuffer buffer)
    {
      NetPacketType type = (NetPacketType)buffer.PeekByte();
      switch (type)
      {
        case NetPacketType.Payload:
          this.payloadReusable.Read(buffer);
          this.ReadPayload(source, this.payloadReusable);
          break;

        case NetPacketType.Protocol:
          this.protocolReusable.Read(buffer);
          this.ReadProtocol(source, this.protocolReusable);
          break;

        case NetPacketType.Session:
          this.sessionReusable.Read(buffer, this.CreateEmptyNotification);
          this.ReadSession(source, this.sessionReusable);
          break;

        default:
          NetDebug.LogError("Unrecognized packet type");
          break;
      }
    }

    private void ReadPayload(
      IPEndPoint source, 
      NetPayloadPacket payloadPacket)
    {
      NetPeer peer = null;
      if (this.peers.TryGetValue(source, out peer) && peer.IsConnected)
      {
        peer.LogPayloadSequence(payloadPacket.sequenceId);
        this.ReportEvent(this.PayloadEvent(peer, payloadPacket.payload));
      }

      payloadPacket.Reset();
    }

    private void ReadProtocol(
      IPEndPoint source,
      NetProtocolPacket protocolPacket)
    {
      // TODO: Respond to protocol logic
      protocolPacket.Reset();
    }

    private void ReadSession(
      IPEndPoint source,
      NetSessionPacket sessionPacket)
    {
      // TODO: Reliability stuff
      // TODO: Session header data stuff
      // TODO: Make sure each notification has its Target set to the peer
      sessionPacket.Reset();
    }
    #endregion

    #region Packet Send
    /// <summary>
    /// Sends a session packet to the given peer.
    /// </summary>
    private SendResult SendSessionPacket(NetPeer peer, bool force)
    {
      if (force || peer.HasNotifications)
      {
        this.sessionReusable.uniqueId = this.uniqueId;
        this.sessionReusable.remoteLoss = 0; // TODO
        this.sessionReusable.notifyAck = 0; // TODO
        this.sessionReusable.pingSequence = 0; // TODO
        this.sessionReusable.pongSequence = 0; // TODO
        this.sessionReusable.pongProcessTime = 0; // TODO

        foreach (NetEvent notification in peer.OutgoingNotifications)
          if (this.sessionReusable.TryAdd(notification) == false)
            break;

        bool result = 
          this.socket.TrySend(peer.EndPoint, this.sessionReusable);
        this.sessionReusable.Reset();
        return result ? SendResult.Succeeded : SendResult.Failed;
      }

      return SendResult.Skipped;
    }
    #endregion

    #region Event Allocation
    private NetEvent CreateEmpty(NetEventType type)
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.EventType = type;
      return evnt;
    }

    private NetEvent CreateEmptyNotification()
    {
      return this.CreateEmpty(NetEventType.Notification);
    }

    private NetEvent PayloadEvent(
      NetPeer target,
      NetByteBuffer userData)
    {
      NetEvent evnt = this.eventPool.Allocate();
      evnt.EventType = NetEventType.Payload;
      evnt.Target = target;
      evnt.userData.Append(userData);
      return evnt;
    }
    #endregion

    #endregion
  }
}
