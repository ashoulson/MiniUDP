using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace MiniUDP
{
  internal class NetSender
  {
    private readonly byte[] sendBuffer;
    private readonly INetSocketWriter writer;

    internal NetSender(INetSocketWriter writer)
    {
      this.sendBuffer = new byte[NetConfig.SOCKET_BUFFER_SIZE];
      this.writer = writer;
    }

    /// <summary>
    /// Sends a request to connect to a remote peer.
    /// </summary>
    internal SocketError SendConnect(
      NetPeer peer,
      string version)
    {
      NetDebug.LogMessage("Sending connect");
      int length =
        NetIO.PackConnectRequest(
          this.sendBuffer,
          version,
          peer.Token);
      return this.writer.TrySend(peer.EndPoint, this.sendBuffer, length);
    }

    /// <summary>
    /// Accepts a remote request and sends an affirmative reply.
    /// </summary>
    internal SocketError SendAccept(
      NetPeer peer)
    {
      NetDebug.LogMessage("Sending accept");
      int length =
        NetIO.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.ConnectAccept,
          0,
          0);
      return this.writer.TrySend(peer.EndPoint, this.sendBuffer, length);
    }

    /// <summary>
    /// Notifies a peer that we are disconnecting. May not arrive.
    /// </summary>
    internal SocketError SendKick(
      NetPeer peer,
      NetKickReason kickReason,
      byte userReason = 0)
    {
      NetDebug.LogMessage("Sending kick: " + kickReason);
      int length =
        NetIO.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.Kick,
          (byte)kickReason,
          userReason);
      return this.writer.TrySend(peer.EndPoint, this.sendBuffer, length);
    }

    /// <summary>
    /// Sends a scheduled carrier message containing ping information
    /// and reliable messages (if any).
    /// </summary>
    internal SocketError SendCarrier(
      NetPeer peer)
    {
      NetDebug.LogMessage("Sending carrier - Queue: " + peer.Outgoing.Count());

      int headerLength =
        NetIO.PackCarrierHeader(
          this.sendBuffer,
          0,
          0,
          0,
          0,
          peer.NotifyAck,
          peer.GetFirstSequence());
      int packedLength =
        NetIO.PackNotifications(
          this.sendBuffer,
          headerLength,
          peer.Outgoing);
      int length = headerLength + packedLength;

      return this.writer.TrySend(peer.EndPoint, this.sendBuffer, length);
    }

    /// <summary>
    /// Notifies a sender that we have rejected their connection request.
    /// </summary>
    internal SocketError SendReject(
      IPEndPoint destination,
      NetRejectReason rejectReason)
    {
      NetDebug.LogMessage("Sending reject: " + rejectReason);
      int length =
        NetIO.PackProtocolHeader(
          this.sendBuffer,
          NetPacketType.ConnectReject,
          (byte)rejectReason,
          0);
      return this.writer.TrySend(destination, this.sendBuffer, length);
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
      int position = NetIO.PackPayloadHeader(this.sendBuffer, sequence);
      Array.Copy(buffer, 0, this.sendBuffer, position, length);
      position += length;
      return this.writer.TrySend(peer.EndPoint, this.sendBuffer, position);
    }
  }
}
