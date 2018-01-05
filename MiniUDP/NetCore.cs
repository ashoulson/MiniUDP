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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MiniUDP
{
  public delegate void NetPeerConnectEvent(
    NetPeer peer);

  public delegate void NetPeerCloseEvent(
    NetPeer peer,
    NetCloseReason closeReason,
    byte userReason,
    SocketError error);

  public delegate void NetPeerDataEvent(
    NetPeer peer, 
    byte[] data, 
    int dataLength);

  public class NetCore
  {
    public event NetPeerConnectEvent PeerConnected;
    public event NetPeerCloseEvent PeerClosed;
    public event NetPeerDataEvent PeerReceivedMessage;
    public event NetPeerDataEvent PeerReceivedPayload;

    private bool IsFull { get { return false; } } // TODO: Keep a count
    private long Time { get { return this.timer.ElapsedMilliseconds; } }

    public NetBandwidth BandwidthOut { get { return this.sender.BandwidthOut; } }
    public NetBandwidth BandwidthIn { get { return this.bandwidthIn; } }

    private readonly NetPool<NetMessage> messagePool;
    private readonly Dictionary<IPEndPoint, NetPeer> peers;
    private readonly Stopwatch timer;
    private readonly NetSocket socket;
    private readonly NetSender sender;
    private readonly NetReceiver receiver;

    private readonly Queue<NetMessage> reusableQueue;
    private readonly List<NetPeer> activePeerList;
    private readonly byte[] reusableBuffer;

    private readonly string version;
    private readonly bool allowConnections;
    private readonly NetBandwidth bandwidthIn;

    private long nextTick;
    private long nextLongTick;
    private bool isShutdown;
    private bool isBound;

    public NetCore(string version, bool allowConnections)
    {
      if (Encoding.UTF8.GetByteCount(version) > NetConfig.MAX_VERSION_BYTES)
        throw new ArgumentException("Version string too long");

      this.messagePool = new NetPool<NetMessage>();
      this.peers = new Dictionary<IPEndPoint, NetPeer>();
      this.timer = new Stopwatch();
      this.socket = new NetSocket();

      this.reusableQueue = new Queue<NetMessage>();
      this.activePeerList = new List<NetPeer>();
      this.reusableBuffer = new byte[NetConfig.SOCKET_BUFFER_SIZE];

      this.version = version;
      this.allowConnections = allowConnections;

      this.nextTick = 0;
      this.nextLongTick = 0;
      this.isShutdown = false;
      this.isBound = false;

      // Start the timer and get ready
      this.timer.Start();

      // Create the sender and receiver once we have time
      this.sender = new NetSender(this.socket, this.Time);
      this.receiver = new NetReceiver(this.socket);
      this.bandwidthIn =
        new NetBandwidth(NetConfig.BANDWIDTH_HISTORY, this.Time);
    }

    #region Session Control
    /// <summary>
    /// Optionally binds our socket to receive incoming connections.
    /// </summary>
    public void Bind(int port)
    {
      if (this.isShutdown)
        throw new InvalidOperationException("NetCore has been shut down");
      if (this.isBound)
        throw new InvalidOperationException("NetCore has already been bound");

      this.socket.Bind(port);
      this.isBound = true;
    }

    /// <summary>
    /// Begins establishing a connection to a remote host.
    /// Returns the peer representing this pending connection.
    /// </summary>
    public NetPeer Connect(IPEndPoint endPoint, string token)
    {
      if (this.isShutdown)
        throw new InvalidOperationException("NetCore has been shut down");
      if (this.peers.ContainsKey(endPoint))
        throw new InvalidOperationException("Connecting to existing peer");
      if (token == null)
        throw new ArgumentNullException("Token string is null");
      if (Encoding.UTF8.GetByteCount(token) > NetConfig.MAX_TOKEN_BYTES)
        throw new ArgumentException("Token string too long");

      NetPeer pending = new NetPeer(this, false, endPoint, token, this.Time);
      this.peers.Add(pending.EndPoint, pending);
      return pending;
    }

    /// <summary>
    /// Shuts down the network, disconnects all peers, and cleans up.
    /// The network cannot be used or restarted again after this.
    /// </summary>
    public void Shutdown()
    {
      this.ShutdownPeers();
      this.sender.Flush();
      this.socket.Close();

      this.isShutdown = true;
      this.Cleanup();
    }

    /// <summary>
    /// Primary update logic. Iterates through and manages all peers.
    /// </summary>
    public void Update()
    {
      this.receiver.Update(this.Time);
      this.ReadPackets();
      this.bandwidthIn.Update(this.Time);

      bool longTick;
      if (this.TickAvailable(out longTick))
      {
        this.activePeerList.Clear();
        this.activePeerList.AddRange(this.peers.Values);

        foreach (NetPeer peer in this.activePeerList)
        {
          peer.Update(this.Time);
          switch (peer.Status)
          {
            case NetPeerStatus.Connecting:
              this.UpdateConnecting(peer);
              break;

            case NetPeerStatus.Connected:
              this.UpdateConnected(peer, longTick);
              break;

            case NetPeerStatus.Closed:
              // Peer is closed, do not update
              break;

            default:
              NetDebug.LogError("Invalid peer state");
              break;
          }
        }

        this.activePeerList.Clear();
      }

      this.sender.Update(this.Time);
    }
    #endregion

    #region Socket I/O
    /// <summary>
    /// Polls the socket and receives all pending packet data.
    /// </summary>
    private void ReadPackets()
    {
      for (int i = 0; i < NetConfig.MaxPacketReads; i++)
      {
        IPEndPoint source;
        byte[] buffer;
        int length;
        SocketError result =
          this.receiver.TryReceive(out source, out buffer, out length);
        if (NetSocket.Succeeded(result) == false)
          return;

        NetPacketType type = NetEncoding.GetType(buffer);
        if (type == NetPacketType.Connect)
        {
          // We don't have a peer yet -- special case
          this.HandleConnectRequest(source, buffer, length);
        }
        else
        {
          NetPeer peer;
          if (this.peers.TryGetValue(source, out peer))
          {
            switch (type)
            {
              case NetPacketType.Accept:
                this.bandwidthIn.AddOther(length);
                this.HandleConnectAccept(peer, buffer, length);
                break;

              case NetPacketType.Kick:
                this.bandwidthIn.AddOther(length);
                this.HandleKick(peer, buffer, length);
                break;

              case NetPacketType.Ping:
                this.bandwidthIn.AddOther(length);
                this.HandlePing(peer, buffer, length);
                break;

              case NetPacketType.Pong:
                this.bandwidthIn.AddOther(length);
                this.HandlePong(peer, buffer, length);
                break;

              case NetPacketType.Carrier:
                this.bandwidthIn.AddCarrier(length);
                this.HandleCarrier(peer, buffer, length);
                break;

              case NetPacketType.Payload:
                this.bandwidthIn.AddPayload(length);
                this.HandlePayload(peer, buffer, length);
                break;
            }
          }
        }
      }
    }

    /// <summary>
    /// Handles an incoming payload.
    /// </summary>
    private void HandlePayload(
      NetPeer peer,
      byte[] buffer,
      int packetLength)
    {
      if (peer.IsConnected == false)
        return;

      // Read the payload
      ushort payloadSeq;
      ushort dataLength;
      bool success =
        NetEncoding.ReadPayload(
          peer,
          buffer,
          packetLength,
          this.reusableBuffer,
          out dataLength,
          out payloadSeq);

      // Validate
      if (success == false)
      {
        NetDebug.LogNotify("Can't read payload from " + peer.EndPoint);
        return;
      }

      // Send out the payload received event if the peer accepts it
      if (peer.RecordPayload(this.Time, payloadSeq, packetLength))
      {
        peer.HandlePayload(this.reusableBuffer, dataLength);
        this.PeerReceivedPayload?.Invoke(
          peer,
          this.reusableBuffer,
          dataLength);
      }
    }

    /// <summary>
    /// Handles an incoming carrier packet containing message info.
    /// </summary>
    private void HandleCarrier(
      NetPeer peer,
      byte[] buffer,
      int packetLength)
    {
      if (peer.IsConnected == false)
        return;

      // Read the carrier and messages
      ushort messageAck;
      ushort messageSeq;
      this.reusableQueue.Clear();
      bool success =
        NetEncoding.ReadCarrier(
          this.CreateMessage,
          peer,
          buffer,
          packetLength,
          out messageAck,
          out messageSeq,
          this.reusableQueue);

      // Validate
      if (success == false)
      {
        NetDebug.LogNotify("Can't read carrier from " + peer.EndPoint);
        return;
      }

      long curTime = this.Time;
      peer.RecordCarrier(curTime, messageAck, packetLength);

      // The packet contains the first sequence number. All subsequent
      // messages have sequence numbers in order, so we just increment.
      foreach (NetMessage message in this.reusableQueue)
      {
        if (peer.RecordMessage(curTime, messageSeq++))
        {
          peer.HandleMessage(message);
          this.PeerReceivedMessage?.Invoke(
            peer, 
            message.EncodedData, 
            message.EncodedLength);
        }
        this.RecycleMessage(message);
      }
    }

    /// <summary>
    /// Handles an incoming connection request packet from a remote peer.
    /// </summary>
    private void HandleConnectRequest(
      IPEndPoint source,
      byte[] buffer,
      int packetLength)
    {
      string version;
      string token;
      bool success =
        NetEncoding.ReadConnectRequest(
          buffer,
          out version,
          out token);

      // Validate
      if (success == false)
      {
        NetDebug.LogNotify("Can't read connect from " + source);
        return;
      }

      if (this.ShouldCreatePeer(source, version))
      {
        // Create, add, and accept the new peer as a client
        NetPeer peer = new NetPeer(this, true, source, token, this.Time);
        peer.RecordOther(this.Time, packetLength);
        this.peers.Add(source, peer);
        this.sender.SendAccept(peer);

        // Send out appropriate events
        this.PeerConnected?.Invoke(peer);
      }
    }

    /// <summary>
    /// Handles an incoming connection accept packet.
    /// </summary>
    private void HandleConnectAccept(
      NetPeer peer,
      byte[] buffer,
      int packetLength)
    {
      if (peer.RemoteIsClient)
      {
        NetDebug.LogNotify("Ignored connect accept from " + peer.EndPoint);
        return;
      }

      if (peer.IsConnected)
        return;

      // Send out appropriate events
      peer.RecordOther(this.Time, packetLength);
      peer.HandleConnected();
      this.PeerConnected?.Invoke(peer);
    }

    /// <summary>
    /// Handles an incoming remote kick packet.
    /// </summary>
    private void HandleKick(
      NetPeer peer,
      byte[] buffer,
      int packetLength)
    {
      if (peer.IsClosed)
        return;

      byte rawReason;
      byte userReason;
      bool success =
        NetEncoding.ReadProtocol(
          buffer,
          packetLength,
          out rawReason,
          out userReason);

      // Validate
      if (success == false)
      {
        NetDebug.LogNotify("Can't read kick from " + peer.EndPoint);
        return;
      }

      NetCloseReason closeReason = (NetCloseReason)rawReason;
      // Skip the packet if it's a bad reason (this will cause error output)
      if (NetUtil.ValidateKickReason(closeReason) == NetCloseReason.INVALID)
        return;

      peer.RecordOther(this.Time, packetLength);
      this.peers.Remove(peer.EndPoint);
      peer.HandleClosed(closeReason, userReason);
      this.PeerClosed?.Invoke(
        peer, 
        closeReason, 
        userReason, 
        SocketError.SocketError);
    }

    /// <summary>
    /// Handles an incoming ping packet.
    /// </summary>
    private void HandlePing(
      NetPeer peer,
      byte[] buffer,
      int packetLength)
    {
      if (peer.IsConnected == false)
        return;

      byte pingSeq;
      byte loss;
      bool success =
        NetEncoding.ReadProtocol(
          buffer,
          packetLength,
          out pingSeq,
          out loss);

      // Validate
      if (success == false)
      {
        NetDebug.LogNotify("Can't read ping from " + peer.EndPoint);
        return;
      }

      peer.RecordPing(this.Time, loss, packetLength);
      this.sender.SendPong(peer, pingSeq, peer.GetDropByte());
    }

    /// <summary>
    /// Handles an incoming pong packet.
    /// </summary>
    private void HandlePong(
      NetPeer peer,
      byte[] buffer,
      int packetLength)
    {
      if (peer.IsConnected == false)
        return;

      byte pongSeq;
      byte drop;
      bool success =
        NetEncoding.ReadProtocol(
          buffer,
          packetLength,
          out pongSeq,
          out drop);

      // Validate
      if (success == false)
      {
        NetDebug.LogNotify("Can't read pong from " + peer.EndPoint);
        return;
      }

      peer.RecordPong(this.Time, pongSeq, drop, packetLength);
    }
    #endregion

    #region Event Allocation and Deallocation
    /// <summary>
    /// Creates an empty message.
    /// </summary>
    private NetMessage CreateMessage(
      NetPeer target)
    {
      NetMessage message = this.messagePool.Allocate();
      message.Initialize(target);
      return message;
    }

    /// <summary>
    /// Creates a message with the given data.
    /// </summary>
    internal NetMessage CreateMessage(
      NetPeer target,
      byte[] buffer,
      ushort length)
    {
      NetMessage message = this.CreateMessage(target);
      if (message.ReadData(buffer, 0, length) == false)
        throw new OverflowException("Data too long for message");
      return message;
    }

    /// <summary>
    /// Deallocates a created message.
    /// </summary>
    internal void RecycleMessage(NetMessage message)
    {
      this.messagePool.Deallocate(message);
    }
    #endregion

    #region Data Sending
    /// <summary>
    /// Immediately sends out a payload packet to a peer.
    /// </summary>
    internal SocketError SendPayload(
      NetPeer peer,
      ushort sequence,
      byte[] data,
      ushort length)
    {
      return this.sender.SendPayload(peer, sequence, data, length);
    }
    #endregion

    #region Peer Management
    internal void HandlePeerClosedByUser(NetPeer peer, byte userReason)
    {
      if (userReason != NetConfig.DONT_NOTIFY_PEER)
        this.sender.SendKick(peer, NetCloseReason.KickUserReason, userReason);

      this.peers.Remove(peer.EndPoint);
      peer.HandleClosed(
        NetCloseReason.KickUserReason,
        userReason,
        SocketError.SocketError);
      this.PeerClosed?.Invoke(
        peer,
        NetCloseReason.KickUserReason,
        userReason,
        SocketError.SocketError);
    }

    /// <summary>
    /// Updates a peer with an active connection.
    /// </summary>
    private void UpdateConnected(NetPeer peer, bool longTick)
    {
      if (this.PeerTimeout(peer, true))
        return;

      long time = this.Time;
      if (peer.HasMessages || peer.AckRequested)
      {
        this.sender.SendMessages(peer);
        peer.AckRequested = false;
      }
      if (longTick)
      {
        this.sender.SendPing(peer, this.Time);
      }
    }

    /// <summary>
    /// Updates a peer that is attempting to connect.
    /// </summary>
    private void UpdateConnecting(NetPeer peer)
    {
      if (this.PeerTimeout(peer, false))
        return;

      this.sender.SendConnect(peer, this.version);
    }

    /// <summary>
    /// Checks if a peer has timed out. If so, closes that peer and optionally
    /// sends out a corresponding kick packet.
    /// </summary>
    private bool PeerTimeout(NetPeer peer, bool sendKick)
    {
      NetDebug.Assert(peer.IsClosed == false, "peer.IsClosed");

      if (peer.GetTimeSinceRecv(this.Time) <= NetConfig.ConnectionTimeOut)
        return false;

      if (sendKick)
        this.sender.SendKick(peer, NetCloseReason.KickTimeout);

      this.peers.Remove(peer.EndPoint);
      peer.HandleClosed(NetCloseReason.LocalTimeout);
      this.PeerClosed?.Invoke(
        peer,
        NetCloseReason.LocalTimeout,
        NetConfig.DEFAULT_USER_REASON,
        SocketError.SocketError);
      return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Closes all peers and removes them from the dictionary.
    /// </summary>
    private void ShutdownPeers()
    {
      foreach (NetPeer peer in this.peers.Values)
      {
        if (peer.IsOpen)
          this.sender.SendKick(peer, NetCloseReason.KickShutdown);
        SocketError error = SocketError.SocketError;
        peer.HandleClosed(NetCloseReason.LocalShutdown, 0, error);
      }
      this.peers.Clear();
    }

    /// <summary>
    /// Cleans up all residual data.
    /// </summary>
    private void Cleanup()
    {
      this.timer.Reset();
      this.reusableQueue.Clear();
    }

    /// <summary>
    /// Returns true iff it's time for a tick, or a long tick.
    /// </summary>
    private bool TickAvailable(out bool longTick)
    {
      longTick = false;
      long currentTime = this.Time;
      if (currentTime >= this.nextTick)
      {
        this.nextTick = currentTime + NetConfig.ShortTickRate;
        if (currentTime >= this.nextLongTick)
        {
          longTick = true;
          this.nextLongTick = currentTime + NetConfig.LongTickRate;
        }
        return true;
      }
      return false;
    }

    /// <summary>
    /// Whether or not we should accept a connection before consulting
    /// the application for the final verification step.
    /// </summary>
    private bool ShouldCreatePeer(
      IPEndPoint source,
      string version)
    {
      NetPeer peer;
      if (this.peers.TryGetValue(source, out peer))
      {
        this.sender.SendAccept(peer);
        return false;
      }

      if (this.allowConnections == false)
      {
        this.sender.SendReject(source, NetCloseReason.RejectNotHost);
        return false;
      }

      if (this.IsFull)
      {
        this.sender.SendReject(source, NetCloseReason.RejectFull);
        return false;
      }

      if (this.version != version)
      {
        this.sender.SendReject(source, NetCloseReason.RejectVersion);
        return false;
      }

      return true;
    }
    #endregion
  }
}