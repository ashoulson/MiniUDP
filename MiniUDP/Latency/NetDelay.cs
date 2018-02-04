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

#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

using MiniUDP.Util;

namespace MiniUDP
{
  internal class NetDelay
  {
    private class Entry : IComparable<Entry>
    {
      public long ReleaseTime { get { return this.releaseTime; } }
      public IPEndPoint EndPoint { get { return this.endPoint; } }
      public byte[] Data { get { return this.data; } }

      private readonly long releaseTime;
      private readonly IPEndPoint endPoint;
      private readonly byte[] data;

      public Entry(
        long releaseTime,
        IPEndPoint endPoint,
        byte[] buffer,
        int length)
      {
        this.releaseTime = releaseTime;
        this.endPoint = endPoint;
        this.data = new byte[length];
        Array.Copy(buffer, 0, this.data, 0, length);
      }

      public int CompareTo(Entry other)
      {
        return (int)(this.ReleaseTime - other.releaseTime);
      }
    }

    private class EntryComparer : Comparer<Entry>
    {
      public override int Compare(Entry x, Entry y)
      {
        return (int)(x.ReleaseTime - y.ReleaseTime);
      }
    }

    private readonly Heap<Entry> entries;
    private readonly Stopwatch timer;
    private readonly Noise pingNoise;
    private readonly Noise lossNoise;

    public NetDelay()
    {
      this.entries = new Heap<Entry>(new EntryComparer());
      this.timer = new Stopwatch();
      this.pingNoise = new Noise();
      this.lossNoise = new Noise();
      this.timer.Start();
    }

    public void Enqueue(IPEndPoint endPoint, byte[] buffer, int length)
    {
      // See if we should drop the packet
      float loss =
        this.lossNoise.GetValue(
          this.timer.ElapsedMilliseconds,
           NetConfig.LossTurbulence);
      if (loss < NetConfig.LossChance)
        return;

      // See if we should delay the packet
      float latencyRange = 
        NetConfig.MaximumLatency - NetConfig.MinimumLatency;
      float latencyNoise =
        this.pingNoise.GetValue(
          this.timer.ElapsedMilliseconds,
          NetConfig.LatencyTurbulence);
      int latency = 
        (int)((latencyNoise * latencyRange) + NetConfig.MinimumLatency);

      long releaseTime = this.timer.ElapsedMilliseconds + latency;
      this.entries.Add(new Entry(releaseTime, endPoint, buffer, length));
    }

    public bool TryDequeue(
      out IPEndPoint endPoint, 
      out byte[] buffer, 
      out int length)
    {
      endPoint = null;
      buffer = null;
      length = 0;

      if (this.entries.Count > 0)
      {
        Entry first = this.entries.GetMin();
        if (first.ReleaseTime < this.timer.ElapsedMilliseconds)
        {
          this.entries.ExtractDominating();
          endPoint = first.EndPoint;
          buffer = first.Data;
          length = first.Data.Length;
          return true;
        }
      }
      return false;
    }

    public void Clear()
    {
      this.entries.Clear();
    }
  }
}
#endif
