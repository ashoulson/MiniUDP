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

namespace MiniUDP
{
  /// <summary>
  /// Module for connection quality assessment and reliability management.
  /// </summary>
  public class NetQuality
  {
    internal const int LOSS_BITS = 224;
    internal const int PING_HISTORY = 64;

    public float Ping { get { return this.pingWindow.ComputeAverage() ?? -1.0f; } }
    public float LocalLoss { get { return this.GenerateLoss() / (float)NetQuality.LOSS_BITS; } }
    public float LocalDrop { get { return this.GenerateDrop() / (float)NetQuality.LOSS_BITS; } }
    public float RemoteLoss { get { return this.remoteLoss; } }
    public float RemoteDrop { get { return this.remoteDrop; } }

    internal ushort MessageAck { get { return this.messageAck; } }

    private readonly SequenceCounter payloadLoss;
    private readonly SequenceCounter payloadDrop;
    private readonly PingCounter outgoingPing;
    private readonly RingBuffer<int> pingWindow;
    private readonly long creationTime;

    private ushort lastPayloadSeq;
    private ushort messageAck;

    private long lastPacketRecvTime; // Time we last received anything
    private long lastPongRecvTime;

    private float remoteLoss;
    private float remoteDrop;

    public float? ComputePing()
    {
      return this.pingWindow.ComputeAverage();
    }

    internal NetQuality(long creationTime)
    {
      this.payloadLoss = new SequenceCounter(true);
      this.payloadDrop = new SequenceCounter(false);
      this.outgoingPing = new PingCounter();
      this.pingWindow = new RingBuffer<int>(NetConfig.PING_SMOOTHING_WINDOW);
      this.creationTime = creationTime;

      this.lastPayloadSeq = ushort.MaxValue; // "-1"
      this.messageAck = 0;

      this.lastPacketRecvTime = creationTime;
      this.lastPongRecvTime = creationTime;

      this.remoteLoss = 0.0f;
      this.remoteDrop = 0.0f;
    }

    internal void Update(long curTime)
    {
      // If it's been too long, clear out the window since it's inaccurate
      long timeSincePong = curTime - this.lastPongRecvTime;
      if (timeSincePong > (this.pingWindow.Capacity * 1000))
        this.pingWindow.Clear();
    }

    internal long GetTimeSinceRecv(long curTime)
    {
      return curTime - this.lastPacketRecvTime;
    }

    internal byte GeneratePing(long curTime)
    {
      return this.outgoingPing.CreatePing(curTime);
    }

    internal byte GenerateLoss()
    {
      return (byte)(NetQuality.LOSS_BITS - this.payloadLoss.ComputeCount());
    }

    internal byte GenerateDrop()
    {
      return (byte)this.payloadDrop.ComputeCount();
    }

    /// <summary>
    /// Logs the receipt of a message for timing and keepalive.
    /// Returns false iff the message is too old and should be rejected.
    /// </summary>
    internal bool OnReceiveMessage(
      long curTime,
      ushort messageSeq)
    {
      // Reject it if it's too old, including statistics for it
      if (NetUtil.UShortSeqDiff(messageSeq, this.MessageAck) <= 0)
        return false;

      this.messageAck = messageSeq;
      this.lastPacketRecvTime = curTime;
      return true;
    }

    /// <summary>
    /// Logs the receipt of a payload for statistics calculation.
    /// Returns false iff the payload is too old and should be rejected.
    /// </summary>
    internal bool OnReceivePayload(
      long curTime,
      ushort payloadSeq)
    {
      bool isNew = this.IsPayloadNew(payloadSeq);
      this.payloadLoss.Store(payloadSeq);

      if (isNew)
      {
        this.lastPacketRecvTime = curTime;
        this.lastPayloadSeq = payloadSeq;
        this.payloadDrop.Advance(payloadSeq);
      }
      else
      {
        this.payloadDrop.Store(payloadSeq);
      }

      return isNew;
    }

    /// <summary>
    /// Logs the receipt of a carrier packet for statistics calculation.
    /// </summary>
    internal void OnReceiveCarrier(
      long curTime)
    {
      this.lastPacketRecvTime = curTime;
    }

    /// <summary>
    /// Processes the loss value from a received ping.
    /// </summary>
    internal void OnReceivePing(
      long curTime,
      byte loss)
    {
      this.lastPacketRecvTime = curTime;
      this.remoteLoss = loss / (float)NetQuality.LOSS_BITS;
    }

    /// <summary>
    /// Receives a pong and updates connection timings.
    /// </summary>
    internal void OnReceivePong(
      long curTime, 
      byte pongSeq, 
      byte drop)
    {
      // Reject it if it's too old, including statistics for it
      long creationTime = this.outgoingPing.ConsumePong(pongSeq);
      if (creationTime < 0)
        return;
      long diff = curTime - creationTime;
      if (diff < 0)
        return;

      this.lastPacketRecvTime = curTime;
      this.lastPongRecvTime = curTime;
      this.pingWindow.Push((int)diff);
      this.remoteDrop = drop / (float)NetQuality.LOSS_BITS;
    }

    /// <summary>
    /// For all other packet types.
    /// </summary>
    internal void OnReceiveOther(
      long curTime)
    {
      this.lastPacketRecvTime = curTime;
    }

    /// <summary>
    /// Returns true iff a payload sequence is new.
    /// </summary>
    private bool IsPayloadNew(ushort sequence)
    {
      int difference =
        NetUtil.UShortSeqDiff(this.lastPayloadSeq, sequence);
      return (difference < 0);
    }
  }
}
