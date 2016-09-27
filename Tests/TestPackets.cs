using Microsoft.VisualStudio.TestTools.UnitTesting;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestPackets
  {
    private static byte remoteLoss = 21;
    private static byte notifyAck = 21;
    private static byte pingSequence = 21;
    private static byte pongSequence = 21;
    private static ushort pongProcessTime = 3221;

    internal static void PopulateSessionPacketHeader(NetSessionPacket packet)
    {
      packet.remoteLoss = TestPackets.remoteLoss;
      packet.notifyAck = TestPackets.notifyAck;
      packet.pingSequence = TestPackets.pingSequence;
      packet.pongSequence = TestPackets.pongSequence;
      packet.pongProcessTime = TestPackets.pongProcessTime;
    }

    internal static void CheckSessionPacketHeader(NetSessionPacket packet)
    {
      Assert.AreEqual(TestPackets.remoteLoss, packet.remoteLoss);
      Assert.AreEqual(TestPackets.notifyAck, packet.notifyAck);
      Assert.AreEqual(TestPackets.pingSequence, packet.pingSequence);
      Assert.AreEqual(TestPackets.pongSequence, packet.pongSequence);
      Assert.AreEqual(TestPackets.pongProcessTime, packet.pongProcessTime);
    }

    internal static void CheckResetSessionPacket(NetSessionPacket packet)
    {
      Assert.AreEqual(0, packet.remoteLoss);
      Assert.AreEqual(0, packet.notifyAck);
      Assert.AreEqual(0, packet.pingSequence);
      Assert.AreEqual(0, packet.pongSequence);
      Assert.AreEqual(0, packet.pongProcessTime);
      Assert.AreEqual(0, packet.notifications.Count);
    }

    [TestMethod]
    public void TestSessionPacket()
    {
      NetPool<NetEvent> notificationPool = new NetPool<NetEvent>();

      NetSessionPacket packet = new NetSessionPacket();
      TestPackets.PopulateSessionPacketHeader(packet);

      // Fill the session with notifications
      int numAdded = 0;
      while (true)
      {
        NetEvent notification = notificationPool.Allocate();
        notification.sequence = (byte)numAdded;
        notification.userData.Append(TestByteBuffer.FillBuffer());

        if (packet.TryAdd(notification) == false)
          break;
        numAdded++; 
      }

      // Make sure we filled the right number
      int notificationSize =
        TestByteBuffer.FillBuffer().Length
        + NetEvent.EVENT_HEADER_SIZE;
      int expectedFill = NetConst.MAX_SESSION_DATA_SIZE / notificationSize;
      Assert.AreEqual(expectedFill, packet.notifications.Count);

      NetByteBuffer writeBuffer = new NetByteBuffer(NetConst.MAX_PACKET_SIZE);
      packet.Write(writeBuffer);

      foreach (NetEvent notification in packet.notifications)
        notificationPool.Deallocate(notification);
      packet.Reset();

      // Make sure deallocation worked
      CheckResetSessionPacket(packet);

      byte[] rawData = new byte[10000];
      int bytes = writeBuffer.Store(rawData);

      NetByteBuffer readBuffer = new NetByteBuffer(NetConst.MAX_PACKET_SIZE);
      readBuffer.Load(rawData, bytes);

      packet.Read(readBuffer, () => notificationPool.Allocate());

      // Check header data
      CheckSessionPacketHeader(packet);

      // Check the received notifications
      int sequence = 0;
      foreach (NetEvent notification in packet.notifications)
      {
        Assert.AreEqual(notification.sequence, sequence);
        TestByteBuffer.EvaluateBuffer(notification.userData);
        sequence++;
      }
    }
  }
}
