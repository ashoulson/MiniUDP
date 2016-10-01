using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestNetIO
  {
    [TestMethod]
    public void TestPayloadHeader()
    {
      ushort sequence = 0xFACD;
      ushort sequenceClamped = (ushort)(sequence & NetIO.MASK_BIG_SEQUENCE);

      byte[] buffer = new byte[100];
      int bytesPacked = NetIO.PackPayloadHeader(buffer, sequence);

      Assert.AreEqual(sizeof(ushort), bytesPacked);
      Assert.AreEqual(NetPacketType.Payload, NetIO.ReadType(buffer));

      ushort sequenceRead;
      int bytesRead = NetIO.ReadPayloadHeader(buffer, out sequenceRead);

      Assert.AreEqual(bytesPacked, bytesRead);
      Assert.AreEqual(sequenceClamped, sequenceRead);
    }

    [TestMethod]
    public void TestPayloadHeaderSimple()
    {
      ushort sequence = 0xFACD;
      ushort sequenceClamped = (ushort)(sequence & NetIO.MASK_BIG_SEQUENCE);

      byte[] buffer = new byte[100];
      buffer[0] = (byte)NetPacketType.Payload;
      NetIO.PackU16(buffer, 1, sequenceClamped);
      int bytesPacked = 3;

      Assert.AreEqual(3, bytesPacked);
      Assert.AreEqual(NetPacketType.Payload, (NetPacketType)buffer[0]);

      ushort sequenceRead = NetIO.ReadU16(buffer, 1);
      int bytesRead = 3;

      Assert.AreEqual(bytesPacked, bytesRead);
      Assert.AreEqual(sequenceClamped, sequenceRead);
    }
  }
}
