using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestNetTraffic
  {
    [TestMethod]
    public void TestLossCounter()
    {
      NetTraffic.LossCounter counter;
      
      counter = new NetTraffic.LossCounter();
      Assert.AreEqual(0, counter.ComputeLostAmount());

      counter.LogSequence(1);
      Assert.AreEqual(0, counter.ComputeLostAmount());

      counter.LogSequence(3);
      Assert.AreEqual(1, counter.ComputeLostAmount());

      counter.LogSequence(10000);
      Assert.AreEqual(NetTraffic.LOSS_BITS - 1, counter.ComputeLostAmount());

      counter = new NetTraffic.LossCounter();
      counter.LogSequence(95);
      Assert.AreEqual(94, counter.ComputeLostAmount());
    }

    [TestMethod]
    public void TestPingCounter()
    {
      NetTraffic.PingCounter counter = new NetTraffic.PingCounter();

      long createTime;
      byte pingSeq;

      // Consume empty ping
      createTime = counter.ConsumePong(0);
      Assert.AreEqual(-1, createTime);

      // Create and consume ping after 100ms
      counter.CreatePing(100);
      pingSeq = counter.CurrentPingSeq;
      createTime = counter.ConsumePong(pingSeq);
      Assert.AreEqual(100, createTime);

      // Try consuming again
      createTime = counter.ConsumePong(pingSeq);
      Assert.AreEqual(-1, createTime);

      // Bad data safety
      createTime = counter.ConsumePong(-1);
      Assert.AreEqual(-1, createTime);
      createTime = counter.ConsumePong(1243);
      Assert.AreEqual(-1, createTime);
      createTime = counter.ConsumePong(20);
      Assert.AreEqual(-1, createTime);
    }
  }
}
