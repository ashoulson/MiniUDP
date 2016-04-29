using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniUDP
{
  public class NetWindow
  {
    private const int DEFAULT_BITS = 128;

    private readonly int numChunks;
    private readonly int numBits;
    private readonly uint[] data;

    private int latestSequence;

    public NetWindow(int numBits = NetWindow.DEFAULT_BITS)
    {
      if ((numBits <= 0) || ((numBits % 32) != 0))
        throw new ArgumentException("numBits");

      this.numBits = numBits;
      this.numChunks = numBits / 32;
      this.data = new uint[this.numChunks];

      this.latestSequence = 0;
    }

    /// <summary>
    /// Returns true iff the sequence is already contained.
    /// </summary>
    public bool Store(int sequence)
    {
      int difference = this.latestSequence - sequence;

      if (difference == 0)
        return true;
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

      if (count > this.numBits)
      {
        this.ClearChunks();
      }
      else
      {
        for (; count >= 32; count -= 32)
          this.ShiftChunks();
        this.ShiftBits(count);
      }
    }

    /// <summary>
    /// Shifts the entire chunk array by an amount less than 32 bits.
    /// </summary>
    private void ShiftBits(int bits)
    {
      if ((bits < 0) || (bits >= 32))
        throw new ArgumentOutOfRangeException("count");
      if (bits == 0)
        return;

      for (int i = 0; i < this.numChunks - 1; i++)
      {
        ulong combined = this.data[i] | ((ulong)this.data[i + 1] << 32);
        combined >>= bits;
        this.data[i] = (uint)combined;
      }

      this.data[this.numChunks - 1] >>= bits;
    }

    /// <summary>
    /// Quickly shifts the entire array over by one chunk amount.
    /// </summary>
    private void ShiftChunks()
    {
      for (int i = 0; i < this.numChunks - 1; i++)
        this.data[i] = this.data[i + 1];
      this.data[this.numChunks - 1] = 0;
    }

    /// <summary>
    /// Clears all chunk values.
    /// </summary>
    private void ClearChunks()
    {
      for (int i = 0; i < this.numChunks; i++)
        this.data[i] = 0;
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

    public override string ToString()
    {
      StringBuilder builder = new StringBuilder();
      for (int i = this.numChunks - 1; i >= 0; i--)
        builder.Append(Convert.ToString(this.data[i], 2).PadLeft(32, '0'));
      return Regex.Replace(builder.ToString(), ".{8}", "$0 ");
    }
  }
}
