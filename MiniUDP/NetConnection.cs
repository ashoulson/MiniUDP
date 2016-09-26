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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using CommonUtil;

namespace MiniUDP
{
  public class NetConnection
  {
    private readonly NetSocketIO socket;

    public NetConnection()
    {

    }

























    ///// <summary>
    ///// A remote peer has connected.
    ///// </summary>
    //public event PeerEvent Connected;

    ///// <summary>
    ///// A local peer has been closed and its disconnect message has been sent.
    ///// </summary>
    //public event PeerEvent Closed;

    ///// <summary>
    ///// A remote peer has disconnected.
    ///// </summary>
    //public event PeerEvent Disconnected;

    ///// <summary>
    ///// A remote peer has timed out.
    ///// </summary>
    //public event PeerEvent TimedOut;

    ///// <summary>
    ///// An attempt to connect to a remote peer has timed out.
    ///// </summary>
    //public event ConnectionEvent ConnectFailed;

    //private class PendingConnection
    //{
    //  internal IPEndPoint EndPoint { get { return this.endPoint; } }

    //  private NetTime time;
    //  private readonly IPEndPoint endPoint;
    //  private double lastAttempt;
    //  private long expireTime;

    //  internal PendingConnection(NetTime time, IPEndPoint endPoint)
    //  {
    //    this.time = time;
    //    this.endPoint = endPoint;
    //    this.lastAttempt = double.NegativeInfinity;
    //    this.expireTime =
    //      this.time.Time + NetConfig.CONNECTION_ATTEMPT_TIME_OUT;
    //  }

    //  internal void LogAttempt()
    //  {
    //    this.lastAttempt = this.time.Time;
    //  }

    //  internal bool TimeToRetry()
    //  {
    //    double nextTime = this.lastAttempt + NetConfig.CONNECTION_RETRY_RATE;
    //    return (nextTime < this.time.Time);
    //  }

    //  internal bool HasExpired()
    //  {
    //    return (this.time.Time > this.expireTime);
    //  }
    //}

    //#region Static Methods
    //public static IPEndPoint StringToEndPoint(string address)
    //{
    //  string[] split = address.Split(':');
    //  string stringAddress = split[0];
    //  string stringPort = split[1];

    //  int port = int.Parse(stringPort);
    //  IPAddress ipaddress = IPAddress.Parse(stringAddress);
    //  IPEndPoint endpoint = new IPEndPoint(ipaddress, port);

    //  if (endpoint == null)
    //    throw new ArgumentException("Failed to parse address: " + address);
    //  return endpoint;
    //}

    //public static IPAddress StringToAddress(string address)
    //{
    //  string[] split = address.Split(':');
    //  string stringAddress = split[0];
    //  string stringPort = split[1];

    //  int port = int.Parse(stringPort);
    //  IPAddress ipaddress = IPAddress.Parse(stringAddress);

    //  if (ipaddress == null)
    //    throw new ArgumentException("Failed to parse address: " + address);
    //  return ipaddress;
    //}
    //#endregion

    //#region Properties and Fields
    //public bool UseWhiteList { get; set; }
    //public int PeerCount { get { return this.peers.Count; } }

    //protected readonly Socket socket;

    //public readonly NetTime time;
    //private readonly byte[] dataBuffer;
    //private readonly NetPool<NetPacket> packetPool;
    //private readonly Dictionary<IPEndPoint, NetPeer> peers;
    //private readonly Dictionary<IPEndPoint, PendingConnection> pendingConnections;
    //private readonly HashSet<IPAddress> whiteList;

    //// Pre-allocated lists for iteration tasks
    //private readonly List<NetPeer> reusablePeerList;
    //private readonly List<PendingConnection> reusableConnectionList;
    //#endregion

    //#region Constructors
    //public NetSocket()
    //{
    //  this.time = new NetTime();
    //  this.dataBuffer = new byte[NetConfig.DATA_BUFFER_SIZE];
    //  this.packetPool = new NetPool<NetPacket>();
    //  this.peers = new Dictionary<IPEndPoint, NetPeer>();
    //  this.pendingConnections = new Dictionary<IPEndPoint, PendingConnection>();
    //  this.whiteList = new HashSet<IPAddress>();

