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
    Pending,
    Connected,
    Closed,
  }

  public class NetPeer
  {
    public object UserData { get; set; }
    public bool IsConnected
    {
      get { return this.status == NetPeerStatus.Connected; }
    }

    internal int NotificationCount { get { return this.outgoing.Count; } }
    internal IPEndPoint EndPoint { get { return this.endPoint; } }

    private readonly IPEndPoint endPoint;
    private byte payloadSequence;

    internal NetPeer(IPEndPoint endPoint, long creationTick)
    {
      this.endPoint = endPoint;
      this.payloadSequence = 0;

      // Background thread data
      this.outgoing = new Queue<NetNotification>();
      this.status = NetPeerStatus.Pending;
      this.notificationSequence = 0;

      this.creationTick = creationTick;
      this.expireTime = creationTick + NetConfig.CONNECTION_TIME_OUT;
    }

    #region Background Thread
    internal readonly Queue<NetNotification> outgoing;
    private NetPeerStatus status;
    private ushort notificationSequence;

    internal long creationTick;
    internal long expireTime;

    /// <summary>
    /// Called on the background session thread.
    /// </summary>
    internal void QueueNotification(NetNotification data)
    {
      int notificationCount = this.outgoing.Count;
      if (notificationCount >= NetConfig.MAX_PENDING_NOTIFICATIONS)
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
      Action<NetNotification> deallocate)
    {
      while (this.outgoing.Count > 0)
      {
        NetNotification front = this.outgoing.Peek();
        if (NetUtil.UShortSeqDiff(notificationAck, front.sequence) < 0)
          break;
        deallocate.Invoke(this.outgoing.Dequeue());
      }
    }
    #endregion
  }
}
