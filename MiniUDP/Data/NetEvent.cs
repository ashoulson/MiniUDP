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

    internal ushort sequence;           // 2 Byte
    internal ushort byteSize            /* 2 Bytes */ { get { return (ushort)this.userData.Length; } }
    internal const int EVENT_HEADER_SIZE = 4; // Total Bytes

    internal readonly NetByteBuffer userData;

    // Additional data for passing events around internally, not synchronized
    internal NetEventType EventType { get; set; }
    internal NetPeer Target { get; set; }
    internal int AdditionalData { get; set; } // Socket error code, etc.

    public NetEvent()
    {
      this.userData = new NetByteBuffer(NetConst.MAX_NOTIFICATION_DATA_SIZE);
      this.Reset();
    }

    private void Reset()
    {
      this.sequence = 0;

      this.userData.Reset();

      this.EventType = NetEventType.INVALID;
      this.Target = null;
      this.AdditionalData = 0;
    }

    internal int ComputeTotalSize()
    {
      return this.byteSize + NetEvent.EVENT_HEADER_SIZE;
    }

    internal void Write(NetByteBuffer destBuffer)
    {
      destBuffer.Write(this.sequence);
      destBuffer.Write(this.byteSize);
      destBuffer.Append(this.userData);
    }

    internal void Read(NetByteBuffer sourceBuffer)
    {
      this.sequence = sourceBuffer.ReadUShort();
      ushort byteSize = sourceBuffer.ReadUShort();
      sourceBuffer.Extract(this.userData, byteSize);
    }
  }
}
