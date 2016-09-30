using System;
using System.Collections.Generic;
using System.Threading;

namespace MiniUDP
{
  internal class NetPipeline<T>
  {
    private Queue<T> queue;
    private volatile int count;

    public NetPipeline()
    {
      this.queue = new Queue<T>();
      this.count = 0;
    }

    public bool TryDequeue(out T obj)
    {
      // This check can be done out of lock...
      obj = default(T);
      if (this.count <= 0)
        return false;

      lock (this.queue)
      {
        obj = this.queue.Dequeue();
        Interlocked.Decrement(ref this.count);
        return true;
      }
    }

    public void Enqueue(T obj)
    {
      lock (this.queue)
        this.queue.Enqueue(obj);

      // ...as long as this ++ is atomic and happens after we add
      Interlocked.Increment(ref this.count);
    }
  }
}
