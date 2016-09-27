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
  public class NetPeer
  {
    #region Main Thread
    // This region should only be accessed by the MAIN thread

    public object UserData { get; set; }
    public bool IsConnected { get { return this.status == NetPeerStatus.Connected; } }
    public IPEndPoint EndPoint { get { return this.endPoint; } }

    private byte payloadSequence;
    #endregion

    #region Background Thread
    // This region should only be accessed by the BACKGROUND thread

    internal IEnumerable<NetEvent> OutgoingNotifications { get { return this.outgoing; } }
    internal NetPeerStatus Status { get { return this.status; } }
    internal bool HasNotifications { get { return (this.outgoing.Count > 0); } }

    private readonly Queue<NetEvent> outgoing;
    private IPEndPoint endPoint;
    private NetPeerStatus status;
    private ushort notificationSequence;
    private long creationTick;
    private long expireTick;

    internal NetPeer(IPEndPoint endPoint, long creationTick)
    {
      // Probably no need to pool this class since users may want to hold on
      // to them after closing and they aren't created all that often anyway

      this.payloadSequence = 0;

      this.outgoing = new Queue<NetEvent>();
      this.endPoint = endPoint;
      this.status = NetPeerStatus.Pending;
      this.notificationSequence = 0;
      this.creationTick = creationTick;
      this.expireTick = creationTick + NetConst.CONNECTION_TIME_OUT;
    }

    /// <summary>
    /// Called on the background session thread.
    /// </summary>
    internal void QueueNotification(NetEvent data)
    {
      // TODO: Sequence number

      int notificationCount = this.outgoing.Count;
      if (notificationCount >= NetConst.MAX_PENDING_NOTIFICATIONS)
      {
        NetDebug.LogError("Notification queue full, ignoring latest");
        return;
      }

      this.outgoing.Enqueue(data);
      data.sequence = this.notificationSequence++;
    }

    /// <summary>
    /// Called on the background session thread.
    /// </summary>
    internal void CleanNotifications(
      ushort notificationAck,
      Action<NetEvent> deallocate)
    {
      while (this.outgoing.Count > 0)
      {
        NetEvent front = this.outgoing.Peek();
        if (NetUtil.UShortSeqDiff(notificationAck, front.sequence) < 0)
          break;
        deallocate.Invoke(this.outgoing.Dequeue());
      }
    }

    /// <summary>
    /// Logs the payload's sequence ID to record payload packet loss.
    /// Note that the sequence ID is too low-resolution to use for actually
    /// sequencing the payloads in any way, we just use it for statistics.
    /// </summary>
    internal void LogPayloadSequence(byte sequenceId)
    {
      // TODO: Record sequence for PL% calculation
    }
    #endregion
  }
}
