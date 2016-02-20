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
using System.Collections;
using System.Collections.Generic;
using System.Net;

namespace MiniUDP
{
  public delegate void MessagesWaiting(NetPeer source);

  public class NetPeer
  {
    public event MessagesWaiting MessagesWaiting;

    public object UserData { get; set; }

    internal IEnumerable<NetPacket> Received { get { return this.received; } }
    internal IEnumerable<NetPacket> Outgoing { get { return this.outgoing; } }
    internal IPEndPoint EndPoint { get { return this.endPoint; } }

    private readonly Queue<NetPacket> received;
    private readonly Queue<NetPacket> outgoing;
    private readonly IPEndPoint endPoint;
    private readonly NetSocket owner;

    public NetPeer(IPEndPoint endPoint, NetSocket owner)
    {
      this.UserData = null;

      this.received = new Queue<NetPacket>();
      this.outgoing = new Queue<NetPacket>();
      this.endPoint = endPoint;
      this.owner = owner;
    }

    public override string ToString()
    {
      return this.EndPoint.ToString();
    }

    public void QueueOutgoing(byte[] buffer, int length)
    {
      NetPacket packet = this.owner.AllocatePacket();
      packet.Write(buffer, length);
      this.outgoing.Enqueue(packet);
    }

    public IEnumerable<int> ReadReceived(byte[] buffer)
    {
      foreach (NetPacket packet in this.received)
        yield return packet.Read(buffer);
    }

    internal void QueueOutgoing(NetPacket packet)
    {
      this.outgoing.Enqueue(packet);
    }

    internal void QueueReceived(NetPacket packet)
    {
      this.received.Enqueue(packet);
    }

    internal void FlagMessagesWaiting()
    {
      if ((this.received.Count > 0) && (this.MessagesWaiting != null))
        this.MessagesWaiting.Invoke(this);
    }

    internal void ClearReceived()
    {
      foreach (NetPacket packet in this.received)
        packet.Free();
      this.received.Clear();
    }

    internal void ClearOutgoing()
    {
      foreach (NetPacket packet in this.outgoing)
        packet.Free();
      this.outgoing.Clear();
    }
  }
}
