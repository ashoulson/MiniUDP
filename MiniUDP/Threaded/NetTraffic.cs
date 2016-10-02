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
using System.Text;
using System.Text.RegularExpressions;

namespace MiniUDP
{
    /// <summary>
  /// Module for traffic management and connection quality assessment.
  /// </summary>
  public class NetTraffic
  {
    internal const int LOSS_BITS = 256;
    internal const int PING_HISTORY = 64;
    internal const int PING_WINDOW = 16;

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
      public byte CurrentPingSeq
      {
        get { return (byte)this.currentPingSeq; }
      }

      private readonly long[] timeHistory;
      private readonly byte[] timeSequence;
      private byte currentPingSeq;

      public PingCounter()
      {
        this.timeHistory = new long[NetTraffic.PING_HISTORY];
        this.timeSequence = new byte[NetTraffic.PING_HISTORY];
        for (int i = 0; i < this.timeHistory.Length; i++)
          this.timeHistory[i] = -1;
      }

      /// <summary>
      /// Creates a new outgoing ping. Stores both that ping's sequence
      /// and the time it was created.
      /// </summary>
      public void CreatePing(long curTime)
      {
        this.currentPingSeq++;
        int index = this.currentPingSeq % NetTraffic.PING_HISTORY;
        this.timeHistory[index] = curTime;
        this.timeSequence[index] = this.currentPingSeq;
      }

      /// <summary>
      /// Returns the time the ping was created for the given pong.
      /// Checks to make sure the stored slot corresponds to the sequence.
      /// </summary>
      public long ConsumePong(int pongSeq)
      {
        int index = pongSeq % NetTraffic.PING_HISTORY;
        if (this.timeSequence[index] != pongSeq)
          return -1;

        long pingTime = this.timeHistory[index];
        this.timeHistory[index] = -1;
        return pingTime;
      }
    }

    /// <summary>
    /// Computes the average of a float array.
    /// </summary>
    private static float Average(int[] window)
    {
      float sum = 0.0f;
      for (int i = 0; i < window.Length; i++)
        sum += window[i];
      return (sum / window.Length);
    }

    // May be accessed from main thread
    public float Ping { get; private set; }
    public float LocalLoss { get; private set; }
    public float RemoteLoss { get; private set; }

    internal byte PingSeq { get { return this.outgoingPing.CurrentPingSeq; } }

    private readonly LossCounter payloadLoss;
    private readonly PingCounter outgoingPing;
    private readonly int[] pingWindow;

    private int pingWindowIndex;
    private byte lastPingRecvSeq;
    private long lastPingRecvTime;
    private byte lastRecvLoss;
    private long lastRecvTime; // Time we last received anything

    internal NetTraffic()
    {
      this.payloadLoss = new LossCounter();
      this.outgoingPing = new PingCounter();
      this.pingWindow = new int[NetTraffic.PING_WINDOW];

      this.pingWindowIndex = 0;
      this.lastPingRecvSeq = 0;
      this.lastPingRecvTime = 0;
      this.lastRecvLoss = 0;
      this.lastRecvTime = 0;
    }

    internal long GetTimeSinceRecv(long curTime)
    {
      return curTime - this.lastRecvTime;
    }

    internal void AdvancePing(long curTime)
    {
      this.outgoingPing.CreatePing(curTime);
    }

    internal void GeneratePing(out byte pingSeq)
    {
      pingSeq = this.outgoingPing.CurrentPingSeq;
    }

    internal void GeneratePong(
      long curTime,
      out byte pongSeq,
      out ushort processTime)
    {
      pongSeq = this.lastPingRecvSeq;
      long timeDiff = curTime - this.lastPingRecvTime;
      if (timeDiff < 0)
        timeDiff = 0;
      if (timeDiff > ushort.MaxValue)
        timeDiff = ushort.MaxValue;
      processTime = (ushort)timeDiff;
    }

    internal void GenerateLoss(out byte loss)
    {
      loss = (byte)(this.payloadLoss.ComputeLostAmount());
    }

    internal void ReceivePacket(long curTime)
    {
      this.lastRecvTime = curTime;
    }

    /// <summary>
    /// Logs the arrival time of a ping sequence, if it's new.
    /// </summary>
    internal void ReceivePing(long curTime, byte pingSeq)
    {
      if (NetUtil.ByteSeqDiff(pingSeq, this.lastPingRecvSeq) > 0)
      {
        this.lastPingRecvTime = curTime;
        this.lastPingRecvSeq = pingSeq;
      }
    }

    internal void ReceivePong(long curTime, byte pongSeq, ushort processTime)
    {
      long creationTime = this.outgoingPing.ConsumePong(pongSeq);
      if (creationTime < 0)
        return;
      long diff = (curTime - creationTime) - processTime;
      if (diff < 0)
        return;

      this.pingWindow[this.pingWindowIndex] = (int)diff;
      this.pingWindowIndex = 
        (this.pingWindowIndex + 1) % NetTraffic.PING_WINDOW;

      // Precompute public since it may be read on the main thread
      this.Ping = NetTraffic.Average(this.pingWindow);
    }

    internal void ReceiveLoss(byte loss)
    {
      this.lastRecvLoss = loss;

      // Precompute public since it may be read on the main thread
      this.RemoteLoss = 
        this.lastRecvLoss / (float)NetTraffic.LOSS_BITS;
    }

    internal bool ReceivePayloadSequence(ushort sequence)
    {
      if (this.payloadLoss.LogSequence(sequence) == false)
        return false;

      // Precompute public since it may be read on the main thread
      this.LocalLoss = 
        this.payloadLoss.ComputeLostAmount() / (float)NetTraffic.LOSS_BITS;
      return true;
    }
  }
}
