//using Microsoft.VisualStudio.TestTools.UnitTesting;

//using MiniUDP;

//namespace Tests
//{
//  [TestClass]
//  public class TestPackets
//  {
//    private static byte remoteLoss = 21;
//    private static byte notifyAck = 21;
//    private static byte pingSequence = 21;
//    private static byte pongSequence = 21;
//    private static ushort pongProcessTime = 3221;

//    internal static void PopulateSessionPacketHeader(NetSessionPacket packet)
//    {
//      packet.Initialize(
//        0,
//        TestPackets.remoteLoss,
//        TestPackets.notifyAck,
//        TestPackets.pingSequence,
//        TestPackets.pongSequence,
//        TestPackets.pongProcessTime);
//    }

//    internal static void CheckSessionPacketHeader(NetSessionPacket packet)
//    {
//      Assert.AreEqual(TestPackets.remoteLoss, packet.RemoteLoss);
//      Assert.AreEqual(TestPackets.notifyAck, packet.NotifyAck);
//      Assert.AreEqual(TestPackets.pingSequence, packet.PingSequence);
//      Assert.AreEqual(TestPackets.pongSequence, packet.PongSequence);
//      Assert.AreEqual(TestPackets.pongProcessTime, packet.PongProcessTime);
//    }

//    internal static void CheckResetSessionPacket(NetSessionPacket packet)
//    {
//      Assert.AreEqual(0, packet.RemoteLoss);
//      Assert.AreEqual(0, packet.NotifyAck);
//      Assert.AreEqual(0, packet.PingSequence);
//      Assert.AreEqual(0, packet.PongSequence);
//      Assert.AreEqual(0, packet.PongProcessTime);
//      Assert.AreEqual(0, packet.notifications.Count);
//    }

//    [TestMethod]
//    public void TestSessionPacket()
//    {
//      NetPool<NetEvent> notificationPool = new NetPool<NetEvent>();

//      NetSessionPacket packet = new NetSessionPacket();
//      TestPackets.PopulateSessionPacketHeader(packet);

//      // Fill the session with notifications
//      int numAdded = 0;
//      while (true)
//      {
//        NetEvent notification = notificationPool.Allocate();
//        notification.Initialize(
//          NetEventType.Notification, 
//          null, 
//          (ushort)numAdded, 
//          -1, 
//          TestByteBuffer.FillBuffer());

//        if (packet.TryAdd(notification) == false)
//          break;
//        numAdded++; 
//      }

//      // Make sure we filled the right number
//      int notificationSize =
//        TestByteBuffer.FillBuffer().Length
//        + NetEvent.EVENT_HEADER_SIZE;
//      int expectedFill = NetConst.MAX_SESSION_DATA_SIZE / notificationSize;
//      Assert.AreEqual(expectedFill, packet.notifications.Count);

//      NetByteBuffer writeBuffer = new NetByteBuffer(NetConst.MAX_PACKET_SIZE);
//      packet.Write(writeBuffer);

//      int alternator = 0;
//      foreach (NetEvent notification in packet.notifications)
//        if ((alternator++ % 2) == 1) // Free every other notification
//          notificationPool.Deallocate(notification);
//      packet.Reset();

//      // Make sure deallocation worked
//      CheckResetSessionPacket(packet);

//      byte[] rawData = new byte[10000];
//      int bytes = writeBuffer.Store(rawData);

//      NetByteBuffer readBuffer = new NetByteBuffer(NetConst.MAX_PACKET_SIZE);
//      readBuffer.Load(rawData, bytes);

//      packet.Read(readBuffer, () => notificationPool.Allocate());

//      // Check header data
//      CheckSessionPacketHeader(packet);

//      // Check the received notifications
//      int sequence = 0;
//      foreach (NetEvent notification in packet.notifications)
//      {
//        Assert.AreEqual(notification.Sequence, sequence);
//        TestByteBuffer.EvaluateBuffer(notification.userData);
//        sequence++;
//      }
//    }
//  }
//}
