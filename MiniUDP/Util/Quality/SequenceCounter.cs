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
  /// Sliding bit array keeping a history of received sequence numbers.
  /// </summary>
  internal class SequenceCounter
  {
    private readonly int numChunks;
    internal readonly uint[] data;

    private ushort latestSequence;

    public SequenceCounter(bool startFilled = true)
    {
      this.numChunks = NetQuality.LOSS_BITS / 32;
      this.data = new uint[this.numChunks];
      this.latestSequence = 0;

      if (startFilled)
        for (int i = 0; i < this.data.Length; i++)
          this.data[i] = 0xFFFFFFFF;
    }

    public int ComputeCount()
    {
      uint sum = 0;
      for (int i = 0; i < this.numChunks; i++)
        sum += this.HammingWeight(this.data[i]);
      return (int)sum;
    }

    /// <summary>
    /// Logs the sequence in the accumulator.
    /// </summary>
    public void Store(ushort sequence)
    {
      int difference =
        NetUtil.UShortSeqDiff(this.latestSequence, sequence);

      if (difference == 0)
        return;
      if (difference >= NetQuality.LOSS_BITS)
        return;
      if (difference > 0)
      {
        this.SetBit(difference);
        return;
      }

      this.Shift(-difference);
      this.latestSequence = sequence;
      this.data[0] |= 1;
    }

    /// <summary>
    /// Advances to a given sequence without storing anything.
    /// </summary>
    public void Advance(ushort sequence)
    {
      int difference =
        NetUtil.UShortSeqDiff(this.latestSequence, sequence);
      if (difference < 0)
      {
        this.Shift(-difference);
        this.latestSequence = sequence;
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
      if ((index < 0) || (index >= NetQuality.LOSS_BITS))
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
}