    //  this.reusablePeerList = new List<NetPeer>();
    //  this.reusableConnectionList = new List<PendingConnection>();

    //  this.socket =
    //    new Socket(
    //      AddressFamily.InterNetwork,
    //      SocketType.Dgram,
    //      ProtocolType.Udp);
    //  this.ConfigureSocket();
    //}
    //#endregion

    //#region Public Interface
    ///// <summary>
    ///// Adds an address to the whitelist for refusing connections.
    ///// </summary>
    //public void AddToWhiteList(string address)
    //{
    //  this.whiteList.Add(NetSocket.StringToAddress(address));
    //}

    ///// <summary>
    ///// Starts the socket using the supplied endpoint.
    ///// If the port is taken, the given port will be incremented to a free port.
    ///// </summary>
    //public void Bind(int port)
    //{
    //  try
    //  {
    //    this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
    //  }
    //  catch (SocketException exception)
    //  {
    //    if (exception.ErrorCode == 10048)
    //      UtilDebug.LogError("Port " + port + " unavailable!");
    //    else
    //      UtilDebug.LogError(exception.Message);
    //    return;
    //  }
    //}

    ///// <summary>
    ///// Begins the process of connecting to an address given as a string in 
    ///// "IP.IP.IP.IP:PORT" format.
    ///// </summary>
    //public void Connect(string address)
    //{
    //  IPEndPoint endPoint = NetSocket.StringToEndPoint(address);
    //  if (this.peers.ContainsKey(endPoint) == false)
    //    this.pendingConnections.Add(
    //      endPoint,
    //      new PendingConnection(this.time, endPoint));
    //}

    ///// <summary>
    ///// Queues up a disconnect message to a given peer.
    ///// </summary>
    //public void Disconnect(NetPeer peer)
    //{
    //  peer.AddOutgoing(this.AllocatePacket(NetPacketType.Disconnect));
    //}

    ///// <summary>
    ///// Called at the beginning of processing. Reads all incoming data,
    ///// processes it, and assigns packets to the peers it received them from.
    ///// </summary>
    //public void Receive()
    //{
    //  for (int i = 0; i < NetConfig.MAX_PACKET_READS; i++)
    //  {
    //    if (this.CanReceive() == false)
    //      break;

    //    IPEndPoint source;
    //    NetPacket packet = this.TryReceive(out source);

    //    if ((packet != null) && this.PreProcess(packet, source))
    //    {
    //      NetPeer sourcePeer;
    //      if (this.peers.TryGetValue(source, out sourcePeer))
    //        sourcePeer.AddReceived(packet);
    //      else
    //        UtilDebug.LogWarning("Message from unrecognized peer: " + source);
    //    }
    //  }
    //}

    ///// <summary>
    ///// Updates all peers, sending notifications of received messages or 
    ///// timeouts. Timeouts occur after all message notifications.
    ///// </summary>
    //public void Poll()
    //{
    //  this.RetryConnections();

    //  List<NetPeer> timedOutPeers = this.GetPeerList();
    //  foreach (NetPeer peer in this.peers.Values)
    //  {
    //    peer.FlagMessagesReady();
    //    if (peer.IsTimedOut())
    //      timedOutPeers.Add(peer);
    //  }

    //  foreach (NetPeer peer in timedOutPeers)
    //  {
    //    peer.SilentClose();
    //    this.peers.Remove(peer.EndPoint);
    //    if (this.TimedOut != null)
    //      this.TimedOut.Invoke(peer);
    //  }
    //}

