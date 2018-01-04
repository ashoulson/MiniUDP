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
