using Microsoft.VisualStudio.TestTools.UnitTesting;

using MiniUDP;

namespace Tests
{
  [TestClass]
  public class TestByteBuffer
  {
    private static bool a = true;
    private static byte b = 25;
    private static short c = -372;
    private static ushort d = 10002;
    private static string shortString = "TestingTesting";
    private static string tooLongString = "TestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTesting";
    private static int e = -57399;
    private static uint f = 4423532;
    private static long g = -47238930234;
    private static ulong h = 47238972387409970;

    internal static NetByteBuffer FillBuffer()
    {
      NetByteBuffer buffer = new NetByteBuffer(3000);

      buffer.Write(TestByteBuffer.a);
      buffer.Write(TestByteBuffer.b);
      buffer.Write(TestByteBuffer.c);
      buffer.Write(TestByteBuffer.d);
      buffer.Write(TestByteBuffer.shortString, 30);
      buffer.Write(TestByteBuffer.tooLongString, 30);
      buffer.Write(TestByteBuffer.e);
      buffer.Write(TestByteBuffer.f);
      buffer.Write(TestByteBuffer.g);
      buffer.Write(TestByteBuffer.h);

      return buffer;
    }

    internal static void EvaluateBuffer(NetByteBuffer buffer)
    {
      Assert.AreEqual(TestByteBuffer.a, buffer.ReadBool());
      Assert.AreEqual(TestByteBuffer.b, buffer.ReadByte());
      Assert.AreEqual(TestByteBuffer.c, buffer.ReadShort());
      Assert.AreEqual(TestByteBuffer.d, buffer.ReadUShort());
      Assert.AreEqual(TestByteBuffer.shortString, buffer.ReadString());
      Assert.AreEqual("", buffer.ReadString());
      Assert.AreEqual(TestByteBuffer.e, buffer.ReadInt());
      Assert.AreEqual(TestByteBuffer.f, buffer.ReadUInt());
      Assert.AreEqual(TestByteBuffer.g, buffer.ReadLong());
      Assert.AreEqual(TestByteBuffer.h, buffer.ReadULong());
      Assert.AreEqual(0, buffer.ReadRemaining, 0);
    }

    [TestMethod]
    public void TestByteBufferSimple()
    {

      TestByteBuffer.EvaluateBuffer(TestByteBuffer.FillBuffer());
    }

    [TestMethod]
    public void TestByteBufferNested()
    {
      NetByteBuffer inBuffer1 = TestByteBuffer.FillBuffer();
      NetByteBuffer inBuffer2 = TestByteBuffer.FillBuffer();

      NetByteBuffer combined = new NetByteBuffer(1000);
      combined.Append(inBuffer1);
      combined.Append(inBuffer2);

      NetByteBuffer outBuffer1 = new NetByteBuffer(1000);
      NetByteBuffer outBuffer2 = new NetByteBuffer(1000);

      combined.Extract(outBuffer1, inBuffer1.Length);
      combined.ExtractRemaining(outBuffer2);

      EvaluateBuffer(outBuffer1);
      EvaluateBuffer(outBuffer2);
      Assert.AreEqual(combined.ReadRemaining, 0);
    }
  }
}
