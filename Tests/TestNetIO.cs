using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestNetIO
  {
    private static NetEvent CreateEvent()
    {
      return new NetEvent();
    }

    [TestMethod]
    public void TestNotificationPack()
    {
      byte[] buffer = new byte[2048];
      byte[] filled = new byte[2048];
      for (int i = 0; i < filled.Length; i++)
        filled[i] = (byte)(i % 256);

      int headerSize = NetEncoding.PackNotificationHeader(buffer, 0, 0);

      ushort notifyAckIn = 23;
      ushort notifySeqIn = 6533;

      ushort notifyAckOut;
      ushort notifySeqOut;

      NetEvent evnt1 = new NetEvent();
      NetEvent evnt2 = new NetEvent();
      NetEvent evnt3 = new NetEvent();
      NetEvent evnt4 = new NetEvent();
      NetEvent evnt5 = new NetEvent();

      int halfPack = (NetConfig.MAX_NOTIFICATION_PACK / 2) - NetEvent.HEADER_SIZE;

      Queue<NetEvent> srcQueue = new Queue<NetEvent>();
      Queue<NetEvent> dstQueue = new Queue<NetEvent>();
      int packedSize;

      evnt1.Initialize(NetEventType.Notification, null, filled, NetConfig.MAX_DATA_SIZE);
      evnt2.Initialize(NetEventType.Notification, null, filled, halfPack);
      evnt3.Initialize(NetEventType.Notification, null, filled, halfPack);
      evnt4.Initialize(NetEventType.Notification, null, filled, halfPack + 1);

      // The buffer should fit this notification exactly.
      srcQueue.Clear();
      srcQueue.Enqueue(evnt1);
      packedSize = NetEncoding.PackNotifications(buffer, notifyAckIn, notifySeqIn, srcQueue);
      dstQueue.Clear();
      NetEncoding.ReadNotifications(null, buffer, packedSize, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, dstQueue);
      Assert.AreEqual(notifyAckIn, notifyAckOut);
      Assert.AreEqual(notifySeqIn, notifySeqOut);
      Assert.AreEqual(1, dstQueue.Count);
      Assert.AreEqual(NetConfig.MAX_NOTIFICATION_PACK, packedSize - headerSize);

      // The buffer should fit these two notifications exactly.
      srcQueue.Clear();
      srcQueue.Enqueue(evnt2);
      srcQueue.Enqueue(evnt3);
      packedSize = NetEncoding.PackNotifications(buffer, notifyAckIn, notifySeqIn, srcQueue);
      dstQueue.Clear();
      NetEncoding.ReadNotifications(null, buffer, packedSize, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, dstQueue);
      Assert.AreEqual(notifyAckIn, notifyAckOut);
      Assert.AreEqual(notifySeqIn, notifySeqOut);
      Assert.AreEqual(2, dstQueue.Count);
      Assert.AreEqual(NetConfig.MAX_NOTIFICATION_PACK, packedSize - headerSize);

      // The second notification should be one byte too big for the buffer.
      srcQueue.Clear();
      srcQueue.Enqueue(evnt3);
      srcQueue.Enqueue(evnt4);
      packedSize = NetEncoding.PackNotifications(buffer, notifyAckIn, notifySeqIn, srcQueue);
      dstQueue.Clear();
      NetEncoding.ReadNotifications(null, buffer, packedSize, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, dstQueue);
      Assert.AreEqual(notifyAckIn, notifyAckOut);
      Assert.AreEqual(notifySeqIn, notifySeqOut);
      Assert.AreEqual(1, dstQueue.Count);
      Assert.AreEqual(halfPack + NetEvent.HEADER_SIZE, packedSize - headerSize);

      // We should pack no bytes and read no notifications for this.
      srcQueue.Clear();
      packedSize = NetEncoding.PackNotifications(buffer, notifyAckIn, notifySeqIn, srcQueue);
      dstQueue.Clear();
      NetEncoding.ReadNotifications(null, buffer, packedSize, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, dstQueue);
      Assert.AreEqual(notifyAckIn, notifyAckOut);
      Assert.AreEqual(notifySeqIn, notifySeqOut);
      Assert.AreEqual(0, dstQueue.Count);
      Assert.AreEqual(0, packedSize - headerSize);
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

      byte[] firstBytes = Encoding.UTF8.GetBytes(firstStr);
      byte[] secondBytes = Encoding.UTF8.GetBytes(secondStr);
      byte[] thirdBytes = Encoding.UTF8.GetBytes(thirdStr);

      NetEvent evnt1 = new NetEvent();
      NetEvent evnt2 = new NetEvent();
      NetEvent evnt3 = new NetEvent();

      evnt1.Initialize(NetEventType.Notification, null, firstBytes, firstBytes.Length);
      evnt2.Initialize(NetEventType.Notification, null, secondBytes, secondBytes.Length);
      evnt3.Initialize(NetEventType.Notification, null, thirdBytes, thirdBytes.Length);

      Queue<NetEvent> srcQueue = new Queue<NetEvent>();
      Queue<NetEvent> destQueue = new Queue<NetEvent>();
      srcQueue.Enqueue(evnt1);
      srcQueue.Enqueue(evnt2);
      srcQueue.Enqueue(evnt3);

      int packedSize = NetEncoding.PackNotifications(buffer, notifyAckIn, notifySeqIn, srcQueue);

      NetEncoding.ReadNotifications(null, buffer, packedSize, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, destQueue);
      NetEvent evntA = destQueue.Dequeue();
      NetEvent evntB = destQueue.Dequeue();
      NetEvent evntC = destQueue.Dequeue();

      Assert.AreEqual(notifyAckIn, notifyAckOut);
      Assert.AreEqual(notifySeqIn, notifySeqOut);
      Assert.AreEqual(firstStr, Encoding.UTF8.GetString(evntA.EncodedData, 0, evntA.EncodedLength));
      Assert.AreEqual(secondStr, Encoding.UTF8.GetString(evntB.EncodedData, 0, evntB.EncodedLength));
      Assert.AreEqual(thirdStr, Encoding.UTF8.GetString(evntC.EncodedData, 0, evntC.EncodedLength));
    }

    [TestMethod]
    public void TestBadNotificationLength()
    {
      byte[] buffer = new byte[2048];
      int size = 25;
      ushort notifyAckOut;
      ushort notifySeqOut;

      int headerBytes = NetEncoding.PackNotificationHeader(buffer, 0, 0);
      buffer[headerBytes] = (byte)size;
      int length = headerBytes + 1;

      // Add only one byte to the end. Since there aren't enough bytes to contain
      // the header for the notification size (2 bytes), this should prevent the 
      // notification from being read.
      Queue<NetEvent> destQueue = new Queue<NetEvent>();
      NetEncoding.ReadNotifications(null, buffer, length, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, destQueue);
      Assert.AreEqual(0, destQueue.Count);

      // Manually configure the size properly to cover the end of the buffer.
      // There should be one notification read.
      NetEncoding.PackU16(buffer, headerBytes, (ushort)size);
      length = headerBytes + size + NetEvent.HEADER_SIZE;
      destQueue.Clear();
      NetEncoding.ReadNotifications(null, buffer, length, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, destQueue);
      Assert.AreEqual(1, destQueue.Count);

      // Maliciously set the size to be beyond the buffer by 1 byte. 
      // This should prevent the notification from being read.
      NetEncoding.PackU16(buffer, headerBytes, (ushort)(size + 1));
      length = headerBytes + size + NetEvent.HEADER_SIZE;
      destQueue.Clear();
      NetEncoding.ReadNotifications(null, buffer, length, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, destQueue);
      Assert.AreEqual(0, destQueue.Count);

      // Maliciously set the size to end before the buffer by 1 byte. 
      // This should read the first notification but not cause any other issues.
      NetEncoding.PackU16(buffer, headerBytes, (ushort)(size - 1));
      length = headerBytes + size + NetEvent.HEADER_SIZE;
      destQueue.Clear();
      NetEncoding.ReadNotifications(null, buffer, length, out notifyAckOut, out notifySeqOut, TestNetIO.CreateEvent, destQueue);
      Assert.AreEqual(1, destQueue.Count);
    }

    [TestMethod]
    public void TestPayloadHeader()
    {
      byte[] buffer = new byte[100];

      ushort sequence = 0xFACD;
      int bytesPacked = 
        NetEncoding.PackPayloadHeader(
          buffer, 
          sequence);

      ushort sequenceRead;
      int bytesRead = 
        NetEncoding.ReadPayloadHeader(
          buffer, 
          out sequenceRead);

      Assert.AreEqual(0, buffer[bytesPacked]);
      Assert.AreEqual(NetPacketType.Payload, NetEncoding.GetType(buffer));
      Assert.AreEqual(bytesPacked, bytesRead);
      Assert.AreEqual(sequence, sequenceRead);
    }

    [TestMethod]
    public void TestPayloadPack()
    {
      byte[] buffer = new byte[2048];
      byte[] dataBuffer = new byte[2048];

      int length;
      int dataLength;

      string firstStr = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.";
      byte[] firstBytes = Encoding.UTF8.GetBytes(firstStr);
      ushort seqIn = 42323;
      ushort seqOut;

      length = NetEncoding.PackPayload(buffer, seqIn, firstBytes, firstBytes.Length);
      NetEncoding.ReadPayload(buffer, length, out seqOut, dataBuffer, out dataLength);

      Assert.AreEqual(seqIn, seqOut);
      Assert.AreEqual(firstStr, Encoding.UTF8.GetString(dataBuffer, 0, dataLength));
    }

    [TestMethod]
    public void TestProtocolHeader()
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
      int bytesRead = 
        NetEncoding.ReadProtocol(
          buffer, 
          out firstParamRead, 
          out secondParamRead);

      Assert.AreEqual(0, buffer[bytesPacked]);
      Assert.AreEqual(NetPacketType.Kick, NetEncoding.GetType(buffer));
      Assert.AreEqual(bytesPacked, bytesRead);
      Assert.AreEqual(firstParam, firstParamRead);
      Assert.AreEqual(secondParam, secondParamRead);
    }

    [TestMethod]
    public void TestCarrierHeader()
    {
      byte[] buffer = new byte[100];

      ushort messageAck = 0xDCAF;
      ushort messageSeq = 0xFCCE;
      int bytesPacked =
        NetEncoding.PackNotificationHeader(
          buffer,
          messageAck,
          messageSeq);

      ushort notificationAckRead;
      ushort notificationSeqRead;
      int bytesRead =
        NetEncoding.ReadNotificationHeader(
          buffer,
          out notificationAckRead,
          out notificationSeqRead);

      Assert.AreEqual(0, buffer[bytesPacked]);
      Assert.AreEqual(NetPacketType.Notification, NetEncoding.GetType(buffer));
      Assert.AreEqual(bytesPacked, bytesPacked);
      Assert.AreEqual(messageAck, notificationAckRead);
      Assert.AreEqual(messageSeq, notificationSeqRead);
    }
  }
}
