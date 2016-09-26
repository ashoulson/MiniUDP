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

using System;

namespace MiniUDP
{
  internal enum NetPacketType : byte
  {
    INVALID = 0,

    Protocol, // Protocol-level packet (connect, disconnect, etc.)
    Session,  // Session packet containing notifications and ping headers
    Payload,  // Raw data payload
  }

  internal enum NetProtocolType : byte
  {
    INVALID = 0x00,

    ConnectRequest,
    ConnectAccept,
    ConnectReject,
    Disconnect,
  }











































  //internal struct NetPacketHeader
  //{
  //  public const int BYTE_LENGTH = 7;

  //  public NetPacketType PacketType { get { return this.packetType; } }
  //  public byte RemoteLoss { get { return this.remoteLoss; } }
  //  public byte MessageAck { get { return this.messageAck; } }
  //  public byte MessageSequence { get { return this.messageSequence; } }
  //  public byte PingSequence { get { return this.pingSequence; } }
  //  public byte PongSequence { get { return this.pongSequence; } }
  //  public ushort PongProcessTime { get { return this.pongProcessTime; } }

  //  private const ushort SHORT_MASK = 0x0FFF;

  //  private readonly NetPacketType packetType;  // 4 bits
  //  private readonly byte remoteLoss;           // 8 bits
  //  private readonly byte messageAck;           // 8 bits
  //  private readonly byte messageSequence;      // 8 bits
  //  private readonly byte pingSequence;         // 8 bits
  //  private readonly byte pongSequence;         // 8 bits
  //  private readonly ushort pongProcessTime;    // 12 bits

  //  public NetPacketHeader(
  //    NetPacketType packetType,
  //    byte remoteLoss,
  //    byte messageAck,
  //    byte messageSequence,
  //    byte pingSequence,
  //    byte pongSequence,
  //    ushort pongProcessTime)
  //  {
  //    this.packetType = packetType;
  //    this.remoteLoss = remoteLoss;
  //    this.messageAck = messageAck;
  //    this.messageSequence = messageSequence;
  //    this.pingSequence = pingSequence;
  //    this.pongSequence = pongSequence;
  //    this.pongProcessTime = NetPacketHeader.ClampUShort(pongProcessTime);
  //  }

  //  internal void Serialize(byte[] buffer, int offset = 0)
  //  {
  //    ulong bits = 0;

  //    bits |= (ulong)this.packetType        << 52;
  //    bits |= (ulong)this.remoteLoss        << 44;
  //    bits |= (ulong)(this.messageAck)      << 36;
  //    bits |= (ulong)(this.messageSequence) << 28;
  //    bits |= (ulong)(this.pingSequence)    << 20;
  //    bits |= (ulong)(this.pongSequence)    << 12;
  //    bits |= (ulong)(this.pongProcessTime) << 0;

  //    buffer[offset + 0] = (byte)(bits >> 0);
  //    buffer[offset + 1] = (byte)(bits >> 8);
  //    buffer[offset + 2] = (byte)(bits >> 16);
  //    buffer[offset + 3] = (byte)(bits >> 24);
  //    buffer[offset + 4] = (byte)(bits >> 32);
  //    buffer[offset + 5] = (byte)(bits >> 40);
  //    buffer[offset + 6] = (byte)(bits >> 48);
  //  }

  //  internal static NetPacketHeader Deserialize(byte[] buffer, int offset = 0)
  //  {
  //    ulong bits =
  //      (ulong)buffer[offset + 0] << 0   |
  //      (ulong)buffer[offset + 1] << 8   |
  //      (ulong)buffer[offset + 2] << 16  |
  //      (ulong)buffer[offset + 3] << 24  |
  //      (ulong)buffer[offset + 4] << 32  |
  //      (ulong)buffer[offset + 5] << 40  |
  //      (ulong)buffer[offset + 6] << 48;

  //    NetPacketType packetType = (NetPacketType)(bits >> 52);
  //    byte remoteLoss =          (byte)(bits          >> 44);
  //    byte messageAck =          (byte)(bits          >> 36);
  //    byte messageSequence =     (byte)(bits          >> 28);
  //    byte pingSequence =        (byte)(bits          >> 20);
  //    byte pongSequence =        (byte)(bits          >> 12);
  //    ushort pongProcessTime =   (ushort)(bits & SHORT_MASK);

  //    return new NetPacketHeader(
  //      packetType, 
  //      remoteLoss, 
  //      messageAck,
  //      messageSequence,
  //      pingSequence, 
  //      pongSequence, 
  //      pongProcessTime);
  //  }

  //  private static ushort ClampUShort(ushort value)
  //  {
  //    if (value > NetPacketHeader.SHORT_MASK)
  //      return NetPacketHeader.SHORT_MASK;
  //    return value;
  //  }
  //}
}
