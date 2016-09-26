using Microsoft.VisualStudio.TestTools.UnitTesting;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestNetPeer
  {
    [TestMethod]
    public void TestNotifications()
    {
      NetPeer netPeer = new NetPeer(null);
      for (int i = 0; i < 20; i++)
        netPeer.QueueNotification(new NetNotification());

      netPeer.CleanNotifications(10, (x) => x = null);
      Assert.AreEqual(netPeer.NotificationCount, 9);
    }
  }
}
