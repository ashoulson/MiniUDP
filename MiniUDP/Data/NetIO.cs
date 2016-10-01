using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MiniUDP
{
  internal static class NetIO
  {
    internal const ulong MASK_BOOL           = (1 << 1)  - 1;
    internal const ulong MASK_TYPE           = (1 << 3)  - 1;
    internal const ulong MASK_LOSS           = (1 << 7)  - 1;
    internal const ulong MASK_PROCESSING     = (1 << 13) - 1;
    internal const ulong MASK_BIG_SEQUENCE   = (1 << 14) - 1;
    internal const ulong MASK_SMALL_SEQUENCE = (1 << 6)  - 1;
    internal const ulong MASK_BIG_PARAM      = (1 << 8)  - 1;
    internal const ulong MASK_SMALL_PARAM    = (1 << 4)  - 1;

    internal static NetPacketType ReadType(byte[] buffer)
    {
      byte bufByte = buffer[0];
      if ((bufByte & 0x80) == 0x80)
        return NetPacketType.Payload;
      return (NetPacketType)(bufByte >> 4);
    }

    internal static int PackPayloadHeader(
      byte[] buffer,
      ushort sequence)
    {
      NetDebug.Assert((sequence & NetIO.MASK_BIG_SEQUENCE) == sequence, "Truncating sequence");

      ulong value =                                   // Size
        (NetIO.MASK_BOOL         & 1)        << 15 |  // 1
        (NetIO.MASK_BIG_SEQUENCE & sequence) << 0;    // 14 (1 wasted)
      NetIO.PackU16(buffer, 0, (ushort)value); // Total: 16
      return sizeof(ushort);
    }

    internal static int ReadPayloadHeader(
      byte[] buffer,
      out ushort sequence)
    {
      ulong data = NetIO.ReadU16(buffer, 0);
      sequence = (ushort)(NetIO.MASK_BIG_SEQUENCE & (data >> 0));
      return sizeof(ushort);
    }

    internal static int PackProtocolHeader(
      byte[] buffer,
      NetPacketType type, 
      byte smallParam, 
      byte bigParam)
    {
      NetDebug.Assert(((ulong)type & NetIO.MASK_TYPE) == (ulong)type, "Truncating type");
      NetDebug.Assert((smallParam & NetIO.MASK_SMALL_PARAM) == smallParam, "Truncating smallParam");
      NetDebug.Assert((bigParam & NetIO.MASK_BIG_PARAM) == bigParam, "Truncating bigParam");

      ulong value =                                    // Size
        (NetIO.MASK_TYPE        & (ulong)type) << 12 | // 4
        (NetIO.MASK_SMALL_PARAM & smallParam)  << 8  | // 4
        (NetIO.MASK_BIG_PARAM   & bigParam)    << 0;   // 8
      NetIO.PackU16(buffer, 0, (ushort)value);  // Total: 16
      return sizeof(ushort);
    }

    internal static int ReadProtocolHeader(
      byte[] buffer,
      out NetPacketType type,
      out byte smallParam,
      out byte bigParam)
    {
      ulong data = NetIO.ReadU16(buffer, 0);
      type =       (NetPacketType)(NetIO.MASK_TYPE        & (data >> 12));
      smallParam = (byte)         (NetIO.MASK_SMALL_PARAM & (data >> 8));
      bigParam =   (byte)         (NetIO.MASK_BIG_PARAM   & (data >> 0));
      return sizeof(ushort);
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
      NetDebug.Assert((pingSeq & NetIO.MASK_SMALL_SEQUENCE) == pingSeq, "Truncating pingSeq");
      NetDebug.Assert((pongSeq & NetIO.MASK_SMALL_SEQUENCE) == pongSeq, "Truncating pongSeq");
      NetDebug.Assert((loss & NetIO.MASK_LOSS) == loss, "Truncating loss");
      NetDebug.Assert((processing & NetIO.MASK_PROCESSING) == processing, "Truncating processing");
      NetDebug.Assert((messageAck & NetIO.MASK_BIG_SEQUENCE) == messageAck, "Truncating messageAck");
      NetDebug.Assert((messageSeq & NetIO.MASK_BIG_SEQUENCE) == messageSeq, "Truncating messageSeq");

      ulong value =                                                        // Size
        (NetIO.MASK_TYPE           & (ulong)NetPacketType.Carrier) << 60 | // 4
        (NetIO.MASK_SMALL_SEQUENCE & pingSeq)                      << 54 | // 6
        (NetIO.MASK_SMALL_SEQUENCE & pongSeq)                      << 48 | // 6
        (NetIO.MASK_LOSS           & loss)                         << 41 | // 7
        (NetIO.MASK_PROCESSING     & processing)                   << 28 | // 13
        (NetIO.MASK_BIG_SEQUENCE   & messageAck)                   << 14 | // 14
        (NetIO.MASK_BIG_SEQUENCE   & messageSeq)                   << 0;   // 14
      NetIO.PackU64(buffer, 0, value);                              // Total: 64
      return sizeof(ulong);
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
      ulong data = NetIO.ReadU64(buffer, 0);
      pingSeq =    (byte)  (NetIO.MASK_SMALL_SEQUENCE & (data >> 54));
      pongSeq =    (byte)  (NetIO.MASK_SMALL_SEQUENCE & (data >> 48));
      loss =       (byte)  (NetIO.MASK_LOSS           & (data >> 41));
      processing = (ushort)(NetIO.MASK_PROCESSING     & (data >> 28));
      messageAck = (ushort)(NetIO.MASK_BIG_SEQUENCE   & (data >> 14));
      messageSeq = (ushort)(NetIO.MASK_BIG_SEQUENCE   & (data >> 0));
      return sizeof(ushort);
    }

    /// <summary>
    /// Encodes a U64 into a buffer at a location in Big Endian order.
    /// </summary>
    private static void PackU64(
      byte[] buffer,
      int position,
      ulong value)
    {
      buffer[position + 0] = (byte)(value >> (8 * 7));
      buffer[position + 1] = (byte)(value >> (8 * 6));
      buffer[position + 2] = (byte)(value >> (8 * 5));
      buffer[position + 3] = (byte)(value >> (8 * 4));
      buffer[position + 4] = (byte)(value >> (8 * 3));
      buffer[position + 5] = (byte)(value >> (8 * 2));
      buffer[position + 6] = (byte)(value >> (8 * 1));
      buffer[position + 7] = (byte)(value >> (8 * 0));
    }

    /// <summary>
    /// Reads a U64 from a buffer at a location in Big Endian order.
    /// </summary>
    private static ulong ReadU64(
      byte[] buffer,
      int position)
    {
      ulong read =
        ((ulong)buffer[position + 0] << (8 * 7)) |
        ((ulong)buffer[position + 1] << (8 * 6)) |
        ((ulong)buffer[position + 2] << (8 * 5)) |
        ((ulong)buffer[position + 3] << (8 * 4)) |
        ((ulong)buffer[position + 4] << (8 * 3)) |
        ((ulong)buffer[position + 5] << (8 * 2)) |
        ((ulong)buffer[position + 6] << (8 * 1)) |
        ((ulong)buffer[position + 7] << (8 * 0));
      return (ushort)read;
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
