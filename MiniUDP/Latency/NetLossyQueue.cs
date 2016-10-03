using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

using MiniUDP.Util;

namespace MiniUDP
{
  internal class NetLossyQueue
  {
    private readonly static Noise PingNoise = new Noise();
    private readonly static Noise LossNoise = new Noise();

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

    public NetLossyQueue()
    {
      this.entries = new Heap<Entry>();
      this.timer = new Stopwatch();
      this.timer.Start();
    }

    public void Enqueue(IPEndPoint destination, byte[] buffer, int length)
    {
      // See if we should drop the packet
      float loss =
        NetLossyQueue.LossNoise.GetValue(
          this.timer.ElapsedMilliseconds,
           NetConfig.LossTurbulence);
      if (loss < NetConfig.LossChance)
        return;

      // See if we should delay the packet
      float latencyRange = 
        NetConfig.MaximumLatency - NetConfig.MinimumLatency;
      float latencyNoise =
        NetLossyQueue.PingNoise.GetValue(
          this.timer.ElapsedMilliseconds,
          NetConfig.LatencyTurbulence);
      int latency = 
        (int)((latencyNoise * latencyRange) + NetConfig.MinimumLatency);

      long releaseTime = this.timer.ElapsedMilliseconds + latency;
      this.entries.Add(new Entry(releaseTime, destination, buffer, length));
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
  }
}
