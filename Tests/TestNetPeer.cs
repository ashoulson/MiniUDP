using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Linq;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestNetPeer
  {
    [TestMethod]
    public void TestReliableCleanup()
    {
      NetPeer netPeer = new NetPeer(null, 0);
      for (int i = 0; i < 20; i++)
        netPeer.QueueNotification(new NetEvent());

      netPeer.CleanNotifications(10, (x) => x = null);
      Assert.AreEqual(netPeer.OutgoingNotifications.Count(), 9);
    }
  }
}
