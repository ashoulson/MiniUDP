/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
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
