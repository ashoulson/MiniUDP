using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace MiniUDP
{
  /// <summary>
  /// Implementation of the Disruptor pattern
  /// http://forum.unity3d.com/threads/thread-safe-queue-with-no-allocations.308842/
  /// </summary>
  /// <typeparam name="T">the type of item to be stored</typeparam>
  public class RingBuffer<T>
  {
    private const int CACHE_LINE_SIZE = 64;

    [StructLayout(LayoutKind.Explicit, Size = CACHE_LINE_SIZE * 2)]
    public struct PaddedLong
    {
      [FieldOffset(CACHE_LINE_SIZE)]
      private long value;

      /// <summary>
      /// Create a new <see cref="PaddedLong"/> with the given initial value.
      /// </summary>
      /// <param name="value">Initial value</param>
      public PaddedLong(long value)
      {
        this.value = value;
      }

      /// <summary>
      /// Read the value without applying any fence
      /// </summary>
      /// <returns>The current value</returns>
      public long ReadUnfenced()
      {
        return this.value;
      }

      /// <summary>
      /// Read the value applying acquire fence semantic
      /// </summary>
      /// <returns>The current value</returns>
      public long ReadAcquireFence()
      {
        var value = this.value;
        Thread.MemoryBarrier();
        return value;
      }

      /// <summary>
      /// Read the value applying full fence semantic
      /// </summary>
      /// <returns>The current value</returns>
      public long ReadFullFence()
      {
        Thread.MemoryBarrier();
        return this.value;
      }

      /// <summary>
      /// Read the value applying a compiler only fence, 
      /// no CPU fence is applied
      /// </summary>
      /// <returns>The current value</returns>
      [MethodImpl(MethodImplOptions.NoOptimization)]
      public long ReadCompilerOnlyFence()
      {
        return this.value;
      }

      /// <summary>
      /// Write the value applying release fence semantic
      /// </summary>
      /// <param name="newValue">The new value</param>
      public void WriteReleaseFence(long newValue)
      {
        Thread.MemoryBarrier();
        this.value = newValue;
      }

      /// <summary>
      /// Write the value applying full fence semantic
      /// </summary>
      /// <param name="newValue">The new value</param>
      public void WriteFullFence(long newValue)
      {
        Thread.MemoryBarrier();
        this.value = newValue;
      }

      /// <summary>
      /// Write the value applying a compiler fence only, 
      /// no CPU fence is applied
      /// </summary>
      /// <param name="newValue">The new value</param>
      [MethodImpl(MethodImplOptions.NoOptimization)]
      public void WriteCompilerOnlyFence(long newValue)
      {
        this.value = newValue;
      }

      /// <summary>
      /// Write without applying any fence
      /// </summary>
      /// <param name="newValue">The new value</param>
      public void WriteUnfenced(long newValue)
      {
        this.value = newValue;
      }

      /// <summary>
      /// Atomically set the value to the given updated value if the current 
      /// value equals the comparand
      /// </summary>
      /// <param name="newValue">The new value</param>
      /// <param name="comparand">The comparand (expected value)</param>
      /// <returns></returns>
      public bool AtomicCompareExchange(long newValue, long comparand)
      {
        return
          comparand ==
          Interlocked.CompareExchange(
            ref this.value, 
            newValue, 
            comparand);
      }

      /// <summary>
      /// Atomically set the value to the given updated value
      /// </summary>
      /// <param name="newValue">The new value</param>
      /// <returns>The original value</returns>
      public long AtomicExchange(long newValue)
      {
        return Interlocked.Exchange(ref this.value, newValue);
      }

      /// <summary>
      /// Atomically add the given value to the current 
      /// value and return the sum
      /// </summary>
      /// <param name="delta">The value to be added</param>
      /// <returns>The sum of the current value and the given value</returns>
      public long AtomicAddAndGet(long delta)
      {
        return Interlocked.Add(ref this.value, delta);
      }

      /// <summary>
      /// Atomically increment the current value and return the new value
      /// </summary>
      /// <returns>The incremented value.</returns>
      public long AtomicIncrementAndGet()
      {
        return Interlocked.Increment(ref this.value);
      }

      /// <summary>
      /// Atomically increment the current value and return the new value
      /// </summary>
      /// <returns>The decremented value.</returns>
      public long AtomicDecrementAndGet()
      {
        return Interlocked.Decrement(ref this.value);
      }

      /// <summary>
      /// Returns the string representation of the current value.
      /// </summary>
      /// <returns>the string representation of the current value.</returns>
      public override string ToString()
      {
        var value = ReadFullFence();
        return value.ToString();
      }
    }

    private readonly T[] entries;
    private readonly int modMask;
    private PaddedLong consumerCursor = new PaddedLong();
    private PaddedLong producerCursor = new PaddedLong();

    /// <summary>
    /// Creates a new RingBuffer with the given capacity
    /// </summary>
    /// <param name="capacity">The capacity of the buffer</param>
    /// <remarks>Only a single thread may attempt to 
    /// consume at any one time</remarks>
    public RingBuffer(int capacity)
    {
      capacity = NextPowerOfTwo(capacity);
      this.modMask = capacity - 1;
      this.entries = new T[capacity];
    }

    /// <summary>
    /// The maximum number of items that can be stored
    /// </summary>
    public int Capacity
    {
      get { return this.entries.Length; }
    }

    public T this[long index]
    {
      get { unchecked { return this.entries[index & this.modMask]; } }
      set { unchecked { this.entries[index & this.modMask] = value; } }
    }

    /// <summary>
    /// Removes an item from the buffer.
    /// </summary>
    /// <returns>The next available item</returns>
    public T Dequeue()
    {
      var next = this.consumerCursor.ReadAcquireFence() + 1;

      // Make sure we read the data from entries 
      // after we have read the producer cursor
      while (this.producerCursor.ReadAcquireFence() < next)
        Thread.SpinWait(1);
      var result = this[next];

      // Make sure we read the data from entries
      // before we update the consumer cursor
      this.consumerCursor.WriteReleaseFence(next);
      return result;
    }

    /// <summary>
    /// Attempts to remove an items from the queue
    /// </summary>
    /// <param name="obj">the items</param>
    /// <returns>True if successful</returns>
    public bool TryDequeue(out T obj)
    {
      var next = this.consumerCursor.ReadAcquireFence() + 1;

      if (this.producerCursor.ReadAcquireFence() < next)
      {
        obj = default(T);
        return false;
      }
      obj = Dequeue();
      return true;
    }

    /// <summary>
    /// Add an item to the buffer
    /// </summary>
    /// <param name="item"></param>
    public void Enqueue(T item)
    {
      var next = this.producerCursor.ReadAcquireFence() + 1;

      long wrapPoint = next - this.entries.Length;
      long min = this.consumerCursor.ReadAcquireFence();

      while (wrapPoint > min)
      {
        min = this.consumerCursor.ReadAcquireFence();
        Thread.SpinWait(1);
      }

      this[next] = item;
      // Make sure we write the data in entries 
      // before we update the producer cursor
      this.producerCursor.WriteReleaseFence(next);
    }

    private static int NextPowerOfTwo(int x)
    {
      var result = 2;
      while (result < x)
      {
        result <<= 1;
      }
      return result;
    }
  }
}