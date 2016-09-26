using Microsoft.VisualStudio.TestTools.UnitTesting;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestUtil
  {
    [TestMethod]
    public void TestSequence()
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
  }
}
