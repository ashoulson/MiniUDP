/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MiniUDP
{
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
    /// Optionally binds our socket before starting.
    /// </summary>
    internal void Bind(int port)
    {
      this.socket.Bind(port);
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

    /// <summary>
    /// Force-closes the socket, even if we haven't stopped running.
    /// </summary>
    internal void Close()
    {
      this.socket.Close();
    }

    /// <summary>
    /// Immediately sends out a disconnect message to a peer.
    /// Can be called on either thread.
    /// </summary>
    internal void SendKick(NetPeer peer, byte reason)
    {
      this.sender.SendKick(peer, NetKickReason.User, reason);
    }

    /// <summary>
    /// Immediately sends out a payload to a peer.
    /// Can be called on either thread.
    /// </summary>
    internal SocketError SendPayload(
      NetPeer peer,
      ushort sequence,
      byte[] data,
      int length)
    {
      return this.sender.SendPayload(peer, sequence, data, length);
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

    private readonly NetSocket socket;
    private readonly NetSender sender;
    private readonly NetReceiver receiver;
    private readonly string version;

    private readonly Queue<NetEvent> reusableQueue;
    private readonly List<NetPeer> reusableList;

    private long nextTick;
    private long nextLongTick;
    private bool isStarted;
    private bool isRunning;
    private bool acceptConnections;

    public NetController(
      string version,
      bool acceptConnections)
    {
      this.connectIn = new NetPipeline<NetPeer>();
      this.notificationIn = new NetPipeline<NetEvent>();
      this.eventOut = new NetPipeline<NetEvent>();

      this.eventPool = new NetPool<NetEvent>();
      this.peers = new Dictionary<IPEndPoint, NetPeer>();
      this.timer = new Stopwatch();
      this.socket = new NetSocket();
      this.sender = new NetSender(this.socket);
      this.receiver = new NetReceiver(this.socket);

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
#if DEBUG
      this.receiver.Update();
#endif

      this.ReadPackets();
      this.ReadNotifications();
      this.ReadConnectRequests();

      bool longTick;
      if (this.TickAvailable(out longTick))
      {
        foreach (NetPeer peer in this.GetPeers())
        {
          peer.Update(this.Time);
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

#if DEBUG
      this.sender.Update();
#endif
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
        if (notification.Peer.IsOpen)
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
        pending.OnReceiveOther(this.Time);
      }
    }

    /// <summary>
    /// Updates a peer that is attempting to connect.
    /// </summary>
    private void UpdateConnecting(NetPeer peer)
    {
      if (peer.GetTimeSinceRecv(this.Time) > NetConfig.CONNECTION_TIME_OUT)
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
      if (peer.GetTimeSinceRecv(this.Time) > NetConfig.CONNECTION_TIME_OUT)
      {
        this.ClosePeer(peer, NetKickReason.Timeout);
        this.eventOut.Enqueue(
          this.CreateEvent(NetEventType.PeerClosedTimeout, peer, 0));
        return;
      }

      long time = this.Time;
      if (peer.HasNotifications || peer.AckRequested)
      {
        this.sender.SendCarrier(peer);
        peer.AckRequested = false;
      }
      if (longTick)
      {
        this.sender.SendPing(peer, this.Time);
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
          this.receiver.TryReceive(out source, out buffer, out length);
        if (NetSocket.Succeeded(result) == false)
          return;

        NetPacketType type = NetEncoding.GetType(buffer);
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
                this.HandleKick(peer, buffer, length);
                break;

              case NetPacketType.Ping:
                this.HandlePing(peer, buffer, length);
                break;

              case NetPacketType.Pong:
                this.HandlePong(peer, buffer, length);
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
      string version;
      string token;
      NetEncoding.ReadConnectRequest(
        buffer,
        out version,
        out token);

      if (this.ShouldCreatePeer(source, version))
      {
        long curTime = this.Time;
        // Create and add the new peer as a client
        NetPeer peer = new NetPeer(source, token, true, curTime);
        this.peers.Add(source, peer);
        peer.OnReceiveOther(curTime);

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
      NetDebug.Assert(peer.IsClient == false, "Ignoring accept from client");
      if (peer.IsConnected || peer.IsClient)
        return;

      peer.OnReceiveOther(this.Time);
      peer.Connected();

      this.eventOut.Enqueue(
        this.CreateEvent(NetEventType.ConnectAccepted, peer, 0));
    }

    private void HandleConnectReject(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      NetDebug.Assert(peer.IsClient == false, "Ignoring reject from client");
      if (peer.IsConnected || peer.IsClient)
        return;

      peer.OnReceiveOther(this.Time);
      this.ClosePeerSilent(peer);

      byte reason;
      byte unused2;
      NetEncoding.ReadProtocolHeader(buffer, out reason, out unused2);

      this.eventOut.Enqueue(
        this.CreateEvent(NetEventType.ConnectRejected, peer, reason));
    }

    private void HandleKick(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      if (peer.IsClosed)
        return;

      bool isConnected = peer.IsConnected;

      peer.OnReceiveOther(this.Time);
      this.ClosePeerSilent(peer);

      if (isConnected)
      {
        byte internalReason;
        byte userReason;
        NetEncoding.ReadProtocolHeader(
          buffer, 
          out internalReason, 
          out userReason);

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

    private void HandlePing(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      if (peer.IsConnected == false)
        return;

      byte pingSeq;
      byte loss;
      int headerSize = 
        NetEncoding.ReadProtocolHeader(buffer, out pingSeq, out loss);

      peer.OnReceivePing(this.Time, loss);
      this.sender.SendPong(peer, pingSeq, peer.GenerateDrop());
    }

    private void HandlePong(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      if (peer.IsConnected == false)
        return;

      byte pongSeq;
      byte drop;
      int headerSize = 
        NetEncoding.ReadProtocolHeader(buffer, out pongSeq, out drop);

      peer.OnReceivePong(this.Time, pongSeq, drop);
    }

    private void HandleCarrier(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      if (peer.IsConnected == false)
        return;

      ushort notificationAck;
      ushort notificationSeq;
      int headerSize = 
        NetEncoding.ReadCarrierHeader(
          buffer,
          out notificationAck,
          out notificationSeq);

      this.reusableQueue.Clear();
      bool success = 
        NetEncoding.ReadNotifications(
          peer, 
          buffer, 
          headerSize, 
          length, 
          this.AllocateNotification, 
          this.reusableQueue);
      if (success == false)
        return;

      long curTime = this.Time;
      peer.OnReceiveCarrier(curTime, notificationAck, this.RecycleEvent);

      // The packet contains the first sequence number. All subsequent
      // notifications have sequence numbers in order, so we just increment.
      foreach (NetEvent notification in this.reusableQueue)
        if (peer.OnReceiveNotification(curTime, notificationSeq++))
          this.eventOut.Enqueue(notification);
    }

    private void HandlePayload(
      NetPeer peer,
      byte[] buffer,
      int length)
    {
      if (peer.IsConnected == false)
        return;

      ushort payloadSeq;
      int position = NetEncoding.ReadPayloadHeader(
        buffer,
        out payloadSeq);

      if (peer.OnReceivePayload(this.Time, payloadSeq))
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
