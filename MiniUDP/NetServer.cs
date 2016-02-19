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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace MiniUDP
{
  public class NetServer : NetConnector
  {
    public event Action<NetPeer> Connected;

    private Dictionary<IPEndPoint, NetPeer> clients;

    public NetServer()
    {
      this.clients = new Dictionary<IPEndPoint, NetPeer>();
    }

    /// <summary>
    /// Starts the socket using the supplied endpoint.
    /// If the port is taken, the given port will be incremented to a free port.
    /// </summary>
    protected void Start(int port)
    {
      try
      {
        this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
      }
      catch (SocketException exception)
      {
        if (exception.ErrorCode == 10048)
          NetDebug.LogError("Port " + port + " unavailable!");
        else
          NetDebug.LogError(exception.Message);
        return;
      }
    }

    protected override bool PreProcess(NetPacket packet, IPEndPoint source)
    {
      switch (packet.PacketType)
      {
        case NetPacketType.Connect:
          this.ConnectReceived(source);
          return false;

        case NetPacketType.Message:
          return true;

        default:
          NetDebug.LogWarning("Invalid packet type for server");
          return false;
      }
    }

    private void ConnectReceived(IPEndPoint source)
    {
      NetPeer peer = this.GetPeer(source);
      if (peer == null)
      {
        peer = new NetPeer(source);
        this.AddPeer(peer);

        if (this.Connected != null)
          this.Connected.Invoke(peer);
      }

      peer.QueueOutgoing(this.AllocatePacket(NetPacketType.Connected));
    }
  }
}
