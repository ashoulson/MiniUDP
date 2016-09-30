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

namespace MiniUDP
{
  internal enum NetPeerStatus
  {
    Connecting,
    Connected,
    Closed,
  }

  public class NetPeer
  {
    public bool IsConnected { get { return this.status == NetPeerStatus.Connected; } }
    public bool IsClient { get { return this.isClient; } }

    private volatile NetPeerStatus status;

    #region Main Thread
    // This region should only be accessed by the MAIN thread

    public object UserData { get; set; }
    public IPEndPoint EndPoint { get { return this.endPoint; } }

    private ushort outgoingPayloadSequence;
    #endregion

    #region Background Thread
    // This region should only be accessed by the BACKGROUND thread

    internal IEnumerable<NetEvent> OutgoingNotifications { get { return this.outgoing; } }
    internal bool HasNotifications { get { return (this.outgoing.Count > 0); } }
    internal NetPeerStatus Status { get { return this.status; } }

    private readonly Queue<NetEvent> outgoing;
    private readonly IPEndPoint endPoint;
    private readonly bool isClient; // True iff the *remote* peer is a client
    private ushort notificationSequence;
    private long creationTick;
    private long expireTick;

    internal NetPeer(IPEndPoint endPoint, bool isClient, long creationTick)
    {
      // Probably no need to pool this class since users may want to hold on
      // to them after closing and they aren't created all that often anyway

      this.outgoingPayloadSequence = 0;

      this.outgoing = new Queue<NetEvent>();
      this.endPoint = endPoint;
      this.isClient = true;

      this.notificationSequence = 0;
      this.creationTick = creationTick;
      this.expireTick = creationTick + NetConst.CONNECTION_TIME_OUT;

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
      if (notificationCount >= NetConst.MAX_PENDING_NOTIFICATIONS)
      {
        NetDebug.LogError("Notification queue full, ignoring latest");
        return;
      }

      data.SetSequence(this.notificationSequence++);
      this.outgoing.Enqueue(data);
    }

    /// <summary>
    /// Cleans up all notifications older than the given ack.
    /// </summary>
    internal void CleanNotifications(
      ushort notificationAck,
      Action<NetEvent> deallocate)
    {
      while (this.outgoing.Count > 0)
      {
        NetEvent front = this.outgoing.Peek();
        if (NetUtil.UShortSeqDiff(notificationAck, front.Sequence) < 0)
          break;
        deallocate.Invoke(this.outgoing.Dequeue());
      }
    }

    /// <summary>
    /// Make sure this is called before exposing the peer to the main thread.
    /// </summary>
    internal void Connected()
    {
      this.status = NetPeerStatus.Connected;
    }

    internal void Disconnected()
    {
      this.status = NetPeerStatus.Closed;
    }

    /// <summary>
    /// Updates the expire timer and logs the payload's sequence ID to record 
    /// payload packet loss. Note that the sequence ID is too low-resolution 
    /// to use for actually sequencing the payloads in any way, we just use
    /// it for statistics.
    /// </summary>
    internal void PayloadReceived(
      long currentTick, 
      byte sequence)
    {
      this.expireTick = currentTick + NetConst.CONNECTION_TIME_OUT;

      // TODO: Record sequence for PL% calculation
    }

    ///// <summary>
    ///// Logs the fact that we received a session packet. Parses through
    ///// all notifications and updates according to what's contained.
    ///// Returns all not-yet-processed notifications.
    ///// </summary>
    //internal IEnumerable<NetEvent> SessionReceived(
    //  long currentTick,
    //  NetSessionPacket packet)
    //{
    //  this.expireTick = currentTick + NetConst.CONNECTION_TIME_OUT;

    //  // TODO: Traffic/Ping statistics updates

    //  foreach (NetEvent notification in packet.notifications)
    //    if (this.ReceiveNotification(notification))
    //      yield return notification;
    //}

    /// <summary>
    /// Receives a notification and updates its ack counter. 
    /// Return true iff the notification is new.
    /// </summary>
    private bool ReceiveNotification(NetEvent notification)
    {
      // TODO: Initialize notification?

      // TODO: Check for old notifications
      // TODO: Clean old notifications
      // TODO: Update Ack
      // TODO: Return true/false if new/old

      return true;
    }
    #endregion
  }
}