    ///// <summary>
    ///// Called at the end of processing, sends any packets that have been
    ///// queued up for transmission to all relevant peers.
    ///// </summary>
    //public void Transmit()
    //{
    //  List<NetPeer> closedPeers = this.GetPeerList();
    //  foreach (NetPeer peer in this.peers.Values)
    //  {
    //    while (peer.Outgoing.Count > 0)
    //    {
    //      NetPacket packet = peer.Outgoing.Dequeue();
    //      this.TrySend(packet, peer.EndPoint);
    //      NetPool.Free(packet);
    //    }

    //    if (peer.Status == NetPeerStatus.Closed)
    //      closedPeers.Add(peer);
    //  }

    //  foreach (NetPeer peer in closedPeers)
    //  {
    //    peer.SilentClose();
    //    this.peers.Remove(peer.EndPoint);
    //    if (this.Closed != null)
    //      this.Closed.Invoke(peer);
    //  }
    //}

    ///// <summary>
    ///// Queues disconnect packets for all peers (but they won't be sent
    ///// until the next Transmit() call).
    ///// </summary>
    //public void Shutdown()
    //{
    //  foreach (NetPeer peer in this.peers.Values)
    //    peer.Close();
    //}
    //#endregion

    //#region Protected Helpers
    ///// <summary>
    ///// Allocates a packet from the pool for use in transmission. Packets
    ///// will be automatically freed after send -- no need to do it manually.
    ///// </summary>
    //internal NetPacket AllocatePacket(NetPacketType packetType)
    //{
    //  NetPacket packet = this.packetPool.Allocate();
    //  packet.Initialize(packetType);
    //  return packet;
    //}

    ///// <summary>
    ///// Polls each pending connection to see if it is time to retry, and
    ///// does so if applicable. Cleans up any expired connection attempts.
    ///// </summary>
    //private void RetryConnections()
    //{
    //  List<PendingConnection> expired = this.GetConnectionList();
    //  foreach (PendingConnection pending in this.pendingConnections.Values)
    //  {
    //    if (pending.TimeToRetry())
    //    {
    //      NetPacket packet = this.AllocatePacket(NetPacketType.ConnectRequest);
    //      this.TrySend(packet, pending.EndPoint);
    //      NetPool.Free(packet);
    //      pending.LogAttempt();
    //    }
    //    else if (pending.HasExpired())
    //    {
    //      expired.Add(pending);
    //    }
    //  }

    //  foreach (PendingConnection pending in expired)
    //  {
    //    this.pendingConnections.Remove(pending.EndPoint);
    //    if (this.ConnectFailed != null)
    //      this.ConnectFailed.Invoke(pending.EndPoint.ToString());
    //  }
    //}

    ///// <summary>
    ///// Pre-processes an incoming packet for protocol-level traffic before
    ///// sending anything to higher levels. Returns true if the packet
    ///// should be escalated to the application level, false if the packet
    ///// should be considered "consumed".
    ///// </summary>
    //private bool PreProcess(NetPacket packet, IPEndPoint source)
    //{
    //  switch (packet.PacketType)
    //  {
    //    case NetPacketType.ConnectRequest:
    //      this.HandleConnection(source, true);
    //      return false;

    //    case NetPacketType.ConnectAccept:
    //      this.HandleConnection(source, false);
    //      return false;

    //    case NetPacketType.Disconnect:
    //      this.HandleDisconnection(source);
    //      return false;

    //    case NetPacketType.Payload:
    //      return true;

    //    default:
    //      UtilDebug.LogWarning("Invalid packet type for server");
    //      return false;
    //  }
    //}

    ///// <summary>
    ///// Handles incoming connection-related packets by creating peers.
    ///// </summary>
    //private void HandleConnection(IPEndPoint source, bool sendResponse)
    //{
    //  if (this.VerifySource(source))
    //  {
    //    NetPeer peer = this.RegisterPeer(source);

    //    if (sendResponse)
    //      peer.AddOutgoing(this.AllocatePacket(NetPacketType.ConnectAccept));

    //    if (this.pendingConnections.ContainsKey(source))
    //      this.pendingConnections.Remove(source);
    //  }
    //}

