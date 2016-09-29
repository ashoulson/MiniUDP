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
  public interface INetMessageOut
  {
    INetByteWriter Data { get; }
  }

  public interface INetMessageIn
  {
    INetByteReader Data { get; }
  }

  /// <summary>
  /// A multipurpose class (ab)used in two ways. Used for passing messages
  /// between threads internally (called "events" in this instance) on the 
  /// pipeline queues. Also encoded/decoded over the network to pass reliable 
  /// messages to connected peers (called "notifications" in this instance).
  /// </summary>
  internal class NetEvent
    : INetPoolable<NetEvent>
    , INetMessageOut
    , INetMessageIn
  {
    public const int HEADER_SIZE = 
      sizeof(ushort); // Byte count

    void INetPoolable<NetEvent>.Reset() { this.Reset(); }
    INetByteWriter INetMessageOut.Data { get { return this.eventData; } }
    INetByteReader INetMessageIn.Data { get { return this.eventData; } }

    // Net-encoded data
    private ushort length { get { return (ushort)this.eventData.Length; } }
    private readonly NetByteBuffer eventData;

    // Additional data for passing events around internally, not synchronized
    internal NetEventType EventType { get; private set; }
    internal NetPeer Peer { get; private set; } // Associated peer
    internal int Value { get; private set; }    // Sequence#, Error code, etc.

    // Helpers
    internal int PackSize { get { return this.length + NetEvent.HEADER_SIZE; } }
    internal ushort Sequence { get { return (ushort)this.Value; } }

    public NetEvent()
    {
      this.eventData = new NetByteBuffer(NetConst.MAX_NOTIFICATION_DATA_SIZE);
      this.Reset();
    }

    internal void Initialize(
      NetEventType type, 
      NetPeer peer, 
      int value,
      NetByteBuffer toAppend)
    {
      this.EventType = type;
      this.Peer = peer;
      this.Value = value;
      if (toAppend != null)
        this.eventData.Overwrite(toAppend);
    }

    private void Reset()
    {
      this.eventData.Reset();
      this.EventType = NetEventType.INVALID;
      this.Peer = null;
      this.Value = 0;
    }

    internal void Write(NetByteBuffer destBuffer)
    {
      destBuffer.Write(this.length);
      destBuffer.Append(this.eventData);
    }

    internal void Read(NetByteBuffer sourceBuffer)
    {
      ushort byteSize = sourceBuffer.ReadUShort();
      sourceBuffer.Extract(this.eventData, byteSize);
    }

    internal void SetSequence(ushort sequence)
    {
      this.Value = sequence;
    }
  }
}
