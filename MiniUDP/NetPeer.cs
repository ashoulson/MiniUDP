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
  public enum NetPeerStatus
  {
    Connecting,
    Connected,
    Closed,
  }

  public class NetPeer
  {
    /// <summary>
    /// User-configurable data to attach to this peer.
    /// </summary>
    public object UserData { get; set; }

    // Data Receipt
    public event NetPeerDataEvent PayloadReceived;
    public event NetPeerDataEvent MessageReceived;

    // Peer activity
    public event NetPeerConnectEvent PeerConnected;
    public event NetPeerCloseEvent PeerClosed;

    public IPEndPoint EndPoint { get { return this.endPoint; } }
    public NetTraffic Traffic { get { return this.traffic; } }
    public bool RemoteIsClient { get { return this.remoteIsClient; } }
    public string Token { get { return this.token; } }

    public NetPeerStatus Status { get { return this.status; } }
    public bool IsConnected { get { return this.status == NetPeerStatus.Connected; } }
    public bool IsOpen { get { return this.status != NetPeerStatus.Closed; } }
    public bool IsClosed { get { return this.status == NetPeerStatus.Closed; } }

    public int PendingMessages { get { return this.messageOut.Count; } }

    internal IEnumerable<NetMessage> Outgoing { get { return this.messageOut; } }
    internal bool HasMessages { get { return (this.messageOut.Count > 0); } }
    internal ushort MessageAck { get { return this.traffic.MessageAck; } }

    internal bool AckRequested { get; set; }

    private readonly NetCore core;
    private readonly bool remoteIsClient;
    private readonly IPEndPoint endPoint;
    private readonly string token;

    private readonly NetTraffic traffic;
    private readonly Queue<NetMessage> messageOut;

    private NetPeerStatus status;
    private ushort messageSeq;
    private ushort payloadSeq;

    internal NetPeer(
      NetCore core,
      bool remoteIsClient,
      IPEndPoint endPoint,
      string token,
      long creationTime)
    {
      this.AckRequested = false;

      this.core = core;
      this.remoteIsClient = remoteIsClient;
      this.endPoint = endPoint;
      this.token = token;

      this.traffic = new NetTraffic(creationTime);
      this.messageOut = new Queue<NetMessage>();

      if (remoteIsClient) // Clients are created in response to a connection
        this.status = NetPeerStatus.Connected;
      else // Host peers are created in the process of to connecting to them
        this.status = NetPeerStatus.Connecting;
      this.messageSeq = 1;
      this.payloadSeq = 1;
    }

    internal void Update(long curTime)
    {
      NetDebug.Assert(this.IsClosed == false, "this.IsClosed");

      this.traffic.Update(curTime);
    }

    #region I/O and Controls
    /// <summary>
    /// Closes the peer's network connection for a given reason byte.
    /// </summary>
    public void Close(
      byte userReason = NetConfig.DEFAULT_USER_REASON)
    {
      if (this.IsOpen)
        this.core.HandlePeerClosedByUser(this, userReason);
    }

    /// <summary>
    /// Immediately sends an unreliable sequenced payload.
    /// </summary>
    public SocketError SendPayload(byte[] data, ushort length)
    {
      if ((length < 0) || (length > NetConfig.DATA_MAXIMUM))
        throw new ArgumentOutOfRangeException("length");
      if (this.IsConnected == false)
        throw new InvalidOperationException("Peer is not connected");

      return this.core.SendPayload(this, this.payloadSeq++, data, length);
    }

    /// <summary>
    /// Queues a reliable ordered message for delivery.
    /// </summary>
    public void QueueMessage(byte[] buffer, ushort length)
    {
      if ((length < 0) || (length > NetConfig.DATA_MAXIMUM))
        throw new ArgumentOutOfRangeException("length");
      if (this.IsConnected == false)
        throw new InvalidOperationException("Peer is not connected");

      NetMessage message = this.core.CreateMessage(this, buffer, length);
      message.Sequence = this.messageSeq++;
      this.messageOut.Enqueue(message);
    }
    #endregion

    #region I/O Events
    internal void OnPayloadReceived(
      byte[] data, 
      int dataLength)
    {
      this.PayloadReceived?.Invoke(this, data, dataLength);
    }

    internal void OnMessageReceived(
      byte[] data, 
      int dataLength)
    {
      this.MessageReceived?.Invoke(this, data, dataLength);
    }
    #endregion

    #region Utility
    /// <summary>
    /// If we have outgoing messages, returns the sequence of the first.
    /// </summary>
    internal ushort GetFirstSequence()
    {
      if (this.messageOut.Count > 0)
        return this.messageOut.Peek().Sequence;
      return 0;
    }

    /// <summary>
    /// Called when we've received a message.
    /// </summary>
    internal void HandleMessage(NetMessage message)
    {
      NetDebug.Assert(this.status == NetPeerStatus.Connected, "this.status");

      this.MessageReceived?.Invoke(
        this, 
        message.EncodedData, 
        message.EncodedLength);
    }

    internal void HandlePayload(byte[] data, ushort dataLength)
    {
      NetDebug.Assert(this.status == NetPeerStatus.Connected, "this.status");

      this.PayloadReceived?.Invoke(this, data, dataLength);
    }

    /// <summary>
    /// Called on a client when we've received a connect accept packet.
    /// </summary>
    internal void HandleConnected()
    {
      NetDebug.Assert(this.remoteIsClient == false, "this.remoteIsClient");
      NetDebug.Assert(this.status == NetPeerStatus.Connecting, "this.status");

      this.status = NetPeerStatus.Connected;
      this.PeerConnected?.Invoke(this);
    }

    /// <summary>
    /// Called when we've closed a peer for whatever reason.
    /// </summary>
    internal void HandleClosed(
      NetCloseReason reason,
      byte userKickReason = NetConfig.DEFAULT_USER_REASON,
      SocketError error = SocketError.SocketError)
    {
      if (this.IsOpen)
      {
        this.status = NetPeerStatus.Closed;
        this.PeerClosed?.Invoke(this, reason, userKickReason, error);
      }
    }
    #endregion

    #region Statistics
    /// <summary>
    /// Returns the time (in ms) since we last received useful data.
    /// </summary>
    internal long GetTimeSinceRecv(long curTime)
    {
      return this.traffic.GetTimeSinceRecv(curTime);
    }

    /// <summary>
    /// Creates a new ping for a given time and returns the sequence byte.
    /// </summary>
    internal byte CreatePing(long curTime)
    {
      return this.traffic.GeneratePing(curTime);
    }

    /// <summary>
    /// Returns the statistic byte for transmitting packet loss.
    /// </summary>
    internal byte GetLossByte()
    {
      return this.traffic.GenerateLoss();
    }

    /// <summary>
    /// Returns the statistic byte for transmitting packet drop.
    /// </summary>
    internal byte GetDropByte()
    {
      return this.traffic.GenerateDrop();
    }
    #endregion

    #region Metric/Sequence Recording
    /// <summary>
    /// Logs the payload's sequence ID to record payload packet loss.
    /// Returns true if we should accept the payload, false if it's too old.
    /// Does not trigger any notification events.
    /// </summary>
    internal bool RecordPayload(long curTime, ushort sequence)
    {
      return this.traffic.OnReceivePayload(curTime, sequence);
    }

    /// <summary>
    /// Cleans out any messages older than the received carrier ack.
    /// Does not trigger any notification events.
    /// </summary>
    internal void RecordCarrier(
      long curTime,
      ushort messageAck)
    {
      this.traffic.OnReceiveOther(curTime);
      this.DrainOutgoing(messageAck);
    }

    /// <summary>
    /// Records receipt of a message and updates our ack counter. 
    /// Return true iff the message is new.
    /// Does not trigger any notification events.
    /// </summary>
    internal bool RecordMessage(long curTime, ushort messageSeq)
    {
      this.AckRequested = true;
      return this.traffic.OnReceiveMessage(curTime, messageSeq);
    }

    /// <summary>
    /// Processes statistics received from pong packets.
    /// Does not trigger any notification events.
    /// </summary>
    internal void RecordPing(long curTime, byte loss)
    {
      this.traffic.OnReceivePing(curTime, loss);
    }

    /// <summary>
    /// Processes statistics received from pong packets.
    /// Does not trigger any notification events.
    /// </summary>
    internal void RecordPong(long curTime, byte pongSeq, byte drop)
    {
      this.traffic.OnReceivePong(curTime, pongSeq, drop);
    }

    /// <summary>
    /// Records the fact that we've received data.
    /// Does not trigger any notification events.
    /// </summary>
    internal void RecordOther(long curTime)
    {
      this.traffic.OnReceiveOther(curTime);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Drains outgoing messages that have been acked.
    /// </summary>
    private void DrainOutgoing(ushort messageAck)
    {
      while (this.messageOut.Count > 0)
      {
        NetMessage front = this.messageOut.Peek();
        if (NetUtil.UShortSeqDiff(messageAck, front.Sequence) < 0)
          break;
        this.core.RecycleMessage(this.messageOut.Dequeue());
      }
    }
    #endregion
  }
}
