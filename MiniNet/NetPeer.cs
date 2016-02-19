/*
 *  MiniNet - A Simple UDP Layer for Shipping and Receiving Byte Arrays
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
using System.Net;

namespace MiniNet
{
  public class NetPeer
  {
    public object UserData { get; set; }

    internal IPEndPoint endPoint;
    internal Queue<NetPacket> received;
    internal Queue<NetPacket> outgoing;

    public NetPeer(IPEndPoint endPoint)
    {
      this.UserData = null;

      this.endPoint = endPoint;
      this.received = new Queue<NetPacket>();
      this.outgoing = new Queue<NetPacket>();
    }

    internal void QueueOutgoing(NetPacket packet)
    {
      this.outgoing.Enqueue(packet);
    }

    internal void QueueReceived(NetPacket packet)
    {
      this.received.Enqueue(packet);
    }

    #region Local I/O
    internal NetPacket GetReceived()
    {
      if (this.received.Count > 0)
        return this.received.Dequeue();
      return null;
    }

    internal NetPacket GetOutgoing()
    {
      if (this.outgoing.Count > 0)
        return this.outgoing.Dequeue();
      return null;
    }
    #endregion
  }
}
