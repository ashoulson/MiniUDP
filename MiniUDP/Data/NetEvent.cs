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
  /// A poor abused multipurpose class. Events sent over the network are 
  /// referred to as "notifications" but still use this event data structure
  /// for simplicity's and efficiency's sakes as they're very similar.
  /// </summary>
  internal class NetEvent
    : INetPoolable<NetEvent>
    , INetMessageOut
    , INetMessageIn
  {
    void INetPoolable<NetEvent>.Reset() { this.Reset(); }
    INetByteWriter INetMessageOut.Data { get { return this.userData; } }
    INetByteReader INetMessageIn.Data { get { return this.userData; } }

    // Network header data
    internal ushort Sequence            /* 2 Byte  */ { get; private set; }
    internal ushort ByteSize            /* 2 Bytes */ { get { return (ushort)this.userData.Length; } }
    internal const int EVENT_HEADER_SIZE = 4; // Total Bytes

    internal readonly NetByteBuffer userData;

    // Additional data for passing events around internally, not synchronized
    internal NetEventType EventType { get; private set; }
    internal NetPeer Peer { get; private set; }
    internal int AdditionalData { get; private set; } // Socket error code, etc.

    public NetEvent()
    {
      this.userData = new NetByteBuffer(NetConst.MAX_NOTIFICATION_DATA_SIZE);
      this.Reset();
    }

    internal void Initialize(NetEventType type)
    {
      this.Initialize(type, null, 0, 0, null);
    }

    internal void Initialize(
      NetEventType type, 
      NetPeer peer, 
      ushort sequence, 
      int additionalData,
      NetByteBuffer toAppend)
    {
      this.EventType = type;
      this.Peer = peer;
      this.Sequence = sequence;
      this.AdditionalData = additionalData;
      if (toAppend != null)
        this.userData.Overwrite(toAppend);
    }

    private void Reset()
    {
      this.Sequence = 0;

      this.userData.Reset();

      this.EventType = NetEventType.INVALID;
      this.Peer = null;
      this.AdditionalData = 0;
    }

    internal int ComputeTotalSize()
    {
      return this.ByteSize + NetEvent.EVENT_HEADER_SIZE;
    }

    internal void Write(NetByteBuffer destBuffer)
    {
      destBuffer.Write(this.Sequence);
      destBuffer.Write(this.ByteSize);
      destBuffer.Append(this.userData);
    }

    internal void Read(NetByteBuffer sourceBuffer)
    {
      this.Sequence = sourceBuffer.ReadUShort();
      ushort byteSize = sourceBuffer.ReadUShort();
      sourceBuffer.Extract(this.userData, byteSize);

      this.Peer = null; // Will be assigned via AssignPeer afterwards
      this.AdditionalData = 0;
    }

    internal void SetSequence(ushort sequence)
    {
      this.Sequence = sequence;
    }

    internal void AssignPeer(NetPeer target)
    {
      this.Peer = target;
    }
  }
}
