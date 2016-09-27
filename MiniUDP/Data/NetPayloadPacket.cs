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
  /// A reusable class for reading/writing payload packet data.
  /// </summary>
  internal class NetPayloadPacket : INetSendable
  {
    // Packet Type                           1 Byte
    internal uint uniqueId;               // 4 Bytes
    internal byte sequenceId;             // 1 Byte
    internal const int PAYLOAD_HEADER_SIZE = 6; // Total Bytes

    internal readonly NetByteBuffer payload;

    public NetPayloadPacket()
    {
      this.payload = new NetByteBuffer(NetConst.MAX_PAYLOAD_DATA_SIZE);
      this.Reset();
    }

    internal void Reset()
    {
      this.uniqueId = 0;
      this.sequenceId = 0;
      this.payload.Reset();
    }

    public void Write(NetByteBuffer destBuffer)
    {
      destBuffer.Write((byte)NetPacketType.Payload);
      destBuffer.Write(this.uniqueId);
      destBuffer.Write(this.sequenceId);
      destBuffer.Append(this.payload);
    }

    internal void Read(NetByteBuffer sourceBuffer)
    {
      sourceBuffer.ReadByte(); // Skip packet type
      this.uniqueId = sourceBuffer.ReadUInt();
      this.sequenceId = sourceBuffer.ReadByte();
      sourceBuffer.ExtractRemaining(this.payload);
    }
  }
}
