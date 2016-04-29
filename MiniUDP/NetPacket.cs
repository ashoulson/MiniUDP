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

using CommonUtil;

namespace MiniUDP
{
  internal enum NetPacketType : byte
  {
    Invalid =    0x00,

    Connect =    0x10, // Fresh connection, requires acknowledgement
    Connected =  0x20, // Acknowledgement of receipt of a connection
    Disconnect = 0x30, // Disconnected message, may or may not arrive
    Message =    0x40, // General packet payload holding data
  }

  internal class NetPacket : IUtilPoolable<NetPacket>
  {
    #region Pooling
    IUtilPool<NetPacket> IUtilPoolable<NetPacket>.Pool { get; set; }
    void IUtilPoolable<NetPacket>.Reset() { this.Reset(); }
    #endregion

    private static ushort ReadUShort(byte[] buffer, int start)
    {
      return (ushort)((buffer[start] << 8) | buffer[start + 1]);
    }

    private static void WriteUShort(byte[] buffer, int start, ushort value)
    {
      buffer[start] = (byte)(value >> 8);
      buffer[start + 1] = (byte)value;
    }

    public const int HEADER_SIZE = 5;
    private const int PACKET_SIZE = 
      NetConfig.MAX_MESSAGE_SIZE + NetPacket.HEADER_SIZE;

    internal NetPacketType PacketType { get { return this.packetType; } }
    internal ushort Ping { get { return this.ping; } }
    internal ushort Pong { get { return this.pong; } }

    private readonly byte[] message;

    private NetPacketType packetType;
    private int length;

    private ushort ping; // The peer's latest timestamp
    private ushort pong; // The last timestamp the peer received from us

    public NetPacket()
    {
      this.message = new byte[NetConfig.DATA_BUFFER_SIZE];
      this.Reset();
    }

    public void Initialize(NetPacketType type)
    {
      this.Reset();
      this.packetType = type;
    }

    internal int Read(byte[] destinationBuffer)
    {
      if (destinationBuffer.Length < NetConfig.MAX_MESSAGE_SIZE)
        throw new ArgumentException("Destination buffer too small");
      Array.Copy(this.message, destinationBuffer, this.length);
      return this.length;
    }

    internal void Write(byte[] data, int length, ushort ping, ushort pong)
    {
      if ((length < 0) || (length > NetConfig.MAX_MESSAGE_SIZE))
        throw new ArgumentOutOfRangeException("Invalid length");
      Array.Copy(data, this.message, length);
      this.length = length;
      this.ping = ping;
      this.pong = pong;
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

      this.packetType = (NetPacketType)(sourceBuffer[0]);
      this.ping = NetPacket.ReadUShort(sourceBuffer, 1);
      this.pong = NetPacket.ReadUShort(sourceBuffer, 3);

      this.length = receivedBytes - NetPacket.HEADER_SIZE;
      Array.Copy(
        sourceBuffer, 
        NetPacket.HEADER_SIZE, 
        this.message, 
        0, 
        this.length);

      return true;
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

      destinationBuffer[0] = (byte)this.packetType;
      NetPacket.WriteUShort(destinationBuffer, 1, this.ping);
      NetPacket.WriteUShort(destinationBuffer, 3, this.pong);

      Array.Copy(
        this.message, 
        0, 
        destinationBuffer, 
        NetPacket.HEADER_SIZE, 
        this.length);

      return this.length + NetPacket.HEADER_SIZE;
    }
    #endregion

    private void Reset()
    {
      this.packetType = NetPacketType.Invalid;
      this.length = 0;
      this.ping = 0;
      this.pong = 0;
    }
  }
}
