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
    internal bool HasNotifications { get { return (this.outgoing.Count > 0); } }
    internal NetPeerStatus Status { get { return this.status; } }

    internal long ExpireTick { get; private set; }
    internal bool AckRequested { get; set; }
    internal ushort NotifyAck { get; private set; }

    private readonly Queue<NetEvent> outgoing;
    private readonly IPEndPoint endPoint;
    private readonly bool isClient; // True iff the *remote* peer is a client
    private readonly string token;

    private NetCore core;
    private ushort payloadSeqIn;
    private long creationTick;

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

      this.ExpireTick = creationTick + NetConfig.CONNECTION_TIME_OUT;
      this.AckRequested = false;
      this.NotifyAck = 0;

      this.outgoing = new Queue<NetEvent>();
      this.endPoint = endPoint;
      this.isClient = isClient;
      this.token = token;

      this.payloadSeqIn = 0;
      this.creationTick = creationTick;

      if (isClient) // Client peers are created after a successful connection
        this.status = NetPeerStatus.Connected;
      else // Host peers are created in the process of to connecting to them
        this.status = NetPeerStatus.Connecting;
    }

    /// <summary>
    /// Queues a new notification to be send out reliably during ticks.
    /// </summary>
    internal void QueueNotification(NetEvent data)
    {
      int notificationCount = this.outgoing.Count;
      if (notificationCount >= NetConfig.MAX_PENDING_NOTIFICATIONS)
      {
        NetDebug.LogError("Notification queue full, ignoring latest");
        return;
      }

      data.SetSequence(this.NotifyAck++);
      this.outgoing.Enqueue(data);
    }

    /// <summary>
    /// Make sure this is called before exposing the peer to the main thread.
    /// </summary>
    internal void Connected()
    {
      this.status = NetPeerStatus.Connected;
    }

    internal void Expose(NetCore core)
    {
      this.core = core;
    }

    internal void KeepAlive(long currentTick)
    {
      this.ExpireTick = currentTick + NetConfig.CONNECTION_TIME_OUT;
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
    /// Logs the payload's sequence ID to record payload packet loss.
    /// Returns true if we should accept the payload, false if it's too old.
    /// </summary>
    internal bool LogPayloadSequence(ushort sequence)
    {
      // TODO: Record sequence for PL% calculation

      int diff = NetUtil.UShortSeqDiff(sequence, this.payloadSeqIn);
      if (diff > 0)
      {
        this.payloadSeqIn = sequence;
        return true;
      }
      return false;
    }

    /// <summary>
    /// Receives a notification and updates its ack counter. 
    /// Return true iff the notification is new.
    /// </summary>
    internal bool LogNotificationSequence(ushort sequence)
    {
      this.AckRequested = true;
      int diff = NetUtil.UShortSeqDiff(sequence, this.NotifyAck);
      if (diff > 0)
      {
        this.NotifyAck = sequence;
        return true;
      }
      return false;
    }

    /// <summary>
    /// Cleans up all notifications older than the given ack.
    /// </summary>
    internal void LogNotificationAck(
      ushort ack,
      Action<NetEvent> deallocate)
    {
      while (this.outgoing.Count > 0)
      {
        NetEvent front = this.outgoing.Peek();
        if (NetUtil.UShortSeqDiff(ack, front.Sequence) < 0)
          break;
        deallocate.Invoke(this.outgoing.Dequeue());
      }
    }
    #endregion
  }
}
