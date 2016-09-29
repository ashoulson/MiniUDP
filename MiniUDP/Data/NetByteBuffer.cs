/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any expvalues or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following valuetrictions:
 *  
 *  1. The origin of this software must not be misrepvalueented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepvalueented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Diagnostics;
using System.Text;

namespace MiniUDP
{
  public interface INetByteReader
  {
    int Length { get; }
    int Position { get; }
    int ReadRemaining { get; }

    void Rewind();

    byte PeekByte();

    bool ReadBool();
    byte ReadByte();
    short ReadShort();
    ushort ReadUShort();
    int ReadInt();
    uint ReadUInt();
    ulong ReadULong();
    long ReadLong();
    string ReadString();

    int Store(byte[] destinationBuffer);
  }

  public interface INetByteWriter
  {
    int Capacity { get; }
    int Length { get; }
    int SpaceRemaining { get; }

    void Write(bool value);
    void Write(byte value);
    void Write(short value);
    void Write(ushort value);
    void Write(int value);
    void Write(uint value);
    void Write(long value);
    void Write(ulong value);
    void Write(string value, int maxBytes);

    void Load(byte[] sourceBuffer, int sourceLength);
  }

  internal class NetByteBuffer : INetByteReader, INetByteWriter
  {
    #region Encoding
    private static void Encode(byte[] buffer, int offset, byte value)
    {
      if (buffer.Length < (offset + 1))
        throw new OverflowException("buffer");

      buffer[offset + 0] = (byte)(value >> 0);
    }

    private static void Encode(byte[] buffer, int offset, ushort value)
    {
      if (buffer.Length < (offset + 2))
        throw new OverflowException("buffer");

      buffer[offset + 0] = (byte)(value >> 0);
      buffer[offset + 1] = (byte)(value >> 8);
    }

    private static void Encode(byte[] buffer, int offset, uint value)
    {
      if (buffer.Length < (offset + 4))
        throw new OverflowException("buffer");

      buffer[offset + 0] = (byte)(value >> 0);
      buffer[offset + 1] = (byte)(value >> 8);
      buffer[offset + 2] = (byte)(value >> 16);
      buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void Encode(byte[] buffer, int offset, ulong value)
    {
      if (buffer.Length < (offset + 8))
        throw new OverflowException("buffer");

      buffer[offset + 0] = (byte)(value >> 0);
      buffer[offset + 1] = (byte)(value >> 8);
      buffer[offset + 2] = (byte)(value >> 16);
      buffer[offset + 3] = (byte)(value >> 24);
      buffer[offset + 4] = (byte)(value >> 32);
      buffer[offset + 5] = (byte)(value >> 40);
      buffer[offset + 6] = (byte)(value >> 48);
      buffer[offset + 7] = (byte)(value >> 56);
    }
    #endregion

    public int Position { get { return this.position; } }
    public int Capacity { get { return this.rawData.Length; } }
    public int Length { get { return this.length; } }
    public int ReadRemaining { get { return this.length - this.position; } }
    public int SpaceRemaining { get { return this.rawData.Length - this.length; } }

    internal readonly byte[] rawData;
    internal int length;
    private int position;

    public NetByteBuffer(int capacity)
    {
      this.rawData = new byte[capacity];
      this.Reset();
    }

    public void Rewind()
    {
      this.position = 0;
    }

    internal void Reset()
    {
      this.length = 0;
      this.position = 0;
    }

    private void CheckLength(int increment)
    {
      if ((this.length + increment) > this.rawData.Length)
        throw new OverflowException("length + increment");
    }

    public void Load(byte[] sourceBuffer, int sourceLength)
    {
      if (sourceLength > this.rawData.Length)
        throw new OverflowException("sourceBuffer");

      Array.Copy(sourceBuffer, this.rawData, sourceLength);
      this.length += sourceLength;
    }

    public int Store(byte[] destinationBuffer)
    {
      Array.Copy(this.rawData, destinationBuffer, this.length);
      return this.length;
    }

    public void Append(NetByteBuffer source)
    {
      Buffer.BlockCopy(
        source.rawData,
        0,
        this.rawData,
        this.length,
        source.length);
      this.length += source.length;
    }

    public void Overwrite(NetByteBuffer source)
    {
      Buffer.BlockCopy(
        source.rawData,
        0,
        this.rawData,
        0,
        source.length);
      this.length = source.length;
    }

    public void Extract(NetByteBuffer destination, int count)
    {
      if (count > 0)
        Buffer.BlockCopy(
          this.rawData,
          this.position,
          destination.rawData,
          0,
          count);
      destination.length = count;
      this.IncreasePosition(count);
    }

    public void ExtractRemaining(NetByteBuffer destination)
    {
      this.Extract(destination, this.ReadRemaining);
    }

    /// <summary>
    /// Prevent reading uninitialized data
    /// </summary>
    private void IncreasePosition(int size)
    {
      this.position += size;
      if (this.position > this.length)
        throw new OverflowException("position");
    }

    #region Write
    public void Write(bool value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, (byte)(value ? 1 : 0));
      this.length += 1;
    }

    public void Write(byte value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, value);
      this.length += 1;
    }

    public void Write(short value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, (ushort)value);
      this.length += 2;
    }

    public void Write(ushort value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, value);
      this.length += 2;
    }

    public void Write(int value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, (uint)value);
      this.length += 4;
    }

    public void Write(uint value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, value);
      this.length += 4;
    }

    public void Write(long value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, (ulong)value);
      this.length += 8;
    }

    public void Write(ulong value)
    {
      NetByteBuffer.Encode(this.rawData, this.length, value);
      this.length += 8;
    }

    public void Write(string value, int maxBytes)
    {
      int stringLength = Encoding.UTF8.GetByteCount(value);
      if (stringLength > maxBytes)
      {
        stringLength = 0;
        value = "";
      }

      this.Write((ushort)stringLength);
      Encoding.UTF8.GetBytes(
        value, 
        0, 
        value.Length,
        this.rawData, 
        this.length);
      this.length += stringLength;
    }
    #endregion

    #region Read
    public byte PeekByte()
    {
      return this.rawData[this.position];
    }

    public bool ReadBool()
    {
      bool value = this.rawData[this.position] > 0;
      this.IncreasePosition(1);
      return value;
    }

    public byte ReadByte()
    {
      byte value = this.rawData[this.position];
      this.IncreasePosition(1);
      return value;
    }

    public short ReadShort()
    {
      short value = BitConverter.ToInt16(this.rawData, this.position);
      this.IncreasePosition(2);
      return value;
    }

    public ushort ReadUShort()
    {
      ushort value = BitConverter.ToUInt16(this.rawData, this.position);
      this.IncreasePosition(2);
      return value;
    }

    public int ReadInt()
    {
      int value = BitConverter.ToInt32(this.rawData, this.position);
      this.IncreasePosition(4);
      return value;
    }

    public uint ReadUInt()
    {
      uint value = BitConverter.ToUInt32(this.rawData, this.position);
      this.IncreasePosition(4);
      return value;
    }

    public long ReadLong()
    {
      long value = BitConverter.ToInt64(this.rawData, this.position);
      this.IncreasePosition(8);
      return value;
    }

    public ulong ReadULong()
    {
      ulong value = BitConverter.ToUInt64(this.rawData, this.position);
      this.IncreasePosition(8);
      return value;
    }

    public string ReadString()
    {
      int byteCount = this.ReadUShort();
      string result = 
        Encoding.UTF8.GetString(this.rawData, this.position, byteCount);
      this.IncreasePosition(byteCount);
      return result;
    }
    #endregion
  }
}
