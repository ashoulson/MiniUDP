using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestNetEncoding
  {
    private static NetEvent CreateEvent(NetEventType type, NetPeer peer)
    {
      NetEvent evnt = new NetEvent();
      evnt.Initialize(type, peer);
      return evnt;
    }

    private static NetEvent CreateStringEvent(string value)
    {
      byte[] bytes = Encoding.UTF8.GetBytes(value);
      NetEvent evnt = new NetEvent();
      evnt.Initialize(NetEventType.Notification, null);
      evnt.ReadData(bytes, 0, (ushort)bytes.Length);
      return evnt;
    }

    private static NetEvent CreateFilled(int length)
    {
      byte[] filled = new byte[2048];
      for (int i = 0; i < filled.Length; i++)
        filled[i] = (byte)(i % 256);

      NetEvent evnt = new NetEvent();
      evnt.Initialize(NetEventType.Notification, null);
      evnt.ReadData(filled, 0, (ushort)length);
      return evnt;
    }

    private static void TestNotificationCase(
      int expectedSize, 
      int expectedCount, 
      params NetEvent[] evnts)
    {
      byte[] buffer = new byte[2048];
      ushort notifyAckIn = 23;
      ushort notifySeqIn = 6533;
      ushort notifyAckOut;
      ushort notifySeqOut;

      Queue<NetEvent> srcQueue = new Queue<NetEvent>(evnts);
      Queue<NetEvent> dstQueue = new Queue<NetEvent>();

      int packedSize = NetEncoding.PackCarrier(buffer, notifyAckIn, notifySeqIn, srcQueue);
      bool result = NetEncoding.ReadCarrier(CreateEvent, null, buffer, packedSize, out notifyAckOut, out notifySeqOut, dstQueue);

      Assert.AreEqual(true, result);
      Assert.AreEqual(notifyAckIn, notifyAckOut);
      Assert.AreEqual(notifySeqIn, notifySeqOut);
      Assert.AreEqual(expectedCount, dstQueue.Count);
      Assert.AreEqual(expectedSize, packedSize - NetEncoding.CARRIER_HEADER_SIZE);
    }

    [TestMethod]
    public void TestNotificationPack()
    {
      int headerSize = NetEncoding.NOTIFICATION_HEADER_SIZE;
      int maxPack = NetConfig.MAX_NOTIFICATION_PACK;
      int halfPack = (maxPack / 2) - headerSize;

      NetEvent evnt1 = CreateFilled(NetConfig.MAX_DATA_SIZE);
      NetEvent evnt2 = CreateFilled(halfPack);
      NetEvent evnt3 = CreateFilled(halfPack);
      NetEvent evnt4 = CreateFilled(halfPack + 1);

      // The buffer should fit this notification exactly.
      TestNetEncoding.TestNotificationCase(maxPack, 1, evnt1);

      // The buffer should fit these two notifications exactly.
      TestNetEncoding.TestNotificationCase(maxPack, 2, evnt2, evnt3);

      // The second notification should be one byte too big for the buffer.
      TestNetEncoding.TestNotificationCase(halfPack + headerSize, 1, evnt3, evnt4);

      // We should pack no bytes and read no notifications for this.
      TestNetEncoding.TestNotificationCase(0, 0);
    }

    [TestMethod]
    public void TestNotificationUnpack()
    {
      // Encoding and decoding 3 events containing three strings.
      byte[] buffer = new byte[2048];
      ushort notifyAckIn = 6533;
      ushort notifySeqIn = 23;
      ushort notifyAckOut;
      ushort notifySeqOut;

      string firstStr = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.";
      string secondStr = "sit amet tristique mauris pulvinar a. Fusce urna nulla, vehicula id pellentesque at";
      string thirdStr = "Ut orci sapien, tincidunt eget volutpat sed, sagittis vel ante. Etiam sodales ante id justo condimentum, in tincidunt mi sagittis. Etiam maximus finibus sem.";

      Queue<NetEvent> srcQueue = new Queue<NetEvent>();
      srcQueue.Enqueue(CreateStringEvent(firstStr));
      srcQueue.Enqueue(CreateStringEvent(secondStr));
      srcQueue.Enqueue(CreateStringEvent(thirdStr));

      int packedSize = NetEncoding.PackCarrier(buffer, notifyAckIn, notifySeqIn, srcQueue);

      Queue<NetEvent> dstQueue = new Queue<NetEvent>();
      bool result = NetEncoding.ReadCarrier(CreateEvent, null, buffer, packedSize, out notifyAckOut, out notifySeqOut, dstQueue);
      NetEvent evntA = dstQueue.Dequeue();
      NetEvent evntB = dstQueue.Dequeue();
      NetEvent evntC = dstQueue.Dequeue();

      Assert.AreEqual(true, result);
      Assert.AreEqual(notifyAckIn, notifyAckOut);
      Assert.AreEqual(notifySeqIn, notifySeqOut);
      Assert.AreEqual(firstStr, Encoding.UTF8.GetString(evntA.EncodedData, 0, evntA.EncodedLength));
      Assert.AreEqual(secondStr, Encoding.UTF8.GetString(evntB.EncodedData, 0, evntB.EncodedLength));
      Assert.AreEqual(thirdStr, Encoding.UTF8.GetString(evntC.EncodedData, 0, evntC.EncodedLength));
    }

    [TestMethod]
    public void TestPayloadPack()
    {
      byte[] buffer = new byte[2048];

      int length;

      string firstStr = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.";
      byte[] firstBytes = Encoding.UTF8.GetBytes(firstStr);
      ushort seqIn = 42323;
      ushort seqOut;

      length = NetEncoding.PackPayload(buffer, seqIn, firstBytes, (ushort)firstBytes.Length);
      NetEvent evnt;
      bool result = NetEncoding.ReadPayload(CreateEvent, null, buffer, length, out seqOut, out evnt);

      Assert.AreEqual(true, result);
      Assert.AreEqual(seqIn, seqOut);
      Assert.AreEqual(firstStr, Encoding.UTF8.GetString(evnt.EncodedData, 0, evnt.EncodedLength));
    }

    [TestMethod]
    public void TestProtocol()
    {
      byte[] buffer = new byte[100];

      NetPacketType type = NetPacketType.Kick;
      byte firstParam = 0xAF;
      byte secondParam = 0xFA;
      int bytesPacked = 
        NetEncoding.PackProtocol(
          buffer, 
          type, 
          firstParam, 
          secondParam);

      byte firstParamRead;
      byte secondParamRead;
      bool success = 
        NetEncoding.ReadProtocol(
          buffer, 
          bytesPacked,
          out firstParamRead, 
          out secondParamRead);

      Assert.AreEqual(0, buffer[bytesPacked]);
      Assert.AreEqual(NetPacketType.Kick, NetEncoding.GetType(buffer));
      Assert.AreEqual(true, success);
      Assert.AreEqual(firstParam, firstParamRead);
      Assert.AreEqual(secondParam, secondParamRead);
    }

    [TestMethod]
    public void TestCorruptedCarrier()
    {
      byte[] buffer = new byte[2048];
      ushort ackOut;
      ushort seqOut;

      buffer[0] = (byte)NetPacketType.Carrier;
      buffer[6] = 200;

      int length;
      Queue<NetEvent> dstQueue;
      bool result;

      length = 20;
      dstQueue = new Queue<NetEvent>();
      result = NetEncoding.ReadCarrier(CreateEvent, null, buffer, length, out ackOut, out seqOut, dstQueue);
      Assert.AreEqual(false, result);

      length = 2;
      dstQueue = new Queue<NetEvent>();
      result = NetEncoding.ReadCarrier(CreateEvent, null, buffer, length, out ackOut, out seqOut, dstQueue);
      Assert.AreEqual(false, result);

      length = 207;
      dstQueue = new Queue<NetEvent>();
      result = NetEncoding.ReadCarrier(CreateEvent, null, buffer, length, out ackOut, out seqOut, dstQueue);
      Assert.AreEqual(true, result);
    }
  }
}
