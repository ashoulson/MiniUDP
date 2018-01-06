/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
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
using System.Net.Sockets;

namespace MiniUDP
{
  /// <summary>
  /// A class for storing, transmitting, and reading reliable net messages.
  /// </summary>
  internal class NetMessage : INetPoolable<NetMessage>
  {
    void INetPoolable<NetMessage>.Reset() { this.Reset(); }

    internal byte[] EncodedData { get { return this.buffer; } }
    internal ushort EncodedLength { get { return this.length; } }
    internal NetPeer Peer { get; private set; }  // Associated peer
    internal ushort Sequence { get; set; }

    private byte[] buffer;
    private ushort length;

    public NetMessage()
    {
      this.buffer = new byte[NetConfig.DATA_INITIAL];
      this.Reset();
    }

    private void Reset()
    {
      this.length = 0;
      this.Peer = null;
      this.Sequence = 0;
    }

    internal void Initialize(
      NetPeer peer)
    {
      this.Reset();
      this.Peer = peer;
    }

    internal bool ReadData(byte[] sourceBuffer, int position, ushort length)
    {
      if (length > NetConfig.DATA_MAXIMUM)
        return false;

      // Resize if necessary
      int paddedLength = length + NetConfig.DATA_PADDING;
      if (this.buffer.Length < paddedLength)
        this.buffer = new byte[paddedLength];

      // Copy the contents
      Array.Copy(sourceBuffer, position, this.buffer, 0, length);
      this.length = length;
      return true;
    }
  }
}
