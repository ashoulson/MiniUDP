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
      byte[] buffer = new byte[100];

      ushort sequence = 0xFACD;
      int bytesPacked = 
        NetIO.PackPayloadHeader(
          buffer, 
          sequence);

      ushort sequenceRead;
      int bytesRead = 
        NetIO.ReadPayloadHeader(
          buffer, 
          out sequenceRead);

      Assert.AreEqual(0, buffer[bytesPacked]);
      Assert.AreEqual(NetPacketType.Payload, NetIO.GetType(buffer));
      Assert.AreEqual(bytesPacked, bytesRead);
      Assert.AreEqual(sequence, sequenceRead);
    }

    [TestMethod]
    public void TestProtocolHeader()
    {
      byte[] buffer = new byte[100];

      NetPacketType type = NetPacketType.ConnectReject;
      byte firstParam = 0xAF;
      byte secondParam = 0xFA;
      int bytesPacked = 
        NetIO.PackProtocolHeader(
          buffer, 
          type, 
          firstParam, 
          secondParam);

      byte firstParamRead;
      byte secondParamRead;
      int bytesRead = 
        NetIO.ReadProtocolHeader(
          buffer, 
          out firstParamRead, 
          out secondParamRead);

      Assert.AreEqual(0, buffer[bytesPacked]);
      Assert.AreEqual(NetPacketType.ConnectReject, NetIO.GetType(buffer));
      Assert.AreEqual(bytesPacked, bytesRead);
      Assert.AreEqual(firstParam, firstParamRead);
      Assert.AreEqual(secondParam, secondParamRead);
    }

    [TestMethod]
    public void TestCarrierHeader()
    {
      byte[] buffer = new byte[100];

      byte pingSeq = 0xAF;
      byte pongSeq = 0xFA;
      byte loss = 0xDF;
      ushort processing = 0xFACD;
      ushort messageAck = 0xDCAF;
      ushort messageSeq = 0xFCCE;
      int bytesPacked =
        NetIO.PackCarrierHeader(
          buffer,
          pingSeq,
          pongSeq,
          loss,
          processing,
          messageAck,
          messageSeq);

      byte pingSeqRead;
      byte pongSeqRead;
      byte lossRead;
      ushort processingRead;
      ushort messageAckRead;
      ushort messageSeqRead;
      int bytesRead =
        NetIO.ReadCarrierHeader(
          buffer,
          out pingSeqRead,
          out pongSeqRead,
          out lossRead,
          out processingRead,
          out messageAckRead,
          out messageSeqRead);

      Assert.AreEqual(0, buffer[bytesPacked]);
      Assert.AreEqual(NetPacketType.Carrier, NetIO.GetType(buffer));
      Assert.AreEqual(bytesPacked, bytesPacked);
      Assert.AreEqual(pingSeq, pingSeqRead);
      Assert.AreEqual(pongSeq, pongSeqRead);
      Assert.AreEqual(loss, lossRead);
      Assert.AreEqual(processing, processingRead);
      Assert.AreEqual(messageAck, messageAckRead);
      Assert.AreEqual(messageSeq, messageSeqRead);
    }
  }
}
