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
    internal const int CONNECT_HEADER_SIZE = 3;
    internal const int PROTOCOL_HEADER_SIZE = 3;
    internal const int PAYLOAD_HEADER_SIZE = 3;
    internal const int CARRIER_HEADER_SIZE = 5;
    internal const int NOTIFICATION_HEADER_SIZE = 2;
    internal const int MAX_NOTIFICATION_PACK =
      NetConfig.DATA_MAXIMUM + NetEncoding.NOTIFICATION_HEADER_SIZE;

    /// <summary>
    /// Peeks the type from the packet buffer.
    /// </summary>
    internal static NetPacketType GetType(byte[] buffer)
    {
      return (NetPacketType)buffer[0];
    }

    /// <summary>
    /// Packs a payload to the given buffer.
    /// </summary>
    internal static int PackPayload(
      byte[] buffer,
      ushort sequence,
      byte[] data,
      ushort dataLength)
    {
      buffer[0] = (byte)NetPacketType.Payload;
      NetEncoding.PackU16(buffer, 1, sequence);
      int position = NetEncoding.PAYLOAD_HEADER_SIZE;

      Array.Copy(data, 0, buffer, position, dataLength);
      return position + dataLength;
    }

    /// <summary>
    /// Reads payload data from the given buffer.
    /// </summary>
    internal static bool ReadPayload(
      Func<NetEventType, NetPeer, NetEvent> eventFactory,
      NetPeer peer,
      byte[] buffer,
      int length,
      out ushort sequence,
      out NetEvent evnt)
    {
      evnt = null;

      // Read header (already know the type)
      sequence = NetEncoding.ReadU16(buffer, 1);
      int position = NetEncoding.PAYLOAD_HEADER_SIZE;

      ushort dataLength = (ushort)(length - position);
      if ((position + dataLength) > length)
        return false; // We're reading past the end of the packet data

      evnt = eventFactory.Invoke(NetEventType.Payload, peer);
      return evnt.ReadData(buffer, position, dataLength); ;
    }

    /// <summary>
    /// Packs a series of notifications into the buffer.
    /// </summary>
    internal static int PackCarrier(
      byte[] buffer,
      ushort notificationAck,
      ushort notificationSeq,
      IEnumerable<NetEvent> notifications)
    {
      int notificationHeaderSize = NetEncoding.NOTIFICATION_HEADER_SIZE;

      // Pack header
      buffer[0] = (byte)NetPacketType.Carrier;
      NetEncoding.PackU16(buffer, 1, notificationAck);
      NetEncoding.PackU16(buffer, 3, notificationSeq);
      int position = NetEncoding.CARRIER_HEADER_SIZE;

      // Pack notifications
      int dataPacked = 0;
      int maxDataPack = NetEncoding.MAX_NOTIFICATION_PACK;
      foreach (NetEvent notification in notifications)
      {
        // See if we can fit the notification
        int packedSize = notificationHeaderSize + notification.EncodedLength;
        if ((dataPacked + packedSize) > maxDataPack)
          break;

        // Pack the notification data
        int packSize = 
          NetEncoding.PackNotification(
            buffer, 
            position, 
            notification.EncodedData,
            notification.EncodedLength);

        // Increment counters
        dataPacked += packSize;
        position += packSize;
      }

      return position;
    }

    /// <summary>
    /// Reads a collection of notifications packed in the buffer.
    /// </summary>
    internal static bool ReadCarrier(
      Func<NetEventType, NetPeer, NetEvent> eventFactory,
      NetPeer peer,
      byte[] buffer,
      int length,
      out ushort notificationAck,
      out ushort notificationSeq,
      Queue<NetEvent> destinationQueue)
    {
      // Read header (already know the type)
      notificationAck = NetEncoding.ReadU16(buffer, 1);
      notificationSeq = NetEncoding.ReadU16(buffer, 3);
      int position = NetEncoding.CARRIER_HEADER_SIZE;

      // Validate
      int maxDataPack = NetEncoding.MAX_NOTIFICATION_PACK;
      if ((position > length) || ((length - position) > maxDataPack))
        return false;

      // Read notifications
      while (position < length)
      {
        NetEvent evnt = eventFactory.Invoke(NetEventType.Notification, peer);
        int bytesRead = 
          NetEncoding.ReadNotification(buffer, length, position, evnt);
        if (bytesRead < 0)
          return false;

        destinationQueue.Enqueue(evnt);
        position += bytesRead;
      }

      return true;
    }

    /// <summary>
    /// Packs a connect request with version and token strings.
    /// </summary>
    internal static int PackConnectRequest(
      byte[] buffer, 
      string version,
      string token)
    {
      int versionBytes = Encoding.UTF8.GetByteCount(version);
      int tokenBytes = Encoding.UTF8.GetByteCount(token);

      NetDebug.Assert((byte)versionBytes == versionBytes);
      NetDebug.Assert((byte)tokenBytes == tokenBytes);

      // Pack header info
      buffer[0] = (byte)NetPacketType.Connect;
      buffer[1] = (byte)versionBytes;
      buffer[2] = (byte)tokenBytes;
      int position = NetEncoding.CONNECT_HEADER_SIZE;

      Encoding.UTF8.GetBytes(version, 0, version.Length, buffer, position);
      position += versionBytes;
      Encoding.UTF8.GetBytes(token, 0, token.Length, buffer, position);
      position += tokenBytes;

      return position;
    }

    /// <summary>
    /// Reads a packed connect request with version and token strings.
    /// </summary>
    internal static bool ReadConnectRequest(
      byte[] buffer, 
      out string version,
      out string token)
    {
      version = "";
      token = "";

      try
      {
        // Already know the type
        byte versionBytes = buffer[1];
        byte tokenBytes = buffer[2];
        int position = NetEncoding.CONNECT_HEADER_SIZE;

        version = Encoding.UTF8.GetString(buffer, position, versionBytes);
        position += versionBytes;
        token = Encoding.UTF8.GetString(buffer, position, tokenBytes);
        return true;
      }
      catch (Exception)
      {
        return false;
      }
    }

    // Params:
    //    Accept: 0, 0
    //    Disconnect: InternalReason, UserReason
    //    Ping: PingSeq, Loss
    //    Pong: PingSeq, Dropped
    internal static int PackProtocol(
      byte[] buffer,
      NetPacketType type, 
      byte firstParam, 
      byte secondParam)
    {
      buffer[0] = (byte)type;
      buffer[1] = firstParam;
      buffer[2] = secondParam;
      return NetEncoding.PROTOCOL_HEADER_SIZE;
    }

    internal static bool ReadProtocol(
      byte[] buffer,
      int length,
      out byte firstParam,
      out byte secondParam)
    {
      // Already know the type
      firstParam = buffer[1];
      secondParam = buffer[2];

      if (length < NetEncoding.PROTOCOL_HEADER_SIZE)
        return false;
      return true;
    }

    /// <summary>
    /// Packs a notification prepended with that notification's length.
    /// </summary>
    private static int PackNotification(
      byte[] buffer,
      int position,
      byte[] data,
      ushort dataLength)
    {
      // For notifications we add the length since there may be multiple
      NetEncoding.PackU16(buffer, position, dataLength);
      position += NetEncoding.NOTIFICATION_HEADER_SIZE;

      Array.Copy(data, 0, buffer, position, dataLength);
      return NetEncoding.NOTIFICATION_HEADER_SIZE + dataLength;
    }

    /// <summary>
    /// Reads a length-prefixed notification block.
    /// </summary>
    private static int ReadNotification(
      byte[] buffer,
      int length,
      int position,
      NetEvent destination)
    {
      // Read the length we added
      ushort dataLength = NetEncoding.ReadU16(buffer, position);
      position += NetEncoding.NOTIFICATION_HEADER_SIZE;

      // Avoid a crash if the packet is bad (or malicious)
      if ((position + dataLength) > length)
        return -1;

      // Read the data into the event's buffer
      if (destination.ReadData(buffer, position, dataLength) == false)
        return -1;
      return NetEncoding.NOTIFICATION_HEADER_SIZE + dataLength;
    }

    /// <summary>
    /// Encodes a U16 into a buffer at a location in Big Endian order.
    /// </summary>
    private static void PackU16(
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
    private static ushort ReadU16(
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
