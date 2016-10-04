using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;

using MiniUDP;
using MiniUDP.Util;

namespace Tests
{
  [TestClass]
  public class TestUtil
  {
    [TestMethod]
    public void TestSequenceComparison()
    {
      Assert.IsTrue(NetUtil.ByteSeqDiff(0, 180) > 0);
      Assert.IsTrue(NetUtil.ByteSeqDiff(180, 0) < 0);
      Assert.IsTrue(NetUtil.ByteSeqDiff(1, 0) > 0);
      Assert.IsTrue(NetUtil.ByteSeqDiff(0, 1) < 0);
      Assert.IsTrue(NetUtil.ByteSeqDiff(127, 0) > 0);
      Assert.IsTrue(NetUtil.ByteSeqDiff(128, 0) < 0);
      Assert.IsTrue(NetUtil.ByteSeqDiff(128, 128) == 0);

      Assert.IsTrue(NetUtil.UShortSeqDiff(0, 48000) > 0);
      Assert.IsTrue(NetUtil.UShortSeqDiff(48000, 0) < 0);
      Assert.IsTrue(NetUtil.UShortSeqDiff(1, 0) > 0);
      Assert.IsTrue(NetUtil.UShortSeqDiff(0, 1) < 0);
      Assert.IsTrue(NetUtil.UShortSeqDiff(32767, 0) > 0);
      Assert.IsTrue(NetUtil.UShortSeqDiff(32768, 0) < 0);
      Assert.IsTrue(NetUtil.UShortSeqDiff(32768, 32768) == 0);
    }

    [TestMethod]
    public void TestHeap()
    {
      Heap<int> heap = new Heap<int>();
      heap.Add(6);
      heap.Add(2);
      heap.Add(7);
      heap.Add(1);
      heap.Add(4);
      heap.Add(5);
      heap.Add(3);

      Assert.AreEqual(1, heap.ExtractDominating());
      Assert.AreEqual(2, heap.ExtractDominating());
      Assert.AreEqual(3, heap.ExtractDominating());
      Assert.AreEqual(4, heap.ExtractDominating());
      Assert.AreEqual(5, heap.ExtractDominating());
      Assert.AreEqual(6, heap.ExtractDominating());
      Assert.AreEqual(7, heap.ExtractDominating());
    }
  }
}
