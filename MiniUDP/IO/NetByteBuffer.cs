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
using System.Text;

namespace MiniUDP
{
  public interface INetByteReader
  {
    int Remaining { get; }

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

    void ReadOut(byte[] destinationBuffer);
    void ReadOut(byte[] destinationBuffer, int count);
  }

  public interface INetByteWriter
  {
    void Write(bool value);
    void Write(byte value);
    void Write(short value);
    void Write(ushort value);
    void Write(int value);
    void Write(uint value);
    void Write(long value);
    void Write(ulong value);
    void Write(string value);

    void Write(byte[] sourceBuffer, int sourceLength);
  }

  public class NetByteBuffer : INetByteReader, INetByteWriter
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

    public int Remaining { get { return this.length - this.position; } }

    private readonly byte[] rawData;
    private int length;
    private int position;

    public NetByteBuffer()
    {
      this.rawData = new byte[NetConfig.DATA_BUFFER_SIZE];
      this.Reset();
    }

    internal void Load(byte[] source, int sourceLength)
    {
      Buffer.BlockCopy(source, 0, this.rawData, 0, sourceLength);
      this.length = sourceLength;
      this.position = 0;
    }

    internal int Store(byte[] destination)
    {
      Buffer.BlockCopy(this.rawData, 0, destination, 0, this.length);
      return this.length;
    }

    internal void Reset()
    {
      this.length = 0;
      this.position = 0;
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

    public void Write(string value)
    {
      int stringLength = Encoding.UTF8.GetByteCount(value);
      if (stringLength > ushort.MaxValue)
        throw new OverflowException("stringLength");

      Encoding.UTF8.GetBytes(
        value, 
        0, 
        value.Length,
        this.rawData, 
        this.length);
      this.length += stringLength;
    }

    public void Write(byte[] sourceBuffer, int sourceLength)
    {
      if (sourceLength > this.rawData.Length)
        throw new OverflowException("sourceBuffer");

      Buffer.BlockCopy(
        sourceBuffer, 
        0, 
        this.rawData,
        this.length, 
        sourceLength);
      this.length += sourceLength;
    }
    #endregion

    #region Read
    public byte PeekByte()
    {
      this.CheckPosition(1);
      return this.rawData[this.position];
    }

    public bool ReadBool()
    {
      this.CheckPosition(1);
      bool value = this.rawData[this.position] > 0;
      this.position += 1;
      return value;
    }

    public byte ReadByte()
    {
      this.CheckPosition(1);
      byte value = this.rawData[this.position];
      this.position += 1;
      return value;
    }

    public short ReadShort()
    {
      this.CheckPosition(2);
      short value = BitConverter.ToInt16(this.rawData, this.position);
      this.position += 2;
      return value;
    }

    public ushort ReadUShort()
    {
      this.CheckPosition(2);
      ushort value = BitConverter.ToUInt16(this.rawData, this.position);
      this.position += 2;
      return value;
    }

    public int ReadInt()
    {
      this.CheckPosition(4);
      int value = BitConverter.ToInt32(this.rawData, this.position);
      this.position += 4;
      return value;
    }

    public uint ReadUInt()
    {
      this.CheckPosition(4);
      uint value = BitConverter.ToUInt32(this.rawData, this.position);
      this.position += 4;
      return value;
    }

    public long ReadLong()
    {
      this.CheckPosition(8);
      long value = BitConverter.ToInt64(this.rawData, this.position);
      this.position += 8;
      return value;
    }

    public ulong ReadULong()
    {
      this.CheckPosition(8);
      ulong value = BitConverter.ToUInt64(this.rawData, this.position);
      this.position += 8;
      return value;
    }

    public string ReadString()
    {
      int bytesCount = this.ReadUShort();
      this.CheckPosition(bytesCount);

      string result =
        Encoding.UTF8.GetString(this.rawData, this.position, bytesCount);
      this.position += bytesCount;
      return result;
    }

    public void ReadOut(byte[] destinationBuffer)
    {
      this.ReadOut(destinationBuffer, this.Remaining);
    }

    public void ReadOut(byte[] destinationBuffer, int count)
    {
      Buffer.BlockCopy(
        this.rawData, 
        this.position, 
        destinationBuffer, 
        0, 
        count);
    }

    private void CheckPosition(int size)
    {
      if ((this.position + size) > this.length)
        throw new OverflowException("position");
    }
    #endregion
  }
}
