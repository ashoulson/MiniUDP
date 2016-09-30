using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestNetIO
  {
    private delegate int Write0(byte[] buffer);
    private delegate int Write1(byte[] buffer, byte b1);
    private delegate int Write2(byte[] buffer, byte b1, byte b2);
    private delegate int Write3(byte[] buffer, byte b1, byte b2, ushort b3);

    private delegate void Read0(byte[] buffer);
    private delegate void Read1(byte[] buffer, out byte b1);
    private delegate void Read2(byte[] buffer, out byte b1, out byte b2);
    private delegate void Read3(byte[] buffer, out byte b1, out byte b2, out ushort b3);

    private const byte B1_IN = 111;
    private const byte B2_IN = 222;
    private const ushort US_IN = 32342;

    private static byte B1Out = 0;
    private static byte B2Out = 0;
    private static ushort USOut = 0;

    private static void Check(bool b1, bool b2, bool us)
    {
      if (b1)
        Assert.AreEqual(B1_IN, B1Out);
      if (b2)
        Assert.AreEqual(B2_IN, B2Out);
      if (us)
        Assert.AreEqual(US_IN, USOut);
    }

    private static void Clear()
    {
      TestNetIO.B1Out = 0;
      TestNetIO.B2Out = 0;
      TestNetIO.USOut = 0;
    }

    [TestMethod]
    public void TestProtocol()
    {
      byte[] buffer = new byte[100];

      TestIO(buffer, NetIO.WriteConnectRequest, NetIO.ReadConnectRequest, NetPacketType.ConnectRequest);
      TestIO(buffer, NetIO.WriteConnectAccept, NetIO.ReadConnectAccept, NetPacketType.ConnectAccept);
      TestIO(buffer, NetIO.WriteConnectReject, NetIO.ReadConnectReject, NetPacketType.ConnectReject);
      TestIO(buffer, NetIO.WriteDisconnect, NetIO.ReadDisconnect, NetPacketType.Disconnect);
      TestIO(buffer, NetIO.WritePing, NetIO.ReadPing, NetPacketType.Ping);
      TestIO(buffer, NetIO.WritePong, NetIO.ReadPong, NetPacketType.Pong);
    }

    private static void Verify(
      byte[] buffer, 
      int length, 
      NetPacketType type)
    {
      Assert.AreEqual(NetIO.PROTOCOL_SIZE, length);
      Assert.AreEqual(type, NetIO.GetPacketType(buffer));
    }

    private static void TestIO(
      byte[] buffer, 
      Write0 write, 
      Read0 read, 
      NetPacketType type)
    {
      Clear();
      int length = write.Invoke(buffer);
      read.Invoke(buffer);
      Verify(buffer, length, type);
    }

    private static void TestIO(
      byte[] buffer,
      Write1 write,
      Read1 read,
      NetPacketType type)
    {
      Clear();
      int length = write.Invoke(buffer, B1_IN);
      read.Invoke(buffer, out B1Out);
      Verify(buffer, length, type);
      Check(true, false, false);
    }

    private static void TestIO(
      byte[] buffer,
      Write2 write,
      Read2 read,
      NetPacketType type)
    {
      Clear();
      int length = write.Invoke(buffer, B1_IN, B2_IN);
      read.Invoke(buffer, out B1Out, out B2Out);
      Verify(buffer, length, type);
      Check(true, true, false);
    }

    private static void TestIO(
      byte[] buffer,
      Write3 write,
      Read3 read,
      NetPacketType type)
    {
      Clear();
      int length = write.Invoke(buffer, B1_IN, B2_IN, US_IN);
      read.Invoke(buffer, out B1Out, out B2Out, out USOut);
      Verify(buffer, length, type);
      Check(true, true, true);
    }
  }
}
