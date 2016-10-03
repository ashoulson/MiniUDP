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
using System.Text;

namespace MiniUDP
{
  internal static class NetEncoding
  {
    internal static NetPacketType GetType(byte[] buffer)
    {
      return (NetPacketType)buffer[0];
    }

    /// <summary>
    /// Packs a series of notifications into the buffer.
    /// </summary>
    internal static int PackNotifications(
      byte[] buffer, 
      int position, 
      IEnumerable<NetEvent> notifications)
    {
      int bytesPacked = 0;
      int maxPack = NetConfig.MAX_NOTIFICATION_PACK;
      foreach (NetEvent notification in notifications)
      {
        if ((bytesPacked + notification.PackSize) > maxPack)
          break;
        int packSize = notification.Pack(buffer, position);
        bytesPacked += packSize;
        position += packSize;
      }

      return bytesPacked;
    }

    /// <summary>
    /// Reads a collection of notifications packed in the buffer.
    /// </summary>
    internal static bool ReadNotifications(
      NetPeer peer,
      byte[] buffer,
      int position,
      int length,
      Func<NetEvent> eventFactory,
      Queue<NetEvent> destinationQueue)
    {
      if ((length - position) > NetConfig.MAX_NOTIFICATION_PACK)
      {
        NetDebug.LogError("Bad packet length");
        return false;
      }

      while (position < length)
      {
        NetEvent notification = eventFactory.Invoke();
        notification.Initialize(NetEventType.Notification, peer, 0);
        int bytesRead = notification.Read(buffer, position, length);

        if (bytesRead < 0)
        {
          NetDebug.LogError("Error reading notification");
          return false;
        }

        destinationQueue.Enqueue(notification);
        position += bytesRead;
      }

      return true;
    }

    internal static int PackConnectRequest(
      byte[] buffer, 
      string version,
      string token)
    {
      int versionBytes = Encoding.UTF8.GetByteCount(version);
      int tokenBytes = Encoding.UTF8.GetByteCount(token);

      NetDebug.Assert((byte)versionBytes == versionBytes);
      NetDebug.Assert((byte)tokenBytes == tokenBytes);

      int bytesPacked =
        NetEncoding.PackProtocolHeader(
          buffer,
          NetPacketType.Connect,
          (byte)versionBytes,
          (byte)tokenBytes);

      Encoding.UTF8.GetBytes(version, 0, version.Length, buffer, bytesPacked);
      bytesPacked += versionBytes;
      Encoding.UTF8.GetBytes(token, 0, token.Length, buffer, bytesPacked);
      bytesPacked += tokenBytes;

      return bytesPacked;
    }

    internal static int ReadConnectRequest(
      byte[] buffer, 
      out string version,
      out string token)
    {
      byte versionBytes;
      byte tokenBytes;
      int headerBytes = 
        NetEncoding.ReadProtocolHeader(
          buffer, 
          out versionBytes, 
          out tokenBytes);
      int bytesRead = headerBytes;

      try
      {
        version = Encoding.UTF8.GetString(buffer, bytesRead, versionBytes);
        bytesRead += versionBytes;
        token = Encoding.UTF8.GetString(buffer, bytesRead, tokenBytes);
        bytesRead += versionBytes;

        return bytesRead;
      }
      catch (Exception)
      {
        NetDebug.LogError("Error decoding connect request");
        version = "";
        token = "";
        return headerBytes;
      }
    }

    // Params:
    //    Connect: VersionLen, TokenLen
    //    Accept: 0, 0
    //    Reject: InternalReason, 0
    //    Disconnect: InternalReason, UserReason
    //    Ping: PingSeq, Loss
    //    Pong: PingSeq, Dropped
    internal static int PackProtocolHeader(
      byte[] buffer,
      NetPacketType type, 
      byte firstParam, 
      byte secondParam)
    {
      buffer[0] = (byte)type;
      buffer[1] = firstParam;
      buffer[2] = secondParam;
      return 3;
    }

    internal static int ReadProtocolHeader(
      byte[] buffer,
      out byte firstParam,
      out byte secondParam)
    {
      // Already know the type
      firstParam = buffer[1];
      secondParam = buffer[2];
      return 3;
    }

    internal static int PackPayloadHeader(
      byte[] buffer,
      ushort sequence)
    {
      buffer[0] = (byte)NetPacketType.Payload;
      NetEncoding.PackU16(buffer, 1, sequence);
      return 3;
    }

    internal static int ReadPayloadHeader(
      byte[] buffer,
      out ushort sequence)
    {
      // Already know the type
      sequence = NetEncoding.ReadU16(buffer, 1);
      return 3;
    }

    internal static int PackCarrierHeader(
      byte[] buffer,
      ushort notificationAck,
      ushort notificationSeq)
    {
      buffer[0] = (byte)NetPacketType.Carrier;
      NetEncoding.PackU16(buffer, 1, notificationAck);
      NetEncoding.PackU16(buffer, 3, notificationSeq);
      return 5;
    }

    internal static int ReadCarrierHeader(
      byte[] buffer,
      out ushort notificationAck,
      out ushort notificationSeq) // The sequence # of the first notification
    {
      // Already know the type
      notificationAck = NetEncoding.ReadU16(buffer, 1);
      notificationSeq = NetEncoding.ReadU16(buffer, 3);
      return 5;
    }

    /// <summary>
    /// Encodes a U16 into a buffer at a location in Big Endian order.
    /// </summary>
    internal static void PackU16(
      byte[] buffer,
      int position,
      ushort value)
    {
      buffer[position + 0] = (byte)(value >> (8 * 1));
      buffer[position + 1] = (byte)(value >> (8 * 0));
    }

    /// <summary>
    /// Reads a U16 from a buffer at a location in Big Endian order.
    /// </summary>
    internal static ushort ReadU16(
      byte[] buffer,
      int position)
    {
      int read =
        (buffer[position + 0] << (8 * 1)) |
        (buffer[position + 1] << (8 * 0));
      return (ushort)read;
    }
  }
}
