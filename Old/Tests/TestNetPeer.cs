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
      NetPeer netPeer = new NetPeer(null, "Token", false, 0);
      for (int i = 0; i < 20; i++)
        netPeer.QueueNotification(new NetEvent());

      netPeer.OnReceiveCarrier(0, 10, (x) => x = null);
      // First sequence number is 1, so we should have 10 remaining
      Assert.AreEqual(10, netPeer.Outgoing.Count());
    }
  }
}
