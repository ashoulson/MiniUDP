using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniUDP
{
  internal static class NetIO
  {
    public const int HEADER_SIZE = 
      sizeof(NetPacketType) + // Type
      sizeof(uint) +          // UID
      sizeof(ushort);         // Misc. data field

    internal static void Write(
      NetByteBuffer destination,
      NetPacketType type,
      uint uid, 
      ushort data,
      NetByteBuffer additionalDataOut = null)
    {
      destination.Write((byte)type);
      destination.Write(uid);
      destination.Write(data);
      if (additionalDataOut != null)
        destination.Append(additionalDataOut);
    }

    internal static void Read(
      NetByteBuffer source,
      out NetPacketType type,
      out uint uid,
      out ushort data,
      NetByteBuffer additionalDataIn)
    {
      type = (NetPacketType)source.ReadByte();
      uid = source.ReadUInt();
      data = source.ReadUShort();
      source.ExtractRemaining(additionalDataIn);
    }

    internal static ushort Pack(
      byte firstByte, 
      byte secondByte)
    {
      return (ushort)((firstByte << 8) | secondByte);
    }

    internal static void Unpack(
      ushort data, 
      out byte firstByte, 
      out byte secondByte)
    {
      firstByte = (byte)(data >> 8);
      secondByte = (byte)data;
    }
  }
}
