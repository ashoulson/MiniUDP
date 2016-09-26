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
  internal class NetMessageFactory
  {
    public NetPool<NetNotifyData> NotifyPool { get { return this.notifyPool; } }
    public NetPool<NetPayloadData> PayloadPool { get { return this.payloadPool; } }
    public NetPool<NetProtocolPacket> ProtocolPool { get { return this.protocolPool; } }

    private readonly NetPool<NetNotifyData> notifyPool;
    private readonly NetPool<NetPayloadData> payloadPool;
    private readonly NetPool<NetProtocolPacket> protocolPool;

    internal NetMessageFactory()
    {
      this.notifyPool = new NetPool<NetNotifyData>();
      this.payloadPool = new NetPool<NetPayloadData>();
      this.protocolPool = new NetPool<NetProtocolPacket>();
    }
  }

  public class NetPayloadData : INetPoolable<NetPayloadData>
  {
    void INetPoolable<NetPayloadData>.Reset() { this.Reset(); }
    private readonly NetByteBuffer userData;

    public NetByteBuffer UserData { get { return this.userData; } }

    public NetPayloadData()
    {
      this.userData = new NetByteBuffer();
    }

    private void Reset()
    {
      this.userData.Reset();
    }
  }

  public class NetNotifyData : INetPoolable<NetNotifyData>
  {
    void INetPoolable<NetNotifyData>.Reset() { this.Reset(); }

    public NetByteBuffer UserData { get { return this.userData; } }
    private readonly NetByteBuffer userData;

    // Delivery data
    internal NetPeer Target { get; set; }
    internal byte AckId { get; set; }

    public NetNotifyData()
    {
      this.userData = new NetByteBuffer();
    }

    private void Reset()
    {
      this.userData.Reset();
    }
  }
}
