using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Threading;
using System.Collections.Concurrent;

using MiniUDP;
using System;

namespace Tests
{
  [TestClass]
  public class TestQueue
  {
    private const int ITERATIONS = 80000000;

    private interface TestQueueHolder<T>
    {
      void Enqueue(T value);
      bool TryDequeue(out T value);
    }

    private class NetMessageQueueHolder<T> : TestQueueHolder<T>
    {
      private NetPipeline<T> queue;

      public NetMessageQueueHolder() { this.queue = new NetPipeline<T>(); }
      public void Enqueue(T value) { this.queue.Enqueue(value); }
      public bool TryDequeue(out T value) { return this.queue.TryDequeue(out value); }
    }

    private class ConcurrentQueueHolder<T> : TestQueueHolder<T>
    {
      private ConcurrentQueue<T> queue;

      public ConcurrentQueueHolder() { this.queue = new ConcurrentQueue<T>(); }
      public void Enqueue(T value) { this.queue.Enqueue(value); }
      public bool TryDequeue(out T value) { return this.queue.TryDequeue(out value); }
    }

    private class IntHolder
    {
      public int value;

      public IntHolder(int i)
      {
        this.value = i;
      }
    }

    private static long checkSumA = 0;
    private static long checkSumB = 0;
    private static TestQueueHolder<int> buffer = null;
    private static bool run = false;

    private static void Consume()
    {
      int value;
      bool received;
      while ((received = buffer.TryDequeue(out value)) || run)
        if (received)
          checkSumB += value;
    }

    private static void Produce(TestQueueHolder<int> holder)
    {
      checkSumA = 0;
      checkSumB = 0;
      buffer = holder;

      Thread consumeThread = new Thread(new ThreadStart(Consume));
      run = true;
      consumeThread.Start();

      for (int i = 0; i < ITERATIONS; i++)
      {
        buffer.Enqueue(i);
        checkSumA += i;
      }

      run = false;
      consumeThread.Join();

      Assert.IsTrue(checkSumA > 0);
      Assert.AreEqual(checkSumA, checkSumB);
    }

    [TestMethod]
    public void TestMessageQueue()
    {
      Produce(new NetMessageQueueHolder<int>());
    }

    [TestMethod]
    public void TestConcurrentQueue()
    {
      Produce(new ConcurrentQueueHolder<int>());
    }
  }
}
