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

using CommonTools;

namespace MiniUDP
{
  public abstract class NetConnector
  {
    private const int MAX_BUFFER_SIZE = 2048;

    #region Static Methods
    public static IPEndPoint StringToEndPoint(string address)
    {
      string[] split = address.Split(':');
      string stringAddress = split[0];
      string stringPort = split[1];

      int port = int.Parse(stringPort);
      IPAddress ipaddress = IPAddress.Parse(stringAddress);
      IPEndPoint endpoint = new IPEndPoint(ipaddress, port);

      if (endpoint == null) 
        throw new ArgumentException("Failed to parse address: " + address);
      return endpoint;
    }
    #endregion

    #region Properties and Fields
    private byte[] dataBuffer;
    private GenericPool<NetPacket> packetPool;
    private Dictionary<IPEndPoint, NetPeer> peers;

    protected Socket socket;
    #endregion

    #region Abstract Methods
    /// <summary>
    /// Pre-processes packets for protocol-level changes before the packet
    /// gets pushed to a peer's message queue. Returns true if the packet
    /// should be passed on to peers, false if the packet is consumed.
    /// </summary>
    protected abstract bool PreProcess(NetPacket packet, IPEndPoint source);

    /// <summary>
    /// Allows inheritors to perform special tasks prior to message dispatch.
    /// </summary>
    protected virtual void PreSend() { }
    #endregion

    #region Constructors
    public NetConnector()
    {
      this.dataBuffer = new byte[NetConnector.MAX_BUFFER_SIZE];
      this.packetPool = new GenericPool<NetPacket>();
      this.peers = new Dictionary<IPEndPoint, NetPeer>();

      this.socket =
        new Socket(
          AddressFamily.InterNetwork,
          SocketType.Dgram,
          ProtocolType.Udp);

      this.socket.ReceiveBufferSize = NetConnector.MAX_BUFFER_SIZE;
      this.socket.SendBufferSize = NetConnector.MAX_BUFFER_SIZE;
      this.socket.Blocking = false;
    }
    #endregion

    #region Public Interface
    /// <summary>
    /// Called at the beginning of processing. Reads all incoming data,
    /// processes it, and assigns packets to the peers it received them from.
    /// </summary>
    public void Poll()
    {
      while (this.CanReceive())
      {
        IPEndPoint source;
        NetPacket packet = this.TryReceive(out source);

        if ((packet != null) && this.PreProcess(packet, source))
        {
          NetPeer sourcePeer;
          if (this.peers.TryGetValue(source, out sourcePeer))
            sourcePeer.QueueReceived(packet);
          else
            NetDebug.LogWarning("Message from unrecognized peer: " + source);
        }
      }
    }

    /// <summary>
    /// Called at the end of processing, sends any packets that have been
    /// queued up for transmission to all relevant peers.
    /// </summary>
    public void Send()
    {
      this.PreSend();

      foreach (NetPeer peer in this.peers.Values)
        this.SendPeerTraffic(peer);
    }

    /// <summary>
    /// Allocates a message packet for use externally.
    /// </summary>
    public NetPacket AllocatePacket()
    {
      return this.AllocatePacket(NetPacketType.Message);
    }
    #endregion

    #region Protected Helpers
    protected NetPacket AllocatePacket(NetPacketType packetType)
    {
      NetPacket packet = this.packetPool.Allocate();
      packet.Initialize(packetType);
      return packet;
    }

    protected void AddPeer(NetPeer peer)
    {
      this.peers.Add(peer.endPoint, peer);
    }

    protected NetPeer GetPeer(IPEndPoint endPoint)
    {
      NetPeer peer = null;
      if (this.peers.TryGetValue(endPoint, out peer))
        return peer;
      return null;
    }

    /// <summary> 
    /// Returns true if OS socket has data available for read. 
    /// </summary>
    protected bool CanReceive()
    {
      return this.socket.Poll(0, SelectMode.SelectRead);
    }

    /// <summary> 
    /// Attempts to read from OS socket. Returns false if the read fails
    /// or if there is nothing to read.
    /// </summary>
    protected NetPacket TryReceive(out IPEndPoint source)
    {
      source = null;

      try
      {
        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
        int receiveCount = 
          this.socket.ReceiveFrom(
            this.dataBuffer,
            this.dataBuffer.Length, 
            SocketFlags.None, 
            ref endPoint);

        if (receiveCount > 0)
        {
          source = endPoint as IPEndPoint;
          NetPacket packet = this.packetPool.Allocate();
          if (packet.NetInput(this.dataBuffer, receiveCount))
            return packet;
        }

        return null;
      }
      catch
      {
        return null;
      }
    }

    /// <summary> 
    /// Attempts to send data to endpoint via OS socket. 
    /// Returns false if the send failed.
    /// </summary>
    protected bool TrySend(
      NetPacket packet,
      IPEndPoint destination)
    {
      try
      {
        int bytesToSend = packet.NetOutput(this.dataBuffer);
        int bytesSent = 
          this.socket.SendTo(
            this.dataBuffer,
            bytesToSend, 
            SocketFlags.None, 
            destination);

        return (bytesSent == bytesToSend);
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Sends out all pending outgoing packets for a given peer.
    /// </summary>
    protected void SendPeerTraffic(NetPeer peer)
    {
      NetPacket packet = null;
      while ((packet = peer.GetOutgoing()) != null)
      {
        this.TrySend(packet, peer.endPoint);
        packet.Free();
      }
    }
    #endregion
  }
}
