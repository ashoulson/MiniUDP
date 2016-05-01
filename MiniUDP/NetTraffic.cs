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
  internal class NetTraffic
  {
    private class SequenceWindow
    {
      private const int DEFAULT_BITS = 64;

      private readonly int numChunks;
      private readonly int numBits;
      private readonly uint[] data;

      private byte latestSequence;

      public SequenceWindow(int numBits = SequenceWindow.DEFAULT_BITS)
      {
        if ((numBits <= 0) || ((numBits % 32) != 0))
          throw new ArgumentException("numBits");

        this.numBits = numBits;
        this.numChunks = numBits / 32;
        this.data = new uint[this.numChunks];

        this.latestSequence = 0;
      }

      public float FillPercent()
      {
        uint sum = 0;
        for (int i = 0; i < this.numChunks; i++)
          sum += this.HammingWeight(this.data[i]);
        return (float)sum / this.numBits;
      }

      public bool CanStore(byte sequence)
      {
        int difference =
          NetTraffic.SeqDiff(this.latestSequence, sequence);
        return difference < this.numBits;
      }

      /// <summary>
      /// Returns true iff the sequence is already contained.
      /// </summary>
      public bool Store(byte sequence)
      {
        int difference =
          NetTraffic.SeqDiff(this.latestSequence, sequence);

        if (difference == 0)
          return true;
        if (difference >= this.numBits)
          return false;
        if (difference > 0)
          return this.SetBit(difference - 1);

        int offset = -difference;
        this.Shift(offset);

        // Store the current sequence if applicable
        if (offset <= this.numBits)
          this.SetBit(offset - 1);

        this.latestSequence = sequence;
        return false;
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
          ulong dataLow = (sourceNext >= 0) ? this.data[sourceNext] : 0;

          this.data[i] = (uint)((((dataHigh << 32) | dataLow) << bits) >> 32);
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
        if ((index < 0) || (index >= this.numBits))
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

      public override string ToString()
      {
        StringBuilder builder = new StringBuilder();
        for (int i = this.numChunks - 1; i >= 0; i--)
          builder.Append(Convert.ToString(this.data[i], 2).PadLeft(32, '0'));
        return Regex.Replace(builder.ToString(), ".{8}", "$0 ");
      }
    }

    /// <summary>
    /// Computes the average of a float array.
    /// </summary>
    private static float Average(float[] window)
    {
      float sum = 0.0f;
      for (int i = 0; i < window.Length; i++)
        sum += window[i];
      return (sum / window.Length);
    }

    /// <summary>
    /// Compares two sequences with wrap-around arithmetic
    /// </summary>
    private static int SeqDiff(byte a, byte b)
    {
      // Assumes a sequence is 8 bits
      return (int)((a << 24) - (b << 24)) >> 24;
    }

    /// <summary>
    /// Compares two timestamps with wrap-around arithmetic
    /// </summary>
    private static int StampDiff(ushort a, ushort b)
    {
      // Assumes a stamp is 16 bits
      return (int)((a << 16) - (b << 16)) >> 16;
    }

    internal long LastRecvTime { get { return this.lastRecvTime; } }
    internal long TimeSinceRecv { get { return this.time.Time - this.lastRecvTime; } }

    internal float LocalLoss { get { return 1.0f - this.receivedSequences.FillPercent(); } }
    internal float RemoteLoss { get { return this.lastRemoteLoss; } }

    private readonly NetTime time;
    private readonly float[] pingWindow;
    private readonly SequenceWindow receivedSequences;

    private long lastRecvTime;
    private byte lastRecvSequence;
    private ushort lastRecvPing;
    private ushort lastRecvPong;
    private byte nextSequence;

    private int pingIndex;
    private float lastRemoteLoss;

    internal NetTraffic(NetTime time)
    {
      this.time = time;
      this.pingWindow = new float[NetConfig.TRAFFIC_WINDOW_LENGTH];
      this.receivedSequences = new SequenceWindow();

      this.lastRecvTime = -1;
      this.lastRecvSequence = 0;
      this.lastRecvPing = 0;
      this.lastRecvPong = 0;
      this.nextSequence = 1; // Start at 1 because the window starts at 0

      this.pingIndex = 0;
      this.lastRemoteLoss = 0.0f;
    }

    internal void WriteMetadata(NetPacket packet)
    {
      packet.WriteMetadata(
        this.TimeSinceRecv,
        this.nextSequence++,
        this.time.TimeStamp,
        this.lastRecvPing,
        this.LocalLoss);
    }

    #region Reporting
    public int GetPing()
    {
      if (this.TimeSinceRecv > NetConfig.SPIKE_TIME)
        return int.MaxValue;
      return (int)(NetTraffic.Average(this.pingWindow) + 0.5f);
    }

    internal void LogReceived(NetPacket packet)
    {
      this.lastRecvTime = this.time.Time;
      this.receivedSequences.Store(packet.Sequence);

      // Store the remote loss if this is a new packet
      if (NetTraffic.SeqDiff(packet.Sequence, this.lastRecvSequence) > 0)
      {
        this.lastRemoteLoss = packet.Loss;
        this.lastRecvPing = packet.PingStamp;

        this.lastRecvSequence = packet.Sequence;
      }

      // Store the ping if this is a new pong
      if (NetTraffic.StampDiff(packet.PongStamp, this.lastRecvPong) > 0)
      {
        int ping =
          NetTraffic.StampDiff(this.time.TimeStamp, packet.PongStamp) - 
          packet.ProcessTime;
        this.pingWindow[this.pingIndex] = ping;
        this.pingIndex = (this.pingIndex + 1) % this.pingWindow.Length;

        this.lastRecvPong = packet.PongStamp;
      }
    }
    #endregion
  }
}
