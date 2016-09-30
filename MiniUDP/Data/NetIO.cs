using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniUDP
{
  internal static class NetIO
  {
    internal static NetPacketType GetPacketType(byte[] buffer)
    {
      return (NetPacketType)buffer[0];
    }

    #region Protocol
    // Protocol packets are always 5 bytes. This is a bit wasteful but we're
    // already sending a 16-40B+ header regardless, and these are sent rarely.
    internal const int PROTOCOL_SIZE = 5;

    //                 Byte | Byte      | Byte     | UShort
    //                 --------------------------------------
    // ConnectRequest: Type | Major     | Minor    | Revision
    // ConnectAccept:  Type | 0         | 0        | 0
    // ConnectReject:  Type | Reason    | 0        | 0
    // Disconnect:     Type | Reason    | 0        | 0
    // Ping:           Type | Sequence  | Loss     | Ping
    // Pong:           Type | Sequence  | Loss     | 0

    #region Write
    internal static int WriteConnectRequest(byte[] buffer, byte major, byte minor, ushort revision)
    {
      return NetIO.WriteProtocol(buffer, NetPacketType.ConnectRequest, major, minor, revision);
    }

    internal static int WriteConnectAccept(byte[] buffer)
    {
      return NetIO.WriteProtocol(buffer, NetPacketType.ConnectAccept);
    }

    internal static int WriteConnectReject(byte[] buffer, byte reason)
    {
      return NetIO.WriteProtocol(buffer, NetPacketType.ConnectReject, reason);
    }

    internal static int WriteDisconnect(byte[] buffer, byte reason)
    {
      return NetIO.WriteProtocol(buffer, NetPacketType.Disconnect, reason);
    }

    internal static int WritePing(byte[] buffer, byte sequence, byte loss, ushort ping)
    {
      return NetIO.WriteProtocol(buffer, NetPacketType.Ping, sequence, loss, ping);
    }

    internal static int WritePong(byte[] buffer, byte sequence, byte loss)
    {
      return NetIO.WriteProtocol(buffer, NetPacketType.Pong, sequence, loss);
    }
    #endregion

    #region Read
    internal static void ReadConnectRequest(byte[] buffer, out byte major, out byte minor, out ushort revision)
    {
      NetIO.ReadProtocol(buffer, out major, out minor, out revision);
    }

    internal static void ReadConnectAccept(byte[] buffer)
    {
      // Nothing to read
    }

    internal static void ReadConnectReject(byte[] buffer, out byte reason)
    {
      NetIO.ReadProtocol(buffer, out reason);
    }

    internal static void ReadDisconnect(byte[] buffer, out byte reason)
    {
      NetIO.ReadProtocol(buffer, out reason);
    }

    internal static void ReadPing(byte[] buffer, out byte sequence, out byte loss, out ushort ping)
    {
      NetIO.ReadProtocol(buffer, out sequence, out loss, out ping);
    }

    internal static void ReadPong(byte[] buffer, out byte sequence, out byte loss)
    {
      NetIO.ReadProtocol(buffer, out sequence, out loss);
    }
    #endregion

    #region Serialization
    private static int WriteProtocol(
      byte[] buffer,
      NetPacketType type)
    {
      return NetIO.WriteProtocol(buffer, type, 0, 0, 0);
    }

    private static int WriteProtocol(
      byte[] buffer,
      NetPacketType type,
      byte b1)
    {
      return NetIO.WriteProtocol(buffer, type, b1, 0, 0);
    }

    private static int WriteProtocol(
      byte[] buffer,
      NetPacketType type,
      byte b1,
      byte b2)
    {
      return NetIO.WriteProtocol(buffer, type, b1, b2, 0);
    }

    private static int WriteProtocol(
      byte[] buffer,
      NetPacketType type,
      byte b1,
      byte b2,
      ushort s)
    {
      buffer[0] = (byte)type;
      buffer[1] = b1;
      buffer[2] = b2;
      NetIO.WriteUShort(buffer, 3, s);
      return NetIO.PROTOCOL_SIZE;
    }

    private static void ReadProtocol(
      byte[] buffer,
      out byte b1)
    {
      b1 = buffer[1];
    }

    private static void ReadProtocol(
      byte[] buffer,
      out byte b1,
      out byte b2)
    {
      b1 = buffer[1];
      b2 = buffer[2];
    }

    private static void ReadProtocol(
      byte[] buffer,
      out byte b1,
      out byte b2,
      out ushort s)
    {
      b1 = buffer[1];
      b2 = buffer[2];
      s = ReadUShort(buffer, 3);
    }
    #endregion
    #endregion

    internal static void WriteUShort(
      byte[] buffer,
      int position,
      ushort value)
    {
      buffer[position + 0] = (byte)(value >> (8 * 0));
      buffer[position + 1] = (byte)(value >> (8 * 1));
    }

    internal static ushort ReadUShort(
      byte[] buffer,
      int position)
    {
      int read =
        (buffer[position + 0] << (8 * 0)) |
        (buffer[position + 1] << (8 * 1));
      return (ushort)read;
    }
  }
}
