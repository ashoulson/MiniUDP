using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniUDP
{
  public static class Test
  {
    public static void TestPackets()
    {
      byte[] buffer1 = new byte[10000];
      byte[] buffer2 = new byte[10000];

      NetPool<NetNotification> notificationPool = new NetPool<NetNotification>();
      NetPool<NetSessionPacket> packetPool = new NetPool<NetSessionPacket>();

      NetByteBuffer writeBuffer = new NetByteBuffer(NetConfig.MAX_PACKET_SIZE);
      NetByteBuffer readBuffer = new NetByteBuffer(10000);

      NetNotification notification1 = notificationPool.Allocate();
      notification1.userData.Write("Hello", 100);

      NetNotification notification2 = notificationPool.Allocate();
      notification2.userData.Write("How", 100);

      NetNotification notification3 = notificationPool.Allocate();
      notification3.userData.Write("Are", 100);

      NetNotification notification4 = notificationPool.Allocate();
      notification4.userData.Write("You", 100);

      NetNotification notification5 = notificationPool.Allocate();
      notification5.userData.Write("1DOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIINNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNGGGGGGGGGGGGGGGGG1", 800);



      NetSessionPacket packet = packetPool.Allocate();

      packet.remoteLoss = 20;
      packet.notifyAck = 21;
      packet.pingSequence = 22;
      packet.pongSequence = 23;
      packet.pongProcessTime = 24;
      packet.TryAddNotification(notification1);
      packet.TryAddNotification(notification2);
      packet.TryAddNotification(notification3);
      packet.TryAddNotification(notification4);
      packet.TryAddNotification(notification5);

      packet.Write(writeBuffer);

      packetPool.Deallocate(packet);
      packet = null;

      int stored = writeBuffer.Store(buffer1);
      readBuffer.Load(buffer1, stored);

      NetSessionPacket newPacket = packetPool.Allocate();

      newPacket.Read(readBuffer, () => notificationPool.Allocate());


      foreach (NetNotification notification in newPacket.notifications)
        Console.WriteLine(notification.userData.ReadString());


    }
  }
}
