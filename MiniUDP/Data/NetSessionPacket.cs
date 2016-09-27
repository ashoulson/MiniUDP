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
    // Packet Type                           1 Byte
    internal uint uniqueId;               // 4 Bytes
    internal byte remoteLoss;             // 1 Byte
    internal byte notifyAck;              // 1 Byte
    internal byte pingSequence;           // 1 Byte
    internal byte pongSequence;           // 1 Byte
    internal ushort pongProcessTime;      // 2 Bytes
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
      this.uniqueId = 0;
      this.remoteLoss = 0;
      this.notifyAck = 0;
      this.pingSequence = 0;
      this.pongSequence = 0;
      this.pongProcessTime = 0;
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
      destBuffer.Write(this.uniqueId);
      destBuffer.Write(this.remoteLoss);
      destBuffer.Write(this.notifyAck);
      destBuffer.Write(this.pingSequence);
      destBuffer.Write(this.pongSequence);
      destBuffer.Write(this.pongProcessTime);

      foreach (NetEvent notification in this.notifications)
        notification.Write(destBuffer);
    }

    internal void Read(
      NetByteBuffer sourceBuffer, 
      Func<NetEvent> createNotification)
    {
      sourceBuffer.ReadByte(); // Skip packet type
      this.uniqueId = sourceBuffer.ReadUInt();
      this.remoteLoss = sourceBuffer.ReadByte();
      this.notifyAck = sourceBuffer.ReadByte();
      this.pingSequence = sourceBuffer.ReadByte();
      this.pongSequence = sourceBuffer.ReadByte();
      this.pongProcessTime = sourceBuffer.ReadUShort();

      this.notifications.Clear();
      while (sourceBuffer.Remaining > 0)
      {
        NetEvent notification = createNotification.Invoke();
        notification.Read(sourceBuffer);
        this.notifications.Enqueue(notification);
      }
    }
  }
}
