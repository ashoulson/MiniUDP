using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MiniUDP
{
  internal static class NetIO
  {
    internal static NetPacketType GetType(byte[] buffer)
    {
      return (NetPacketType)buffer[0];
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

      int position =
        NetIO.PackProtocolHeader(
          buffer,
          NetPacketType.Connect,
          (byte)versionBytes,
          (byte)tokenBytes);

      Encoding.UTF8.GetBytes(version, 0, version.Length, buffer, position);
      position += versionBytes;
      Encoding.UTF8.GetBytes(token, 0, token.Length, buffer, position);
      position += tokenBytes;

      return position;
    }

    internal static int ReadConnectRequest(
      byte[] buffer, 
      out string version,
      out string token)
    {
      NetPacketType type;
      byte versionBytes;
      byte tokenBytes;
      int headerBytes = 
        NetIO.ReadProtocolHeader(
          buffer, 
          out versionBytes, 
          out tokenBytes);
      int position = headerBytes;

      try
      {
        version = Encoding.UTF8.GetString(buffer, position, versionBytes);
        position += versionBytes;
        token = Encoding.UTF8.GetString(buffer, position, tokenBytes);
        position += versionBytes;

        return position;
      }
      catch (Exception)
      {
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
      NetIO.PackU16(buffer, 1, sequence);
      return 3;
    }

    internal static int ReadPayloadHeader(
      byte[] buffer,
      out ushort sequence)
    {
      // Already know the type
      sequence = NetIO.ReadU16(buffer, 1);
      return 3;
    }

    internal static int PackCarrierHeader(
      byte[] buffer,
      byte pingSeq, 
      byte pongSeq,
      byte loss,
      ushort processing,
      ushort messageAck,
      ushort messageSeq)
    {
      buffer[0] = (byte)NetPacketType.Carrier;
      buffer[1] = pingSeq;
      buffer[2] = pongSeq;
      buffer[3] = loss;
      NetIO.PackU16(buffer, 4, processing);
      NetIO.PackU16(buffer, 6, messageAck);
      NetIO.PackU16(buffer, 8, messageSeq);
      return 10;
    }

    internal static int ReadCarrierHeader(
      byte[] buffer,
      out byte pingSeq,
      out byte pongSeq,
      out byte loss,
      out ushort processing,
      out ushort messageAck,
      out ushort messageSeq)
    {
      // Already know the type
      pingSeq = buffer[1];
      pongSeq = buffer[2];
      loss = buffer[3];
      processing = NetIO.ReadU16(buffer, 4);
      messageAck = NetIO.ReadU16(buffer, 6);
      messageSeq = NetIO.ReadU16(buffer, 8);
      return 10;
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
