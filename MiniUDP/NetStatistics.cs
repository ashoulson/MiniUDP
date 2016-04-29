using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniUDP
{
  public class NetStatistics
  {
    private const int HISTORY_SECONDS = 5;

    private static int SecondToIndex(uint second)
    {
      return (int)(second % NetStatistics.HISTORY_SECONDS);
    }

    private readonly NetTime time;
    private readonly int[] packetsReceived;
    private readonly int[] totalPing;

    private uint curSecond;
    private int curPacketsReceived;
    private int curTotalPing;

    public float GetPing()
    {
      int receivedSum = 0;
      float pingSum = 0.0f;

      for (int i = 0; i < NetStatistics.HISTORY_SECONDS; i++)
      {
        receivedSum += this.packetsReceived[i];
        pingSum += this.totalPing[i];
      }

      if (receivedSum <= 0)
        return -1.0f;
      return pingSum / (float)receivedSum;
    }

    public float GetLoss(int expectedPerSecond)
    {
      int totalExpected = expectedPerSecond * NetStatistics.HISTORY_SECONDS;
      int receivedSum = 0;

      for (int i = 0; i < NetStatistics.HISTORY_SECONDS; i++)
        receivedSum += this.packetsReceived[i];

      if (receivedSum <= 0)
        return 1.0f;
      return 1.0f - ((float)receivedSum / (float)totalExpected);
    }

    internal NetStatistics(NetTime time)
    {
      this.time = time;
      this.packetsReceived = new int[NetStatistics.HISTORY_SECONDS];
      this.totalPing = new int[NetStatistics.HISTORY_SECONDS];

      this.curSecond = this.time.Second;
      this.curPacketsReceived = 0;
      this.curTotalPing = 0;
    }

    internal void RecordPacket(NetPacket packet)
    {
      uint second = this.time.Second;
      while (this.curSecond < second)
        this.Advance();

      this.curPacketsReceived++;
      this.curTotalPing += 
        NetTime.StampDifference(this.time.TimeStamp, packet.Pong);
    }

    private void Advance()
    {
      int index = NetStatistics.SecondToIndex(this.curSecond);
      this.packetsReceived[index] = this.curPacketsReceived;
      this.totalPing[index] = this.curTotalPing;
      this.curSecond++;

      this.curPacketsReceived = 0;
      this.curTotalPing = 0;
    }
  }
}
