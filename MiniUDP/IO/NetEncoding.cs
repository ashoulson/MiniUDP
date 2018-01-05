/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
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
    internal const int MESSAGE_HEADER_SIZE = 2;
    internal const int MAX_MESSAGE_PACK =
      NetConfig.DATA_MAXIMUM + NetEncoding.MESSAGE_HEADER_SIZE;

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
      NetPeer peer,
      byte[] buffer,
      int packetLength,
      byte[] dataBuffer,
      out ushort dataLength,
      out ushort sequence)
    {
      // Read header (already know the type)
      sequence = NetEncoding.ReadU16(buffer, 1);
      int position = NetEncoding.PAYLOAD_HEADER_SIZE;

      dataLength = (ushort)(packetLength - position);
      if ((position + dataLength) > packetLength)
        return false; // We're reading past the end of the packet data

      Array.Copy(buffer, position, dataBuffer, 0, dataLength);
      return true;
    }

    /// <summary>
    /// Packs a series of messages into the buffer.
    /// </summary>
    internal static int PackCarrier(
      byte[] buffer,
      ushort messageAck,
      ushort messageSeq,
      IEnumerable<NetMessage> messages)
    {
      int messageHeaderSize = NetEncoding.MESSAGE_HEADER_SIZE;

      // Pack header
      buffer[0] = (byte)NetPacketType.Carrier;
      NetEncoding.PackU16(buffer, 1, messageAck);
      NetEncoding.PackU16(buffer, 3, messageSeq);
      int position = NetEncoding.CARRIER_HEADER_SIZE;

      // Pack messages
      int dataPacked = 0;
      int maxDataPack = NetEncoding.MAX_MESSAGE_PACK;
      foreach (NetMessage message in messages)
      {
        // See if we can fit the message
        int packedSize = messageHeaderSize + message.EncodedLength;
        if ((dataPacked + packedSize) > maxDataPack)
          break;

        // Pack the message data
        int packSize = 
          NetEncoding.PackMessage(
            buffer, 
            position, 
            message.EncodedData,
            message.EncodedLength);

        // Increment counters
        dataPacked += packSize;
        position += packSize;
      }

      return position;
    }

    /// <summary>
    /// Reads a collection of messages packed in the buffer.
    /// </summary>
    internal static bool ReadCarrier(
      Func<NetPeer, NetMessage> messageFactory,
      NetPeer peer,
      byte[] buffer,
      int packetLength,
      out ushort messageAck,
      out ushort messageSeq,
      Queue<NetMessage> destinationQueue)
    {
      // Read header (already know the type)
      messageAck = NetEncoding.ReadU16(buffer, 1);
      messageSeq = NetEncoding.ReadU16(buffer, 3);
      int position = NetEncoding.CARRIER_HEADER_SIZE;

      // Validate
      int maxPack = NetEncoding.MAX_MESSAGE_PACK;
      if ((position > packetLength) || ((packetLength - position) > maxPack))
        return false;

      // Read messages
      while (position < packetLength)
      {
        NetMessage message = messageFactory.Invoke(peer);
        int bytesRead = 
          NetEncoding.ReadMessage(buffer, packetLength, position, message);
        if (bytesRead < 0)
          return false;

        destinationQueue.Enqueue(message);
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

      NetDebug.Assert((byte)versionBytes == versionBytes, "versionBytes");
      NetDebug.Assert((byte)tokenBytes == tokenBytes, "tokenBytes");

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
    /// Packs a message prepended with that message's length.
    /// </summary>
    private static int PackMessage(
      byte[] buffer,
      int position,
      byte[] data,
      ushort dataLength)
    {
      // For messages we add the length since there may be multiple
      NetEncoding.PackU16(buffer, position, dataLength);
      position += NetEncoding.MESSAGE_HEADER_SIZE;

      Array.Copy(data, 0, buffer, position, dataLength);
      return NetEncoding.MESSAGE_HEADER_SIZE + dataLength;
    }

    /// <summary>
    /// Reads a length-prefixed message block.
    /// </summary>
    private static int ReadMessage(
      byte[] buffer,
      int packetLength,
      int position,
      NetMessage destination)
    {
      // Read the length we added
      ushort dataLength = NetEncoding.ReadU16(buffer, position);
      position += NetEncoding.MESSAGE_HEADER_SIZE;

      // Avoid a crash if the packet is bad (or malicious)
      if ((position + dataLength) > packetLength)
        return -1;

      // Read the data into the message's buffer
      if (destination.ReadData(buffer, position, dataLength) == false)
        return -1;
      return NetEncoding.MESSAGE_HEADER_SIZE + dataLength;
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