    ///// <summary>
    ///// Handles incoming disconnection-related packets by removing peers.
    ///// </summary>
    //private void HandleDisconnection(IPEndPoint source)
    //{
    //  NetPeer peer = null;
    //  if (this.peers.TryGetValue(source, out peer))
    //  {
    //    peer.SilentClose();
    //    this.peers.Remove(source);
    //    if (this.Disconnected != null)
    //      this.Disconnected.Invoke(peer);
    //  }
    //  else
    //  {
    //    UtilDebug.LogWarning("Disconnection from unknown source");
    //  }
    //}

    ///// <summary>
    ///// Verifies that the source of a packet is on our whitelist, if we are
    ///// using one. Note that this is not very secure without encryption, but
    ///// is useful for debugging and can filter out accidents.
    ///// </summary>
    //private bool VerifySource(IPEndPoint source)
    //{
    //  if (this.UseWhiteList == false)
    //    return true;

    //  if (this.whiteList.Contains(source.Address))
    //    return true;

    //  UtilDebug.LogWarning("Unrecognized connection: " + source.Address);
    //  return false;
    //}

    //#region Peer Management
    //private NetPeer RegisterPeer(IPEndPoint source)
    //{
    //  NetPeer peer = this.GetPeer(source);
    //  if (peer == null)
    //  {
    //    // Peers aren't made frequently enough to bother pooling them
    //    peer = new NetPeer(this.time, source, this);
    //    this.AddPeer(peer);

    //    if (this.Connected != null)
    //      this.Connected.Invoke(peer);
    //  }

    //  return peer;
    //}

    //private void AddPeer(NetPeer peer)
    //{
    //  this.peers.Add(peer.EndPoint, peer);
    //}

    //private NetPeer GetPeer(IPEndPoint endPoint)
    //{
    //  NetPeer peer = null;
    //  if (this.peers.TryGetValue(endPoint, out peer))
    //    return peer;
    //  return null;
    //}
    //#endregion

    ///// <summary> 
    ///// Returns true if OS socket has data available for read. 
    ///// </summary>
    //private bool CanReceive()
    //{
    //  return this.socket.Poll(0, SelectMode.SelectRead);
    //}

    ///// <summary> 
    ///// Attempts to read from OS socket. Returns false if the read fails
    ///// or if there is nothing to read.
    ///// </summary>
    //private NetPacket TryReceive(out IPEndPoint source)
    //{
    //  source = null;

    //  try
    //  {
    //    EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
    //    int receivedBytes =
    //      this.socket.ReceiveFrom(
    //        this.dataBuffer,
    //        this.dataBuffer.Length,
    //        SocketFlags.None,
    //        ref endPoint);

    //    if (receivedBytes > 0)
    //    {
    //      source = endPoint as IPEndPoint;
    //      NetPacket packet = this.packetPool.Allocate();
    //      if (packet.NetInput(this.dataBuffer, receivedBytes))
    //        return packet;
    //    }

    //    return null;
    //  }
    //  catch
    //  {
    //    return null;
    //  }
    //}

    ///// <summary> 
    ///// Attempts to send data to endpoint via OS socket. 
    ///// Returns false if the send failed.
    ///// </summary>
    //private bool TrySend(
    //  NetPacket packet,
    //  IPEndPoint destination)
    //{
    //  try
    //  {
    //    int bytesToSend = packet.NetOutput(this.dataBuffer);
    //    int bytesSent =
    //      this.socket.SendTo(
    //        this.dataBuffer,
    //        bytesToSend,
    //        SocketFlags.None,
    //        destination);

    //    return (bytesSent == bytesToSend);
    //  }
    //  catch
    //  {
    //    return false;
    //  }
    //}

    //private List<NetPeer> GetPeerList()
    //{
    //  this.reusablePeerList.Clear();
    //  return this.reusablePeerList;
    //}

    //private List<PendingConnection> GetConnectionList()
    //{
    //  this.reusableConnectionList.Clear();
    //  return this.reusableConnectionList;
    //}
    //#endregion
  }
}
