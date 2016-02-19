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
