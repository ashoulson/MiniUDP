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
using System.Net.Sockets;

namespace MiniNet
{
  public class NetClient : NetConnector
  {
    public event Action Connected;
    public event Action TimedOut;

    private const int RETRY_COUNT = 20;
    private const double RETRY_RATE = 0.5;

    private enum ConnectionState
    {
      Connecting,
      Connected,
      Disconnected,
    }

    private NetPeer server;
    private ConnectionState connectionState;

    private int retryCount;
    private double lastRetry;

    public NetClient()
    {
      this.server = null;
      this.connectionState = ConnectionState.Disconnected;

      this.retryCount = 0;
      this.lastRetry = 0;
    }

    public void Connect(string destination)
    {
      this.Connect(NetConnector.StringToEndPoint(destination));
    }

    public void Connect(IPEndPoint destination)
    {
      if (this.connectionState == ConnectionState.Disconnected)
      {
        this.retryCount = NetClient.RETRY_COUNT;
        this.lastRetry = double.NegativeInfinity;
        this.connectionState = ConnectionState.Connecting;

        this.server = new NetPeer(destination);
        this.AddPeer(this.server);
      }
    }

    protected override bool PreProcess(NetPacket packet, IPEndPoint source)
    {
      switch (packet.PacketType)
      {
        case NetPacketType.Connected:
          this.ConnectedReceived();
          return false;

        case NetPacketType.Message:
          return true;

        default:
          NetDebug.LogWarning("Invalid packet type for client");
          return false;
      }
    }

    protected override void PreSend()
    {
      if (this.connectionState == ConnectionState.Connecting)
        this.RetryConnection();
    }

    /// <summary>
    /// Attempts to connect to the server.
    /// </summary>
    private void RetryConnection()
    {
      if (this.retryCount <= 0)
      {
        this.connectionState = ConnectionState.Disconnected;
        if (this.TimedOut != null)
          this.TimedOut.Invoke();
        return;
      }

      if ((this.lastRetry + NetClient.RETRY_RATE) < NetTime.Time)
      {
        this.SendConnect();
        this.retryCount--;
        this.lastRetry = NetTime.Time;
      }
    }

    /// <summary>
    /// Makes sure the given source matches the server's address.
    /// </summary>
    private bool VerifySource(IPEndPoint source)
    {
      if (source.Address == this.server.endPoint.Address)
        return true;

      NetDebug.LogWarning("Non-server message received and discarded");
      return false;
    }

    /// <summary>
    /// Queues up a connection packet to send to the server.
    /// </summary>
    private void SendConnect()
    {
      this.server.QueueOutgoing(this.AllocatePacket(NetPacketType.Connect));
    }

    /// <summary>
    /// Invoked when we received a "Connected" ack from the server.
    /// </summary>
    private void ConnectedReceived()
    {
      if (this.connectionState == ConnectionState.Connecting)
      {
        this.connectionState = ConnectionState.Connected;
        if (this.Connected != null)
          this.Connected.Invoke();
      }
    }
  }
}
