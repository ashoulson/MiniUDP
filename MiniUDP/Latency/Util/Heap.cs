using System;
using System.Collections.Generic;

namespace MiniUDP.Util
{
  internal class Heap<T>
  {
    private const int INITIAL_CAPACITY = 0;
    private const int GROW_FACTOR = 2;
    private const int MIN_GROW = 1;

    private int capacity = Heap<T>.INITIAL_CAPACITY;
    private T[] heap = new T[Heap<T>.INITIAL_CAPACITY];
    private int tail = 0;

    public int Count { get { return this.tail; } }
    public int Capacity { get { return this.capacity; } }

    protected Comparer<T> Comparer { get; private set; }

    public Heap()
    {
      this.Comparer = Comparer<T>.Default;
    }

    public Heap(Comparer<T> comparer)
    {
      if (comparer == null)
        throw new ArgumentNullException("comparer");
      this.Comparer = comparer;
    }

    public void Add(T item)
    {
      if (this.Count == this.Capacity)
        this.Grow();
      this.heap[this.tail++] = item;
      this.BubbleUp(tail - 1);
    }

    public T GetMin()
    {
      if (this.Count == 0)
        throw new InvalidOperationException("Heap is empty");
      return this.heap[0];
    }

    public T ExtractDominating()
    {
      if (this.Count == 0)
        throw new InvalidOperationException("Heap is empty");
      T ret = this.heap[0];
      this.tail--;
      this.Swap(this.tail, 0);
      this.BubbleDown(0);
      return ret;
    }

    protected bool Dominates(T x, T y)
    {
      return this.Comparer.Compare(x, y) <= 0;
    }

    private void BubbleUp(int i)
    {
      if (i == 0)
        return;
      if (this.Dominates(this.heap[Heap<T>.Parent(i)], this.heap[i]))
        return; // Correct domination (or root)

      this.Swap(i, Heap<T>.Parent(i));
      this.BubbleUp(Heap<T>.Parent(i));
    }

    private void BubbleDown(int i)
    {
      int dominatingNode = this.Dominating(i);
      if (dominatingNode == i)
        return;
      this.Swap(i, dominatingNode);
      this.BubbleDown(dominatingNode);
    }

    private int Dominating(int i)
    {
      int dominatingNode = i;
      dominatingNode =
        this.GetDominating(Heap<T>.YoungChild(i), dominatingNode);
      dominatingNode =
        this.GetDominating(Heap<T>.OldChild(i), dominatingNode);
      return dominatingNode;
    }

    private int GetDominating(int newNode, int dominatingNode)
    {
      if (newNode >= tail)
        return dominatingNode;
      if (this.Dominates(this.heap[dominatingNode], this.heap[newNode]))
        return dominatingNode;
      return newNode;
    }

    private void Swap(int i, int j)
    {
      T tmp = this.heap[i];
      this.heap[i] = this.heap[j];
      this.heap[j] = tmp;
    }

    private static int Parent(int i)
    {
      return (i + 1) / 2 - 1;
    }

    private static int YoungChild(int i)
    {
      return (i + 1) * 2 - 1;
    }

    private static int OldChild(int i)
    {
      return Heap<T>.YoungChild(i) + 1;
    }

    private void Grow()
    {
      int newCapacity =
        this.capacity * Heap<T>.GROW_FACTOR + Heap<T>.MIN_GROW;
      T[] newHeap = new T[newCapacity];
      Array.Copy(this.heap, newHeap, this.capacity);
      this.heap = newHeap;
      this.capacity = newCapacity;
    }
  }
}
