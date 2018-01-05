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
      NetTraffic.SequenceCounter counter;
      
      counter = new NetTraffic.SequenceCounter();
      Assert.AreEqual(0, NetTraffic.LOSS_BITS - counter.ComputeCount());

      counter.Store(1);
      Assert.AreEqual(0, NetTraffic.LOSS_BITS - counter.ComputeCount());

      counter.Store(3);
      Assert.AreEqual(1, NetTraffic.LOSS_BITS - counter.ComputeCount());

      counter.Store(10000);
      Assert.AreEqual(1, counter.ComputeCount());

      counter = new NetTraffic.SequenceCounter();
      counter.Store(95);
      Assert.AreEqual(94, NetTraffic.LOSS_BITS - counter.ComputeCount());
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
      pingSeq = counter.CreatePing(100);
      createTime = counter.ConsumePong(pingSeq);
      Assert.AreEqual(100, createTime);

      // Try consuming again
      createTime = counter.ConsumePong(pingSeq);
      Assert.AreEqual(-1, createTime);

      // Bad data safety
      createTime = counter.ConsumePong(243);
      Assert.AreEqual(-1, createTime);
      createTime = counter.ConsumePong(20);
      Assert.AreEqual(-1, createTime);
    }
  }
}
