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

namespace MiniUDP
{
  /// <summary>
  /// A reusable class for reading/writing protocol packet data.
  /// </summary>
  internal class NetProtocolPacket : INetSendable
  {
    // Packet Type                            1 Byte
    internal uint UID                      /* 4 Bytes */ { get; private set; }
    internal NetProtocolType ProtocolType  /* 1 Byte  */ { get; private set; }
    internal const int PROTOCOL_HEADER_SIZE = 6; // Total Bytes

    internal readonly NetByteBuffer data;

    public NetProtocolPacket()
    {
      this.data = new NetByteBuffer(NetConst.MAX_PROTOCOL_DATA_SIZE);
      this.Reset();
    }

    internal void Reset()
    {
      this.UID = 0;
      this.ProtocolType = NetProtocolType.INVALID;
      this.data.Reset();
    }

    internal void Initialize(
      uint uid,
      NetProtocolType protocolType,
      NetByteBuffer data)
    {
      this.UID = uid;
      this.ProtocolType = protocolType;
      if (data != null)
        this.data.Overwrite(data);
    }

    public void Write(NetByteBuffer destBuffer)
    {
      destBuffer.Write((byte)NetPacketType.Protocol);
      destBuffer.Write(this.UID);
      destBuffer.Write((byte)this.ProtocolType);
      destBuffer.Append(this.data);
    }

    internal void Read(NetByteBuffer sourceBuffer)
    {
      sourceBuffer.ReadByte(); // Skip packet type
      this.UID = sourceBuffer.ReadUInt();
      this.ProtocolType = (NetProtocolType)sourceBuffer.ReadByte();
      sourceBuffer.ExtractRemaining(this.data);
    }
  }
}
