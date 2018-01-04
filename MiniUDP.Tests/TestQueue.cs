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

using System.Threading;
using System.Collections.Concurrent;

using MiniUDP;
using System;

namespace Tests
{
  [TestClass]
  public class TestQueue
  {
    private const int ITERATIONS = 25000000;

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
  }
}
