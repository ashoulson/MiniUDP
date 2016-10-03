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

namespace MiniUDP
{
  /// <summary>
  /// Threadsafe class for writing and sending data via a socket.
  /// </summary>
  internal class NetSender
  {
    private readonly object sendLock;
    private readonly byte[] sendBuffer;
    private readonly NetSocket socket;

    internal NetSender(NetSocket socket)
    {
      this.sendLock = new object();
      this.sendBuffer = new byte[NetConfig.SOCKET_BUFFER_SIZE];
      this.socket = socket;

#if DEBUG
      this.outQueue = new NetLossyQueue();
#endif
    }

    /// <summary>
    /// Sends a request to connect to a remote peer.
    /// </summary>
    internal SocketError SendConnect(
      NetPeer peer,
      string version)
    {
      lock (this.sendLock)
      {
        int length =
          NetEncoding.PackConnectRequest(
            this.sendBuffer,
            version,
            peer.Token);
        return this.TrySend(peer.EndPoint, this.sendBuffer, length);
      }
    }

    /// <summary>
    /// Accepts a remote request and sends an affirmative reply.
    /// </summary>
    internal SocketError SendAccept(
      NetPeer peer)
    {
      lock (this.sendLock)
      {
        int length =
        NetEncoding.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.ConnectAccept,
          0,
          0);
        return this.TrySend(peer.EndPoint, this.sendBuffer, length);
      }
    }

    /// <summary>
    /// Notifies a peer that we are disconnecting. May not arrive.
    /// </summary>
    internal SocketError SendKick(
      NetPeer peer,
      NetKickReason kickReason,
      byte userReason = 0)
    {
      lock (this.sendLock)
      {
        int length =
        NetEncoding.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.Kick,
          (byte)kickReason,
          userReason);
        return this.TrySend(peer.EndPoint, this.sendBuffer, length);
      }
    }

    /// <summary>
    /// Sends a generic ping packet.
    /// </summary>
    internal SocketError SendPing(
      NetPeer peer,
      long curTime)
    {
      lock (this.sendLock)
      {
        int length =
        NetEncoding.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.Ping,
          peer.GeneratePing(curTime),
          peer.GenerateLoss());
        return this.TrySend(peer.EndPoint, this.sendBuffer, length);
      }
    }

    /// <summary>
    /// Sends a generic pong packet.
    /// </summary>
    internal SocketError SendPong(
      NetPeer peer,
      byte pingSeq,
      byte drop)
    {
      lock (this.sendLock)
      {
        int length =
        NetEncoding.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.Pong,
          pingSeq,
          drop);
        return this.TrySend(peer.EndPoint, this.sendBuffer, length);
      }
    }

    /// <summary>
    /// Sends a scheduled carrier message containing ping information
    /// and reliable messages (if any).
    /// </summary>
    internal SocketError SendCarrier(
      NetPeer peer)
    {
      lock (this.sendLock)
      {
        int headerLength =
        NetEncoding.PackCarrierHeader(
          this.sendBuffer,
          peer.NotificationAck,
          peer.GetFirstSequence());
        int packedLength =
          NetEncoding.PackNotifications(
            this.sendBuffer,
            headerLength,
            peer.Outgoing);
        int length = headerLength + packedLength;
        return this.TrySend(peer.EndPoint, this.sendBuffer, length);
      }
    }

    /// <summary>
    /// Notifies a sender that we have rejected their connection request.
    /// </summary>
    internal SocketError SendReject(
      IPEndPoint destination,
      NetRejectReason rejectReason)
    {
      lock (this.sendLock)
      {
        int length =
        NetEncoding.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.ConnectReject,
          (byte)rejectReason,
          0);
        return this.TrySend(destination, this.sendBuffer, length);
      }
    }


    /// <summary>
    /// Immediately sends out a payload to a peer.
    /// </summary>
    internal SocketError SendPayload(
      NetPeer peer,
      ushort sequence,
      byte[] buffer,
      int length)
    {
      lock (this.sendLock)
      {
        int position = 
          NetEncoding.PackPayloadHeader(this.sendBuffer, sequence);
        Array.Copy(buffer, 0, this.sendBuffer, position, length);
        position += length;
        return this.TrySend(peer.EndPoint, this.sendBuffer, position);
      }
    }

    /// <summary>
    /// Sends a packet over the network.
    /// </summary>
    private SocketError TrySend(IPEndPoint endPoint, byte[] buffer, int length)
    {
#if DEBUG
      if (NetConfig.LatencySimulation)
      {
        this.outQueue.Enqueue(endPoint, buffer, length);
        return SocketError.Success;
      }
#endif
      return this.socket.TrySend(endPoint, buffer, length);
    }

    #region Latency Simulation
#if DEBUG
    private readonly NetLossyQueue outQueue;

    internal void Update()
    {
      lock (this.sendLock)
      {
        IPEndPoint endPoint;
        byte[] buffer;
        int length;
        while (this.outQueue.TryDequeue(out endPoint, out buffer, out length))
          this.socket.TrySend(endPoint, buffer, length);
      }
    }
#endif
    #endregion
  }
}
