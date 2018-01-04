/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2015-2018 - Alexander Shoulson - http://ashoulson.com
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
