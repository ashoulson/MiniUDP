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

namespace MiniUDP
{
  /// <summary>
  /// A reusable class for reading/writing session packet data.
  /// </summary>
  internal class NetSessionPacket : INetSendable
  {
    // Packet Type                           1 Byte  */
    internal uint UID                     /* 4 Bytes */ { get; private set; }
    internal byte RemoteLoss              /* 1 Byte  */ { get; private set; }
    internal byte NotifyAck               /* 1 Byte  */ { get; private set; }
    internal byte PingSequence            /* 1 Byte  */ { get; private set; }
    internal byte PongSequence            /* 1 Byte  */ { get; private set; }
    internal ushort PongProcessTime       /* 2 Bytes */ { get; private set; }
    internal const int SESSION_HEADER_SIZE = 11; // Total Bytes

    internal readonly Queue<NetEvent> notifications;

    // Total byte size, for notification capacity
    private int size;

    public NetSessionPacket()
    {
      this.notifications = new Queue<NetEvent>();
      this.Reset();
    }

    internal void Reset()
    {
      this.UID = 0;
      this.RemoteLoss = 0;
      this.NotifyAck = 0;
      this.PingSequence = 0;
      this.PongSequence = 0;
      this.PongProcessTime = 0;
      this.notifications.Clear();
      this.size = 0;
    }

    internal void Initialize(
      uint uid,
      byte remoteLoss,
      byte notifyAck,
      byte pingSequence,
      byte pongSequence,
      ushort pongProcessTime)
    {
      this.UID = uid;
      this.RemoteLoss = remoteLoss;
      this.NotifyAck = notifyAck;
      this.PingSequence = pingSequence;
      this.PongSequence = pongSequence;
      this.PongProcessTime = pongProcessTime;
      this.notifications.Clear();
      this.size = 0;
    }

    internal bool TryAdd(NetEvent notification)
    {
      int notificationSize = notification.ComputeTotalSize();
      if ((this.size + notificationSize) > NetConst.MAX_SESSION_DATA_SIZE)
        return false;

      this.notifications.Enqueue(notification);
      this.size += notificationSize;
      return true;
    }

    public void Write(NetByteBuffer destBuffer)
    {
      destBuffer.Write((byte)NetPacketType.Session);
      destBuffer.Write(this.UID);
      destBuffer.Write(this.RemoteLoss);
      destBuffer.Write(this.NotifyAck);
      destBuffer.Write(this.PingSequence);
      destBuffer.Write(this.PongSequence);
      destBuffer.Write(this.PongProcessTime);

      foreach (NetEvent notification in this.notifications)
        notification.Write(destBuffer);
    }

    internal void Read(
      NetByteBuffer sourceBuffer, 
      Func<NetEvent> createNotification)
    {
      sourceBuffer.ReadByte(); // Skip packet type
      this.UID = sourceBuffer.ReadUInt();
      this.RemoteLoss = sourceBuffer.ReadByte();
      this.NotifyAck = sourceBuffer.ReadByte();
      this.PingSequence = sourceBuffer.ReadByte();
      this.PongSequence = sourceBuffer.ReadByte();
      this.PongProcessTime = sourceBuffer.ReadUShort();

      this.notifications.Clear();
      while (sourceBuffer.ReadRemaining > 0)
      {
        NetEvent notification = createNotification.Invoke();
        notification.Read(sourceBuffer);
        this.notifications.Enqueue(notification);
      }
    }
  }
}
