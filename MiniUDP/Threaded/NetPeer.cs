﻿/*
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
using System.Net;
using System.Net.Sockets;

namespace MiniUDP
{
  internal enum NetPeerStatus
  {
    Connecting,
    Connected,
    Closed,
  }

  public delegate void NetPeerEvent(NetPeer peer);
  public delegate void NetPeerErrorEvent(NetPeer peer, SocketError error);
  public delegate void NetPeerClosedEvent(NetPeer peer, NetKickReason reason, byte userReason);
  public delegate void NetPeerRejectEvent(NetPeer peer, NetRejectReason reason);

  public delegate void NetDataEvent(NetPeer peer, byte[] data, int dataLength);

  public class NetPeer
  {
    // Data Receipt
    public event NetDataEvent PayloadReceived;
    public event NetDataEvent NotificationReceived;

    // Remote peer activity
    public event NetPeerErrorEvent PeerClosedError;
    public event NetPeerEvent PeerClosedTimeout;
    public event NetPeerEvent PeerClosedShutdown;
    public event NetPeerClosedEvent PeerClosedKicked;

    // Responses to connection attempts
    public event NetPeerEvent ConnectTimedOut;
    public event NetPeerConnectEvent ConnectAccepted;
    public event NetPeerRejectEvent ConnectRejected;

    public bool IsConnected { get { return this.status == NetPeerStatus.Connected; } }
    public bool IsOpen { get { return this.status != NetPeerStatus.Closed; } }
    public bool IsClosed { get { return this.status == NetPeerStatus.Closed; } }
    public bool IsClient { get { return this.isClient; } }
    public NetTraffic Traffic { get { return this.traffic; } }
    public string Token { get { return this.token; } }

    private volatile NetPeerStatus status;

    // Can be called from either thread
    internal void Disconnected()
    {
      this.status = NetPeerStatus.Closed;
    }

    #region Main Thread
    // This region should only be accessed by the MAIN thread

    public object UserData { get; set; }
    public IPEndPoint EndPoint { get { return this.endPoint; } }
    internal bool ClosedByUser { get; private set; }

    private ushort payloadSeqOut;

    /// <summary>
    /// Closes the peer's network connection for a given reason byte.
    /// </summary>
    public void Close(
      byte userReason = NetConfig.DEFAULT_USER_REASON)
    {
      NetDebug.Assert(this.core != null);

      if (this.IsOpen)
      {
        this.ClosedByUser = true;
        this.Disconnected();
        if (userReason != NetConfig.DONT_NOTIFY_PEER)
          this.core.NotifyPeerClosed(this, userReason);
      }
    }

    /// <summary>
    /// Immediately sends an unreliable sequenced payload.
    /// </summary>
    public SocketError SendPayload(byte[] data, int length)
    {
      if (length < 0)
        throw new ArgumentOutOfRangeException("length");
      if (length > NetConfig.MAX_DATA_SIZE)
      {
        NetDebug.LogError("Payload too large: " + length);
        return SocketError.MessageSize;
      }

      this.payloadSeqOut++;
      return this.core.SendPayload(this, this.payloadSeqOut, data, length);
    }

    /// <summary>
    /// Queues a reliable ordered notification for delivery.
    /// </summary>
    public bool QueueNotification(byte[] data, int length)
    {
      if (length < 0)
        throw new ArgumentOutOfRangeException("length");
      if (length > NetConfig.MAX_DATA_SIZE)
      {
        NetDebug.LogError("Notification too large: " + length);
        return false;
      }

      this.core.QueueNotification(this, data, length);
      return true;
    }

    #region Events
    internal void OnPayloadReceived(byte[] data, int dataLength)
    {
      this.PayloadReceived?.Invoke(this, data, dataLength);
    }

    internal void OnNotificationReceived(byte[] data, int dataLength)
    {
      this.NotificationReceived?.Invoke(this, data, dataLength);
    }

    internal void OnPeerClosedError(SocketError error)
    {
      this.PeerClosedError?.Invoke(this, error);
    }

    internal void OnPeerClosedTimeout()
    {
      this.PeerClosedTimeout?.Invoke(this);
    }

    internal void OnPeerClosedShutdown()
    {
      this.PeerClosedShutdown?.Invoke(this);
    }

    internal void OnPeerClosedKicked(NetKickReason reason, byte userReason)
    {
      this.PeerClosedKicked?.Invoke(this, reason, userReason);
    }

    internal void OnConnectTimedOut()
    {
      this.ConnectTimedOut?.Invoke(this);
    }

    internal void OnConnectAccepted()
    {
      this.ConnectAccepted?.Invoke(this, this.token);
    }

    internal void OnConnectRejected(NetRejectReason reason)
    {
      this.ConnectRejected?.Invoke(this, reason);
    }
    #endregion

    #endregion

    #region Background Thread
    // This region should only be accessed by the BACKGROUND thread

    internal IEnumerable<NetEvent> Outgoing { get { return this.outgoing; } }
    internal NetPeerStatus Status { get { return this.status; } }
    internal bool HasNotifications { get { return (this.outgoing.Count > 0); } }
    internal ushort NotificationAck { get { return this.traffic.NotificationAck; } }

    internal bool AckRequested { get; set; }

    private readonly NetTraffic traffic;
    private readonly Queue<NetEvent> outgoing;
    private readonly IPEndPoint endPoint;
    private readonly bool isClient; // True iff the *remote* peer is a client
    private readonly string token;

    private NetCore core;
    private ushort notificationSeq;

    internal NetPeer(
      IPEndPoint endPoint, 
      string token,
      bool isClient, 
      long creationTick)
    {
      // Probably no need to pool this class since users may want to hold on
      // to them after closing and they aren't created all that often anyway

      this.ClosedByUser = false;
      this.payloadSeqOut = 0;

      this.AckRequested = false;

      this.traffic = new NetTraffic(creationTick);
      this.outgoing = new Queue<NetEvent>();
      this.endPoint = endPoint;
      this.isClient = isClient;
      this.token = token;

      this.notificationSeq = 1;

      if (isClient) // Client peers are created after a successful connection
        this.status = NetPeerStatus.Connected;
      else // Host peers are created in the process of to connecting to them
        this.status = NetPeerStatus.Connecting;
    }

    internal void Update(long curTime)
    {
      this.traffic.Update(curTime);
    }

    /// <summary>
    /// Queues a new notification to be send out reliably during ticks.
    /// </summary>
    internal bool QueueNotification(NetEvent data)
    {
      int notificationCount = this.outgoing.Count;
      if (notificationCount >= NetConfig.MAX_PENDING_NOTIFICATIONS)
      {
        NetDebug.LogError("Notification queue full");
        return false;
      }

      data.SetSequence(this.notificationSeq++);
      this.outgoing.Enqueue(data);
      return true;
    }

    /// <summary>
    /// Make sure this is called before exposing the peer to the main thread.
    /// </summary>
    internal void Connected()
    {
      this.status = NetPeerStatus.Connected;
    }

    /// <summary>
    /// Assigns this peer to a main-thread core for functionality.
    /// </summary>
    internal void Expose(NetCore core)
    {
      this.core = core;
    }

    /// <summary>
    /// If we have outgoing notifications, returns the sequence of the first.
    /// </summary>
    internal ushort GetFirstSequence()
    {
      if (this.outgoing.Count > 0)
        return this.outgoing.Peek().Sequence;
      return 0;
    }

    /// <summary>
    /// Returns the time (in ms) since we last received useful data.
    /// </summary>
    internal long GetTimeSinceRecv(long curTime)
    {
      return this.traffic.GetTimeSinceRecv(curTime);
    }

    /// <summary>
    /// Advances the outgoing ping sequence.
    /// </summary>
    internal byte GeneratePing(long curTime)
    {
      return this.traffic.GeneratePing(curTime);
    }

    /// <summary>
    /// Produces statistics for carrier packets.
    /// </summary>
    internal byte GenerateLoss()
    {
      return this.traffic.GenerateLoss();
    }

    /// <summary>
    /// Processes statistics received from pong packets.
    /// </summary>
    internal void OnReceivePing(long curTime, byte loss)
    {
      this.traffic.OnReceivePing(curTime, loss);
    }

    /// <summary>
    /// Processes statistics received from pong packets.
    /// </summary>
    internal void OnReceivePong(long curTime, byte pongSeq)
    {
      this.traffic.OnReceivePong(curTime, pongSeq);
    }

    /// <summary>
    /// Cleans out any notifications older than the received carrier ack.
    /// </summary>
    internal void OnReceiveCarrier(
      long curTime,
      ushort notificationAck,
      Action<NetEvent> deallocate)
    {
      this.traffic.OnReceiveOther(curTime);
      while (this.outgoing.Count > 0)
      {
        NetEvent front = this.outgoing.Peek();
        if (NetUtil.UShortSeqDiff(notificationAck, front.Sequence) < 0)
          break;
        deallocate.Invoke(this.outgoing.Dequeue());
      }
    }

    /// <summary>
    /// Logs the payload's sequence ID to record payload packet loss.
    /// Returns true if we should accept the payload, false if it's too old.
    /// </summary>
    internal bool OnReceivePayload(long curTime, ushort sequence)
    {
      return this.traffic.OnReceivePayload(curTime, sequence);
    }

    /// <summary>
    /// Processes a notification and updates our ack counter. 
    /// Return true iff the notification is new.
    /// </summary>
    internal bool OnReceiveNotification(long curTime, ushort notificationSeq)
    {
      this.AckRequested = true;
      return this.traffic.OnReceiveNotification(curTime, notificationSeq);
    }

    /// <summary>
    /// Records the fact that we've received data.
    /// </summary>
    internal void OnReceiveOther(long curTime)
    {
      this.traffic.OnReceiveOther(curTime);
    }

    #endregion
  }
}
