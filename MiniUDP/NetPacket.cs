/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2015-2016 - Alexander Shoulson - http://ashoulson.com
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

using CommonTools;

namespace MiniUDP
{
  internal enum NetPacketType : ushort
  {
    Invalid = 0,

    Connect = 0x1000,    // Fresh connection, requires acknowledgement
    Connected = 0x2000,  // Acknowledgement of receipt of a connection
    Disconnect = 0x3000, // Disconnected message, may or may not arrive
    Message = 0x4000,    // General packet payload holding data
  }

  internal class NetPacket : IPoolable
  {
    // We pack the length and message type into 2 bytes
    private const int METADATA_SIZE = 2;
    private const int PACKET_SIZE = 
      NetConfig.MAX_MESSAGE_SIZE + NetPacket.METADATA_SIZE;

    // Mask for separating out the length and packet type data
    private const ushort LENGTH_MASK = 0x0FFF;

    #region IPoolable Members
    Pool IPoolable.Pool { get; set; }
    void IPoolable.Reset() { this.Reset(); }
    #endregion

    internal NetPacketType PacketType { get { return this.packetType; } }

    private NetPacketType packetType;
    private int length;
    private byte[] message;

    public NetPacket()
    {
      this.packetType = NetPacketType.Invalid;

      this.length = 0;
      this.message = new byte[NetConfig.DATA_BUFFER_SIZE];
    }

    public void Initialize(NetPacketType type)
    {
      this.packetType = type;

      this.length = 0;
    }

    public void Free()
    {
      Pool.Free(this);
    }

    public int Read(byte[] destinationBuffer)
    {
      if (destinationBuffer.Length < NetConfig.MAX_MESSAGE_SIZE)
        throw new ArgumentException("Destination buffer too small");

      Array.Copy(this.message, destinationBuffer, this.length);
      return this.length;
    }

    public void Write(byte[] data, int length)
    {
      if ((length < 0) || (length > NetConfig.MAX_MESSAGE_SIZE))
        throw new ArgumentOutOfRangeException("Invalid length");

      Array.Copy(data, this.message, length);
      this.length = length;
    }

    #region Network I/O
    /// <summary>
    /// Copies the data from the input buffer and stores it internally.
    /// Returns false if the length had bad data.
    /// </summary>
    internal bool NetInput(byte[] sourceBuffer, int receivedBytes)
    {
      if (sourceBuffer.Length < NetPacket.PACKET_SIZE)
        throw new ArgumentException("Source buffer too small");

      int metadata = ((int)sourceBuffer[0] << 8) + sourceBuffer[1];
      this.packetType = (NetPacketType)(metadata & ~NetPacket.LENGTH_MASK);
      this.length = metadata & NetPacket.LENGTH_MASK;

      if (receivedBytes == (this.length + NetPacket.METADATA_SIZE))
      {
        Array.Copy(
          sourceBuffer, 
          NetPacket.METADATA_SIZE, 
          this.message, 
          0,
          this.length);
        return true;
      }

      this.packetType = NetPacketType.Invalid;
      return false;
    }

    /// <summary>
    /// Copies this packet to the given buffer and returns the send length.
    /// </summary>
    internal int NetOutput(byte[] destinationBuffer)
    {
      if (this.packetType == NetPacketType.Invalid)
        throw new InvalidOperationException("Can't send invalid packet");
      if (destinationBuffer.Length < NetPacket.PACKET_SIZE)
        throw new ArgumentException("Destination buffer too small");

      int metadata = this.length | (ushort)this.packetType;
      destinationBuffer[0] = (byte)(metadata >> 8);
      destinationBuffer[1] = (byte)metadata;

      Array.Copy(
        this.message, 
        0, 
        destinationBuffer, 
        NetPacket.METADATA_SIZE, 
        this.length);

      return this.length + NetPacket.METADATA_SIZE;
    }
    #endregion

    private void Reset()
    {
      this.packetType = NetPacketType.Invalid;
      this.length = 0;
    }
  }
}
