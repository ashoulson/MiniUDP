using System;
using System.Diagnostics;

namespace MiniNet
{
  public class NetTime
  {
    private static readonly long start = Stopwatch.GetTimestamp();
    private static readonly double frequency = 
      1.0 / (double)Stopwatch.Frequency;

    /// <summary>
    /// Time represented as elapsed seconds.
    /// </summary>
    public static double Time
    {
      get
      {
        long diff = Stopwatch.GetTimestamp() - start;
        return (double)diff * frequency;
      }
    }
  }
}
