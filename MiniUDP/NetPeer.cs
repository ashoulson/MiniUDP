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

using CommonTools;

namespace MiniUDP
{
  internal enum NetPeerStatus
  {
    Open,
    Closed,
  }

  public class NetPeer
  {
    public event PeerEvent MessagesWaiting;

    public object UserData { get; set; }

    internal IEnumerable<NetPacket> Received { get { return this.received; } }
    internal IEnumerable<NetPacket> Outgoing { get { return this.outgoing; } }
    internal IPEndPoint EndPoint { get { return this.endPoint; } }
    internal NetPeerStatus Status { get { return this.status; } }

    private readonly Queue<NetPacket> received;
    private readonly Queue<NetPacket> outgoing;
    private readonly IPEndPoint endPoint;
    private NetPeerStatus status;

    private readonly NetSocket owner;
    private double lastReceivedTime;

    public NetPeer(IPEndPoint endPoint, NetSocket owner)
    {
      this.UserData = null;

      this.received = new Queue<NetPacket>();
      this.outgoing = new Queue<NetPacket>();
      this.endPoint = endPoint;
      this.status = NetPeerStatus.Open;

      this.owner = owner;
      this.lastReceivedTime = NetTime.Time;
    }

    public override string ToString()
    {
      return this.EndPoint.ToString();
    }

    /// <summary>
    /// Queues data to be sent. The actual send will occur at the socket's
    /// next Transmit() call.
    /// </summary>
    public void EnqueueSend(byte[] buffer, int length)
    {
      if (this.VerifyOpenPeer() == false)
        return;

      NetPacket packet = this.owner.AllocatePacket();
      packet.Write(buffer, length);
      this.outgoing.Enqueue(packet);
    }

    /// <summary>
    /// Iterates over all received messages, writing to the buffer and
    /// returning the length in bytes for each one.
    /// </summary>
    public IEnumerable<int> ReadReceived(byte[] buffer)
    {
      if (this.VerifyOpenPeer() == false)
        yield break;

      foreach (NetPacket packet in this.received)
        yield return packet.Read(buffer);
    }

    /// <summary>
    /// Closes the connection and queues a disconnect message to send.
    /// Note that this message will be sent at the next socket Transmit().
    /// </summary>
    public void Close()
    {
      this.AddOutgoing(this.owner.AllocatePacket(NetPacketType.Disconnect));
      this.status = NetPeerStatus.Closed;
    }

    #region Internal Helpers
    internal void SilentClose()
    {
      this.status = NetPeerStatus.Closed;
    }

    internal void AddOutgoing(NetPacket packet)
    {
      this.outgoing.Enqueue(packet);
    }

    internal void AddReceived(NetPacket packet)
    {
      this.lastReceivedTime = NetTime.Time;
      this.received.Enqueue(packet);
    }

    internal void FlagMessagesWaiting()
    {
      if ((this.received.Count > 0) && (this.MessagesWaiting != null))
        this.MessagesWaiting.Invoke(this);
    }

    internal bool IsTimedOut()
    {
      double timeoutTime = 
        this.lastReceivedTime + NetConfig.ConnectionTimeOut;
      return timeoutTime < NetTime.Time;
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

    private bool VerifyOpenPeer()
    {
      if (this.status != NetPeerStatus.Open)
      {
        CommonDebug.LogWarning("Activity on closed peer");
        return false;
      }

      return true;
    }
    #endregion
  }
}
