/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2015-2018 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
