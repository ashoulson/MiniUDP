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
  public delegate void PeerEvent(NetPeer source);
  public delegate void ConnectionEvent(string address);

  public class NetSocket
  {
    /// <summary>
    /// A remote peer has connected.
    /// </summary>
    public event PeerEvent Connected;

    /// <summary>
    /// A local peer has been closed and its disconnect message has been sent.
    /// </summary>
    public event PeerEvent Closed;

    /// <summary>
    /// A remote peer has disconnected.
    /// </summary>
    public event PeerEvent Disconnected;

    /// <summary>
    /// A remote peer has timed out.
    /// </summary>
    public event PeerEvent TimedOut;

    /// <summary>
    /// An attempt to connect to a remote peer has timed out.
    /// </summary>
    public event ConnectionEvent ConnectFailed;

    private class PendingConnection
    {
      internal IPEndPoint EndPoint { get { return this.endPoint; } }

      private readonly IPEndPoint endPoint;
      private double lastAttempt;
      private double expireTime;

      internal PendingConnection(IPEndPoint endPoint)
      {
        this.endPoint = endPoint;
        this.lastAttempt = double.NegativeInfinity;
        this.expireTime = NetTime.Time + NetConfig.ConnectionAttemptTimeOut;
      }

      internal void LogAttempt()
      {
        this.lastAttempt = NetTime.Time;
      }

      internal bool TimeToRetry()
      {
        double nextTime = this.lastAttempt + NetConfig.ConnectionRetryRate;
        return nextTime < NetTime.Time;
      }

      internal bool HasExpired()
      {
        return NetTime.Time > this.expireTime;
      }
    }

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

    public static IPAddress StringToAddress(string address)
    {
      string[] split = address.Split(':');
      string stringAddress = split[0];
      string stringPort = split[1];

      int port = int.Parse(stringPort);
      IPAddress ipaddress = IPAddress.Parse(stringAddress);

      if (ipaddress == null)
        throw new ArgumentException("Failed to parse address: " + address);
      return ipaddress;
    }
    #endregion

    #region Properties and Fields
    public bool UseWhiteList { get; set; }
    public int PeerCount { get { return this.peers.Count; } }

    protected Socket socket;

    private byte[] dataBuffer;
    private GenericPool<NetPacket> packetPool;
    private Dictionary<IPEndPoint, NetPeer> peers;
    private Dictionary<IPEndPoint, PendingConnection> pendingConnections;
    private HashSet<IPAddress> whiteList;

    // Pre-allocated lists for iteration tasks
    private List<NetPeer> reusablePeerList;
    private List<PendingConnection> reusableConnectionList;
    #endregion

    #region Constructors
    public NetSocket()
    {
      this.dataBuffer = new byte[NetConfig.DATA_BUFFER_SIZE];
      this.packetPool = new GenericPool<NetPacket>();
      this.peers = new Dictionary<IPEndPoint, NetPeer>();
      this.pendingConnections = new Dictionary<IPEndPoint, PendingConnection>();
      this.whiteList = new HashSet<IPAddress>();

      this.reusablePeerList = new List<NetPeer>();
      this.reusableConnectionList = new List<PendingConnection>();

      this.socket =
        new Socket(
          AddressFamily.InterNetwork,
          SocketType.Dgram,
          ProtocolType.Udp);
      this.ConfigureSocket();
    }
    #endregion

    #region Public Interface
    /// <summary>
    /// Adds an address to the whitelist for refusing connections.
    /// </summary>
    public void AddToWhiteList(string address)
    {
      this.whiteList.Add(NetSocket.StringToAddress(address));
    }

    /// <summary>
    /// Starts the socket using the supplied endpoint.
    /// If the port is taken, the given port will be incremented to a free port.
    /// </summary>
    public void Bind(int port)
    {
      try
      {
        this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
      }
      catch (SocketException exception)
      {
        if (exception.ErrorCode == 10048)
          CommonDebug.LogError("Port " + port + " unavailable!");
        else
          CommonDebug.LogError(exception.Message);
        return;
      }
    }

    /// <summary>
    /// Begins the process of connecting to an address given as a string in 
    /// "IP.IP.IP.IP:PORT" format.
    /// </summary>
    public void Connect(string address)
    {
      IPEndPoint endPoint = NetSocket.StringToEndPoint(address);
      if (this.peers.ContainsKey(endPoint) == false)
        this.pendingConnections.Add(endPoint, new PendingConnection(endPoint));
    }

    /// <summary>
    /// Queues up a disconnect message to a given peer.
    /// </summary>
    public void Disconnect(NetPeer peer)
    {
      peer.AddOutgoing(this.AllocatePacket(NetPacketType.Disconnect));
    }

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
            sourcePeer.AddReceived(packet);
          else
            CommonDebug.LogWarning(
              "Message from unrecognized peer: " + source);
        }
      }

      List<NetPeer> timedOutPeers = this.GetPeerList();
      foreach (NetPeer peer in this.peers.Values)
      {
        peer.FlagMessagesWaiting();
        if (peer.IsTimedOut())
          timedOutPeers.Add(peer);
      }

      foreach (NetPeer peer in timedOutPeers)
      {
        peer.SilentClose();
        this.peers.Remove(peer.EndPoint);
        if (this.TimedOut != null)
          this.TimedOut.Invoke(peer);
      }
    }

    /// <summary>
    /// Called at the end of processing, sends any packets that have been
    /// queued up for transmission to all relevant peers.
    /// </summary>
    public void Transmit()
    {
      this.RetryConnections();

      List<NetPeer> closedPeers = this.GetPeerList();
      foreach (NetPeer peer in this.peers.Values)
      {
        foreach (NetPacket packet in peer.Outgoing)
          this.TrySend(packet, peer.EndPoint);

        // Clean all incoming and outgoing packets
        peer.ClearOutgoing();
        peer.ClearReceived();

        if (peer.Status == NetPeerStatus.Closed)
          closedPeers.Add(peer);
      }

      foreach (NetPeer peer in closedPeers)
      {
        peer.SilentClose();
        this.peers.Remove(peer.EndPoint);
        if (this.Closed != null)
          this.Closed.Invoke(peer);
      }
    }

    /// <summary>
    /// Queues disconnect packets for all peers (but they won't be sent
    /// until the next Transmit() call).
    /// </summary>
    public void Shutdown()
    {
      foreach (NetPeer peer in this.peers.Values)
        peer.Close();
    }
    #endregion

    #region Protected Helpers
    /// <summary>
    /// Allocates a packet from the pool for use in transmission. Packets
    /// will be automatically freed after send -- no need to do it manually.
    /// </summary>
    internal NetPacket AllocatePacket(
      NetPacketType packetType = NetPacketType.Message)
    {
      NetPacket packet = this.packetPool.Allocate();
      packet.Initialize(packetType);
      return packet;
    }

    /// <summary>
    /// Polls each pending connection to see if it is time to retry, and
    /// does so if applicable. Cleans up any expired connection attempts.
    /// </summary>
    private void RetryConnections()
    {
      List<PendingConnection> expired = this.GetConnectionList();
      foreach (PendingConnection pending in this.pendingConnections.Values)
      {
        if (pending.TimeToRetry())
        {
          this.TrySend(
            this.AllocatePacket(NetPacketType.Connect),
            pending.EndPoint);
          pending.LogAttempt();
        }
        else if (pending.HasExpired())
        {
          expired.Add(pending);
        }
      }

      foreach (PendingConnection pending in expired)
      {
        this.pendingConnections.Remove(pending.EndPoint);
        if (this.ConnectFailed != null)
          this.ConnectFailed.Invoke(pending.EndPoint.ToString());
      }
    }

    /// <summary>
    /// Pre-processes an incoming packet for protocol-level traffic before
    /// sending anything to higher levels. Returns true if the packet
    /// should be escalated to the application level, false if the packet
    /// should be considered "consumed".
    /// </summary>
    private bool PreProcess(NetPacket packet, IPEndPoint source)
    {
      switch (packet.PacketType)
      {
        case NetPacketType.Connect:
          this.HandleConnection(source, true);
          return false;

        case NetPacketType.Connected:
          this.HandleConnection(source, false);
          return false;

        case NetPacketType.Disconnect:
          this.HandleDisconnection(source);
          return false;

        case NetPacketType.Message:
          return true;

        default:
          CommonDebug.LogWarning("Invalid packet type for server");
          return false;
      }
    }

    /// <summary>
    /// Handles incoming connection-related packets by creating peers.
    /// </summary>
    private void HandleConnection(IPEndPoint source, bool sendResponse)
    {
      if (this.VerifySource(source))
      {
        NetPeer peer = this.RegisterPeer(source);

        if (sendResponse)
          peer.AddOutgoing(this.AllocatePacket(NetPacketType.Connected));

        if (this.pendingConnections.ContainsKey(source))
          this.pendingConnections.Remove(source);
      }
    }

    /// <summary>
    /// Handles incoming disconnection-related packets by removing peers.
    /// </summary>
    private void HandleDisconnection(IPEndPoint source)
    {
      NetPeer peer = null;
      if (this.peers.TryGetValue(source, out peer))
      {
        peer.SilentClose();
        this.peers.Remove(source);
        if (this.Disconnected != null)
          this.Disconnected.Invoke(peer);
      }
      else
      {
        CommonDebug.LogWarning("Disconnection from unknown source");
      }
    }

    /// <summary>
    /// Verifies that the source of a packet is on our whitelist, if we are
    /// using one. Note that this is not very secure without encryption, but
    /// is useful for debugging and can filter out accidents.
    /// </summary>
    private bool VerifySource(IPEndPoint source)
    {
      if (this.UseWhiteList == false)
        return true;

      if (this.whiteList.Contains(source.Address))
        return true;

      CommonDebug.LogWarning("Unrecognized connection: " + source.Address);
      return false;
    }

    #region Peer Management
    private NetPeer RegisterPeer(IPEndPoint source)
    {
      NetPeer peer = this.GetPeer(source);
      if (peer == null)
      {
        peer = new NetPeer(source, this);
        this.AddPeer(peer);

        if (this.Connected != null)
          this.Connected.Invoke(peer);
      }

      return peer;
    }

    private void AddPeer(NetPeer peer)
    {
      this.peers.Add(peer.EndPoint, peer);
    }

    private NetPeer GetPeer(IPEndPoint endPoint)
    {
      NetPeer peer = null;
      if (this.peers.TryGetValue(endPoint, out peer))
        return peer;
      return null;
    }
    #endregion

    /// <summary> 
    /// Returns true if OS socket has data available for read. 
    /// </summary>
    private bool CanReceive()
    {
      return this.socket.Poll(0, SelectMode.SelectRead);
    }

    /// <summary> 
    /// Attempts to read from OS socket. Returns false if the read fails
    /// or if there is nothing to read.
    /// </summary>
    private NetPacket TryReceive(out IPEndPoint source)
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
    private bool TrySend(
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

    private void ConfigureSocket()
    {
      this.socket.ReceiveBufferSize = NetConfig.DATA_BUFFER_SIZE;
      this.socket.SendBufferSize = NetConfig.DATA_BUFFER_SIZE;
      this.socket.Blocking = false;

      try
      {
        const uint IOC_IN = 0x80000000;
        const uint IOC_VENDOR = 0x18000000;
        uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
        this.socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
      }
      catch
      {
        CommonDebug.LogWarning(
          "Failed to set control code for ignoring ICMP port unreachable.");
      }
    }

    private List<NetPeer> GetPeerList()
    {
      this.reusablePeerList.Clear();
      return this.reusablePeerList;
    }

    private List<PendingConnection> GetConnectionList()
    {
      this.reusableConnectionList.Clear();
      return this.reusableConnectionList;
    }
    #endregion
  }
}
