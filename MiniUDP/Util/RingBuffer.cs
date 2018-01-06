/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
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

namespace MiniUDP
{
  internal static class RingBufferExtensions
  {
    public static float? ComputeAverage(this RingBuffer<int> buffer)
    {
      int count = buffer.Count;
      if (count == 0)
        return null;

      float sum = 0.0f;
      buffer.ForEach((x) => sum += x);
      return sum / count;
    }

    public static NetReport ComputeAverage(this RingBuffer<NetReport> buffer)
    {
      NetReport total = buffer.ComputeTotal();
      float count = buffer.Count;
      return new NetReport(
        total.CarrierCount / count,
        total.PayloadCount / count,
        total.OtherCount / count,
        total.CarrierTotal / count,
        total.PayloadTotal / count,
        total.OtherTotal / count);
    }

    public static NetReport ComputeTotal(this RingBuffer<NetReport> buffer)
    {
      NetReport total = new NetReport();
      int count = buffer.Count;
      if (count == 0)
        return total;

      buffer.ForEach((x) => total += x);
      return total;
    }
  }

  internal class RingBuffer<T>
  {
    public int Count { get { return this.count; } }
    public int Capacity { get { return this.buffer.Length; } }

    private readonly T[] buffer;
    private int index;
    private int count;

    private bool allowModify;

    public RingBuffer(int length)
    {
      this.buffer = new T[length];
      this.index = -1;
      this.count = 0;
      this.allowModify = true;
    }

    public void Push(T value)
    {
      if (this.allowModify == false)
        throw new InvalidOperationException("Cannot modify while iterating");

      this.IncreaseCount();
      this.index = this.WrapIncrement(this.index);
      this.buffer[this.index] = value;
    }

    public void ForEach(Action<T> action)
    {
      this.allowModify = false;
      int pointer = this.WrapIncrement(this.index);
      for (int i = 0; i < this.count; i++)
      {
        action?.Invoke(this.buffer[pointer]);
        pointer = this.WrapIncrement(pointer);
      }
      this.allowModify = true;
    }

    internal void Reverse(IList<T> storeList)
    {
      NetDebug.Assert(storeList.Count == 0);
      if (this.count == 0)
        return;

      this.allowModify = false;
      int pointer = this.index;
      do
      {
        storeList.Add(this.buffer[pointer]);
        pointer = this.WrapDecrement(pointer);
      } while (pointer != this.index);
      this.allowModify = true;
    }

    public void Clear()
    {
      this.index = 0;
      this.count = 0;
    }

    private int WrapIncrement(int value)
    {
      value++;
      if (value >= this.count)
        value = 0;
      return value;
    }

    private int WrapDecrement(int value)
    {
      value--;
      if (value < 0)
        value = this.count - 1;
      return value;
    }

    private void IncreaseCount()
    {
      this.count++;
      if (this.count > this.buffer.Length)
        this.count = this.buffer.Length;
    }
  }
}
