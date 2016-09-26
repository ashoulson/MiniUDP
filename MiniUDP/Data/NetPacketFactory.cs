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
  internal class NetPacketFactory
  {
    public NetPool<NetNotification> NotificationPool { get { return this.notificationPool; } }
    public NetPool<NetSessionPacket> SessionPool { get { return this.sessionPool; } }
    public NetPool<NetPayloadPacket> PayloadPool { get { return this.payloadPool; } }
    public NetPool<NetProtocolPacket> ProtocolPool { get { return this.protocolPool; } }

    private readonly NetPool<NetNotification> notificationPool;
    private readonly NetPool<NetSessionPacket> sessionPool;
    private readonly NetPool<NetPayloadPacket> payloadPool;
    private readonly NetPool<NetProtocolPacket> protocolPool;

    internal NetPacketFactory()
    {
      this.notificationPool = new NetPool<NetNotification>();
      this.sessionPool = new NetPool<NetSessionPacket>();
      this.payloadPool = new NetPool<NetPayloadPacket>();
      this.protocolPool = new NetPool<NetProtocolPacket>();
    }
  }
}
