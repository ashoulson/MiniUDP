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

    private static ushort Combine(byte high, byte low)
    {
      return (ushort)((high << 8) | low);
    }

    private static byte GetHigh(ushort value)
    {
      return (byte)(value >> 8);
    }

    private static byte GetLow(ushort value)
    {
      return (byte)(value);
    }

    public const int HEADER_FULL = 8;
    public const int HEADER_SHORT = 1;
    public const ushort MAX_PROCESS_TIME = 0x0FFF;

    private const int MAX_PACKET_SIZE = 
      NetConfig.MAX_PAYLOAD_SIZE + NetPacket.HEADER_FULL;

    internal NetPacketType PacketType { get { return this.packetType; } }
    internal ushort ProcessTime { get { return this.processTime; } }
    internal byte Sequence { get { return this.sequence; } }
    internal ushort PingStamp { get { return this.pingStamp; } }
    internal ushort PongStamp { get { return this.pongStamp; } } 
    internal float Loss { get { return this.loss; } }

    private readonly byte[] payload;
    private int length;

    private NetPacketType packetType; // 4 bits
    private ushort processTime;       // 12 bits
    private byte sequence;            // 8 bits
    private ushort pingStamp;         // 16 bits
    private ushort pongStamp;         // 16 bits
    private float loss;               // 8 bits

    public NetPacket()
    {
      this.payload = new byte[NetConfig.DATA_BUFFER_SIZE];
      this.Reset();
    }

    public void Initialize(
      NetPacketType type)
    {
      this.Reset();
      this.packetType = type;
    }

    public void WriteMetadata(
      long processTime,
      byte sequence,
      ushort pingStamp,
      ushort pongStamp,
      float loss)
    {
      if (processTime > NetPacket.MAX_PROCESS_TIME)
        processTime = NetPacket.MAX_PROCESS_TIME;
      if (processTime < 0)
        processTime = 0;

      if (loss > 1.0f)
        loss = 1.0f;
      if (loss < 0.0f)
        loss = 0.0f;

      this.processTime = (ushort)processTime;
      this.sequence = sequence;
      this.pingStamp = pingStamp;
      this.pongStamp = pongStamp;
      this.loss = loss;
    }

    #region Payload I/O
    internal void PayloadIn(byte[] data, int length)
    {
      if ((length < 0) || (length > NetConfig.MAX_PAYLOAD_SIZE))
        throw new ArgumentOutOfRangeException("Invalid length");
      Array.Copy(data, this.payload, length);
      this.length = length;
    }

    internal int PayloadOut(byte[] destinationBuffer)
    {
      if (destinationBuffer.Length < NetConfig.MAX_PAYLOAD_SIZE)
        throw new ArgumentException("Destination buffer too small");
      Array.Copy(this.payload, destinationBuffer, this.length);
      return this.length;
    }
    #endregion

    #region Network I/O
    /// <summary>
    /// Copies the data from the input buffer and stores it internally.
    /// Returns false if the length had bad data.
    /// </summary>
    internal bool NetInput(byte[] sourceBuf, int receivedBytes)
    {
      if (sourceBuf.Length < NetPacket.MAX_PACKET_SIZE)
        throw new ArgumentException("Source buffer too small");

      int headerSize = this.ReadHeader(sourceBuf);
      if (this.packetType == NetPacketType.Invalid)
        return false;
      if (Enum.IsDefined(typeof(NetPacketType), this.packetType) == false)
        return false;

      this.length = receivedBytes - headerSize;
      if (length < 0)
        return false;

      Array.Copy(sourceBuf, headerSize, this.payload, 0, this.length);
      return true;
    }

    /// <summary>
    /// Copies this packet to the given buffer and returns the send length.
    /// </summary>
    internal int NetOutput(byte[] destBuf)
    {
      if (this.packetType == NetPacketType.Invalid)
        throw new InvalidOperationException("Can't send invalid packet");
      if (destBuf.Length < NetPacket.MAX_PACKET_SIZE)
        throw new ArgumentException("Destination buffer too small");

      int headerSize = this.WriteHeader(destBuf);

      Array.Copy(this.payload, 0, destBuf, headerSize, this.length);
      return this.length + headerSize;
    }

    private int ReadHeader(byte[] sourceBuffer)
    {
      this.packetType = (NetPacketType)(sourceBuffer[0] & 0xF0);

      if (packetType == NetPacketType.Message)
      {
        ulong chunk =
          (ulong)sourceBuffer[0] << 56 |
          (ulong)sourceBuffer[1] << 48 |
          (ulong)sourceBuffer[2] << 40 |
          (ulong)sourceBuffer[3] << 32 |
          (ulong)sourceBuffer[4] << 24 |
          (ulong)sourceBuffer[5] << 16 |
          (ulong)sourceBuffer[6] << 8  |
          (ulong)sourceBuffer[7];

        this.processTime =    (ushort)((chunk >> 48) & 0x0FFF);
        this.sequence =       (byte)(chunk >> 40);
        this.pingStamp =      (ushort)(chunk >> 24);
        this.pongStamp =      (ushort)(chunk >> 8);
        this.loss =           ((byte)chunk) / 255.0f;

        return NetPacket.HEADER_FULL;
      }
      return NetPacket.HEADER_SHORT;
    }

    private int WriteHeader(byte[] destinationBuffer)
    {
      destinationBuffer[0] = (byte)this.packetType;

      if (this.packetType == NetPacketType.Message)
      {
        ulong chunk =
          ((ulong)this.packetType << 56) |
          ((ulong)(this.processTime & 0x0FFF) << 48) |
          ((ulong)(this.sequence) << 40) |
          ((ulong)(this.pingStamp) << 24) |
          ((ulong)(this.pongStamp) << 8) |
          ((ulong)((byte)(this.loss * 255)));

        destinationBuffer[0] = (byte)(chunk >> 56);
        destinationBuffer[1] = (byte)(chunk >> 48);
        destinationBuffer[2] = (byte)(chunk >> 40);
        destinationBuffer[3] = (byte)(chunk >> 32);
        destinationBuffer[4] = (byte)(chunk >> 24);
        destinationBuffer[5] = (byte)(chunk >> 16);
        destinationBuffer[6] = (byte)(chunk >> 8);
        destinationBuffer[7] = (byte)(chunk);

        return NetPacket.HEADER_FULL;
      }
      return NetPacket.HEADER_SHORT;
    }
    #endregion

    private void Reset()
    {
      this.length = 0;

      this.packetType = NetPacketType.Invalid;
      this.processTime = 0;
      this.sequence = 0;
      this.pingStamp = 0;
      this.pongStamp = 0;
      this.loss = 0.0f;
    }
  }
}
