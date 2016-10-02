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
  /// Module for traffic management and connection quality assessment.
  /// </summary>
  public class NetTraffic
  {
    internal const int LOSS_BITS = 256;
    internal const int PING_HISTORY = 64;

    /// <summary>
    /// Sliding bit array keeping a history of received sequence numbers.
    /// </summary>
    internal class LossCounter
    {
      private readonly int numChunks;
      internal readonly uint[] data;

      private ushort latestSequence;

      public LossCounter()
      {
        this.numChunks = NetTraffic.LOSS_BITS / 32;
        this.data = new uint[this.numChunks];
        this.latestSequence = 0;
        for (int i = 0; i < this.data.Length; i++)
          this.data[i] = 0xFFFFFFFF;
      }

      public int ComputeLostAmount()
      {
        uint sum = 0;
        for (int i = 0; i < this.numChunks; i++)
          sum += this.HammingWeight(this.data[i]);
        return NetTraffic.LOSS_BITS - (int)sum;
      }

      /// <summary>
      /// Logs the sequence in the accumulator.
      /// Returns true if this is a new sequence.
      /// </summary>
      public bool LogSequence(ushort sequence)
      {
        int difference =
          NetUtil.UShortSeqDiff(this.latestSequence, sequence);

        if (difference == 0)
          return false;
        if (difference >= NetTraffic.LOSS_BITS)
          return false;

        if (difference > 0)
        {
          // Don't set the bit retroactively since we'll be 
          // rejecting the payload. It counts as lost in this case.
          //this.SetBit(difference);
          return false;
        }
        else
        {
          this.Shift(-difference);
          this.latestSequence = sequence;
          this.data[0] |= 1;
          return true;
        }
      }

      /// <summary>
      /// Shifts the entire array by a given number of bits.
      /// </summary>
      private void Shift(int count)
      {
        if (count < 0)
          throw new ArgumentOutOfRangeException("count");

        int chunks = count / 32;
        int bits = count % 32;

        int i = this.numChunks - 1;
        int min = chunks;

        for (; i >= min; i--)
        {
          int sourceChunk = i - chunks;
          int sourceNext = i - (chunks + 1);

          ulong dataHigh = this.data[sourceChunk];
          ulong dataLow = 
            (sourceNext >= 0) ? this.data[sourceNext] : 0;
          this.data[i] = 
            (uint)((((dataHigh << 32) | dataLow) << bits) >> 32);
        }

        for (; i >= 0; i--)
        {
          this.data[i] = 0;
        }
      }

      /// <summary>
      /// Returns true iff the value is already contained.
      /// </summary>
      private bool SetBit(int index)
      {
        if ((index < 0) || (index >= NetTraffic.LOSS_BITS))
          throw new ArgumentOutOfRangeException("index");

        int chunkIdx = index / 32;
        int chunkBit = index % 32;

        uint bit = 1U << chunkBit;
        uint chunk = this.data[chunkIdx];

        if ((bit & chunk) != 0)
          return true;

        chunk |= bit;
        this.data[chunkIdx] = chunk;
        return false;
      }

      private uint HammingWeight(uint chunk)
      {
        chunk = chunk - ((chunk >> 1) & 0x55555555);
        chunk = (chunk & 0x33333333) + ((chunk >> 2) & 0x33333333);
        return (((chunk + (chunk >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
      }
    }

    /// <summary>
    /// A sliding history window of outgoing ping information and support
    /// for cross-referencing incoming pongs against that history.
    /// </summary>
    internal class PingCounter
    {
      private readonly long[] pingTimes;
      private readonly byte[] pingSequences;
      private byte currentPingSeq;

      public PingCounter()
      {
        this.pingTimes = new long[NetTraffic.PING_HISTORY];
        this.pingSequences = new byte[NetTraffic.PING_HISTORY];
        for (int i = 0; i < this.pingTimes.Length; i++)
          this.pingTimes[i] = -1;
      }

      /// <summary>
      /// Creates a new outgoing ping. Stores both that ping's sequence
      /// and the time it was created.
      /// </summary>
      public byte CreatePing(long curTime)
      {
        this.currentPingSeq++;
        int index = this.currentPingSeq % NetTraffic.PING_HISTORY;
        this.pingTimes[index] = curTime;
        this.pingSequences[index] = this.currentPingSeq;
        return this.currentPingSeq;
      }

      /// <summary>
      /// Returns the time the ping was created for the given pong.
      /// Checks to make sure the stored slot corresponds to the sequence.
      /// </summary>
      public long ConsumePong(byte pongSeq)
      {
        int index = pongSeq % NetTraffic.PING_HISTORY;
        if (this.pingSequences[index] != pongSeq)
          return -1;

        long pingTime = this.pingTimes[index];
        this.pingTimes[index] = -1;
        return pingTime;
      }
    }

    /// <summary>
    /// Computes the average ping over a window.
    /// </summary>
    private static float PingAverage(int[] window)
    {
      float sum = 0.0f;
      int count = 0;
      for (int i = 0; i < window.Length; i++)
      {
        if (window[i] >= 0)
        {
          sum += window[i];
          count++;
        }
      }

      if (count > 0)
        return (sum / count);
      return -1.0f;
    }

    // May be accessed from main thread
    public float Ping { get; private set; }
    public float LocalLoss { get; private set; }
    public float RemoteLoss { get; private set; }
    public long TimeSinceCreation { get; private set; }
    public long TimeSinceReceive { get; private set; }
    public long TimeSincePayload { get; private set; }
    public long TimeSinceNotification { get; private set; }
    public long TimeSincePong { get; private set; }

    internal ushort NotificationAck { get { return this.notificationAck; } }

    private readonly LossCounter payloadLoss;
    private readonly PingCounter outgoingPing;
    private readonly int[] pingWindow;
    private readonly long creationTime;

    private ushort notificationAck;
    private int pingWindowIndex;

    private byte lastRecvLoss;
    private long lastPacketRecvTime; // Time we last received anything
    private long lastPayloadRecvTime;
    private long lastNotificationRecvTime;
    private long lastPongRecvTime;

    internal NetTraffic(long creationTime)
    {
      this.payloadLoss = new LossCounter();
      this.outgoingPing = new PingCounter();
      this.pingWindow = new int[NetConfig.PING_SMOOTHING_WINDOW];
      this.creationTime = creationTime;

      this.notificationAck = 0;
      this.pingWindowIndex = 0;
      this.lastRecvLoss = 0;

      this.lastPacketRecvTime = creationTime;
      this.lastPayloadRecvTime = creationTime;
      this.lastNotificationRecvTime = creationTime;
      this.lastPongRecvTime = creationTime;

      for (int i = 0; i < this.pingWindow.Length; i++)
        this.pingWindow[i] = -1;
    }

    internal void Update(long curTime)
    {
      this.TimeSinceCreation = curTime - this.creationTime;
      this.TimeSinceReceive = curTime - this.lastPacketRecvTime;
      this.TimeSincePayload = curTime - this.lastPayloadRecvTime;
      this.TimeSinceNotification = curTime - this.lastNotificationRecvTime;
      this.TimeSincePong = curTime - this.lastPongRecvTime;

      if (this.TimeSincePong > (this.pingWindow.Length * 1000))
      {
        for (int i = 0; i < this.pingWindow.Length; i++)
          this.pingWindow[i] = -1;
        this.Ping = -1.0f;
      }
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
      return (byte)(this.payloadLoss.ComputeLostAmount());
    }

    /// <summary>
    /// Processes the loss value from a received ping.
    /// </summary>
    internal void OnReceivePing(long curTime, byte loss)
    {
      this.lastPacketRecvTime = curTime;
      this.lastRecvLoss = loss;

      // Recompute since it may be read on the main thread
      this.RemoteLoss = this.lastRecvLoss / (float)NetTraffic.LOSS_BITS;
    }

    /// <summary>
    /// Receives a pong and updates connection timings.
    /// </summary>
    internal void OnReceivePong(long curTime, byte pongSeq)
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

      this.pingWindow[this.pingWindowIndex] = (int)diff;
      this.pingWindowIndex = 
        (this.pingWindowIndex + 1) % this.pingWindow.Length;

      // Recompute since it may be read on the main thread
      this.Ping = NetTraffic.PingAverage(this.pingWindow);
    }

    /// <summary>
    /// Logs the receipt of a payload for packet loss calculation.
    /// Returns false iff the payload is too old and should be rejected.
    /// </summary>
    internal bool OnReceivePayload(long curTime, ushort payloadSeq)
    {
      // Reject it if it's too old, including statistics for it
      if (this.payloadLoss.LogSequence(payloadSeq) == false)
        return false;

      this.lastPacketRecvTime = curTime;
      this.lastPayloadRecvTime = curTime;

      // Recompute since it may be read on the main thread
      this.LocalLoss = 
        this.payloadLoss.ComputeLostAmount() / (float)NetTraffic.LOSS_BITS;
      return true;
    }

    /// <summary>
    /// Logs the receipt of a notification for timing and keepalive.
    /// Returns false iff the notification is too old and should be rejected.
    /// </summary>
    internal bool OnReceiveNotification(long curTime, ushort notificationSeq)
    {
      // Reject it if it's too old, including statistics for it
      if (NetUtil.UShortSeqDiff(notificationSeq, this.NotificationAck) <= 0)
        return false;

      this.notificationAck = notificationSeq;
      this.lastPacketRecvTime = curTime;
      this.lastNotificationRecvTime = curTime;
      return true;
    }

    /// <summary>
    /// For all other packet types.
    /// </summary>
    internal void OnReceiveOther(long curTime)
    {
      this.lastPacketRecvTime = curTime;
    }
  }
}
