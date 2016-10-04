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
  /// <summary>
  /// A multipurpose class (ab)used in two ways. Used for passing messages
  /// between threads internally (called "events" in this instance) on the 
  /// pipeline queues. Also encoded/decoded over the network to pass reliable 
  /// messages to connected peers (called "notifications" in this instance).
  /// </summary>
  internal class NetEvent : INetPoolable<NetEvent>
  {
    #region Header
    internal const int HEADER_SIZE = sizeof(ushort); // Byte count
    #endregion

    #region Disconnect Reason
    internal static int PackReason(
      byte internalReason, 
      byte userReason)
    {
      if ((NetKickReason)internalReason == NetKickReason.User)
      {
        if (userReason == 0)
          NetDebug.LogError("Invalid disconnect reason (user = 0)");
        return userReason;
      }
      return -internalReason;
    }

    internal static NetKickReason ReadReason(
      int reasonPacked, 
      out byte userReason)
    {
      userReason = 0;
      if (reasonPacked == 0)
        return NetKickReason.INVALID;
      if (reasonPacked < 0)
      {
        NetKickReason reason = (NetKickReason)(-reasonPacked);
        if (reason == NetKickReason.User)
        {
          NetDebug.LogError("Invalid disconnect reason (user with no data)");
          return NetKickReason.INVALID;
        }
        return (NetKickReason)(-reasonPacked);
      }

      userReason = (byte)reasonPacked;
      return NetKickReason.User;
    }
    #endregion

    void INetPoolable<NetEvent>.Reset() { this.Reset(); }

    internal byte[] EncodedData { get { return this.buffer; } }
    internal int EncodedLength { get { return this.length; } }

    // Buffer for encoded user data
    private readonly byte[] buffer;
    private ushort length;

    // Additional data for passing events around internally, not synchronized
    internal NetEventType EventType { get; private set; }
    internal NetPeer Peer { get; private set; }  // Associated peer
    internal int OtherData { get; private set; } // Sequence, Error code, etc.

    // Helpers
    internal int PackSize { get { return this.length + NetEvent.HEADER_SIZE; } }
    internal ushort Sequence { get { return (ushort)this.OtherData; } }

    public NetEvent()
    {
      this.buffer = new byte[NetConfig.MAX_DATA_SIZE];
      this.Reset();
    }

    private void Reset()
    {
      this.length = 0;
      this.EventType = NetEventType.INVALID;
      this.Peer = null;
      this.OtherData = 0;
    }

    internal void Initialize(
      NetEventType type, 
      NetPeer peer, 
      int otherData)
    {
      this.length = 0;
      this.EventType = type;
      this.Peer = peer;
      this.OtherData = otherData;
    }

    internal void Initialize(
      NetEventType type,
      NetPeer peer,
      int otherData,
      byte[] buffer,
      int position,
      int length)
    {
      if (length > NetConfig.MAX_DATA_SIZE)
        throw new OverflowException("Data too long for NetEvent");

      this.length = (ushort)length;
      this.EventType = type;
      this.Peer = peer;
      this.OtherData = otherData;

      Array.Copy(buffer, position, this.buffer, 0, length);
    }

    internal void SetSequence(ushort sequence)
    {
      this.OtherData = sequence;
    }

    #region Encoding
    internal int Pack(byte[] destBuf, int position)
    {
      position += this.PackHeader(destBuf, position);
      Array.Copy(this.buffer, 0, destBuf, position, this.length);
      return this.PackSize;
    }

    internal int Read(byte[] sourceBuf, int position, int sourceLength)
    {
      // Not enough room to read the header
      if ((sourceLength - position) < NetEvent.HEADER_SIZE)
        return -1;

      int headerLen = this.ReadHeader(sourceBuf, position, out this.length);

      // We're trying to read past the end
      if ((position + headerLen + this.length) > sourceLength)
        return -1;

      position += headerLen;
      Array.Copy(sourceBuf, position, this.buffer, 0, this.length);
      return this.PackSize;
    }

    private int PackHeader(byte[] destBuf, int position)
    {
      NetEncoding.PackU16(destBuf, position, this.length);
      return NetEvent.HEADER_SIZE;
    }

    private int ReadHeader(byte[] sourceBuf, int position, out ushort length)
    {
      length = NetEncoding.ReadU16(sourceBuf, position);
      return NetEvent.HEADER_SIZE;
    }
    #endregion
  }
}
