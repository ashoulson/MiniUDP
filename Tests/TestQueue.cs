using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Threading;
using System.Collections.Concurrent;

using MiniUDP;
using System;

namespace Tests
{
  public class SPSCQueue<T>
  {
    private readonly T[] _data;
    private readonly int _queueSize;
    private volatile uint _readPos;
#pragma warning disable 169 // unused field
    // Pad out _readPos and _writePos so they're on different 
    // cache lines. This is purely for optimization. Not doing
    // this won't cause any errors.
    private long _p0, _p1, _p2, _p3, _p4, _p5, _p6, _p7;
#pragma warning restore 169
    private volatile uint _writePos;

    public SPSCQueue(int queueSize)
    {
      _queueSize = queueSize;
      _data = new T[_queueSize];
    }

    private int PositionToArrayIndex(uint pos)
    {
      return (int)(pos % _queueSize);
    }

    public bool Enqueue(T entry)
    {
      var readIndex = PositionToArrayIndex(_readPos);
      var currentWritePos = _writePos;
      var writeIndex = PositionToArrayIndex(currentWritePos + 1);

      if (readIndex == writeIndex)
        return false; // queue full

      _data[PositionToArrayIndex(currentWritePos)] = entry;

      _writePos++;
      return true;
    }

    private int _numberOfTimeWaitedForEnqueue;

    public bool Enqueue(T entry, int timeout)
    {
      if (Enqueue(entry))
      {
        _numberOfTimeWaitedForEnqueue = 0;
        return true;
      }
      while (timeout > 0)
      {
        _numberOfTimeWaitedForEnqueue++;
        var timeToWait = _numberOfTimeWaitedForEnqueue / 2;
        if (timeToWait < 2)
          timeToWait = 2;
        else if (timeToWait > timeout)
          timeToWait = timeout;
        timeout -= timeToWait;
        Thread.Sleep(timeToWait);
        if (Enqueue(entry))
        {
          return true;
        }
      }
      return false;
    }

    public bool Dequeue(out T entry)
    {
      entry = default(T);
      var readIndex = PositionToArrayIndex(_readPos);
      var writeIndex = PositionToArrayIndex(_writePos);

      if (readIndex == writeIndex)
        return false; // queue empty

      entry = _data[readIndex];
      _data[readIndex] = default(T);

      _readPos++;

      return true;
    }
  }

  [TestClass]
  public class TestQueue
  {
    private const int ITERATIONS = 250000000;

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


    private class SPSCQueueHolder<T> : TestQueueHolder<T>
    {
      private SPSCQueue<T> queue;

      public SPSCQueueHolder() { this.queue = new SPSCQueue<T>(2048); }
      public void Enqueue(T value) { this.queue.Enqueue(value, 9999); }
      public bool TryDequeue(out T value) { return this.queue.Dequeue(out value); }
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
      Random rand = new Random();

      int value;
      bool received;
      while ((received = buffer.TryDequeue(out value)) || run)
      {
        if (received)
          checkSumB += value;

        //Thread.Sleep(1);
      }
    }

    private static void Produce(TestQueueHolder<int> holder)
    {
      Random rand = new Random();

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

        //if (rand.Next(10) >= 7)
        //  Thread.Sleep(1);
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

    [TestMethod]
    public void TestSPSCQueue()
    {
      Produce(new SPSCQueueHolder<int>());
    }
  }
}
