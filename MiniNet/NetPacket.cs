using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommonTools;
using UnityEngine;

namespace MiniNet
{
  internal enum NetPacketType : byte
  {
    Invalid = 0,

    Message = 253,
    Connect = 254,
    Disconnect = 255,
  }

  internal class NetPacket : IPoolable
  {
    // Max safe MTU is 1500, so we want to allow room for protocol
    public const int MAX_MESSAGE_SIZE = 1400;

    // Add room for the packet type and packet size
    internal const int MESSAGE_BUFFER_SIZE = MAX_MESSAGE_SIZE + 3;

    #region IPoolable Members
    Pool IPoolable.Pool { get; set; }
    void IPoolable.Reset() { this.Reset(); }
    #endregion

    internal NetPacketType PacketType { get; private set; }

    private int length;
    private byte[] message;

    public int Length 
    {
      get 
      {
        Debug.Assert(this.PacketType == NetPacketType.Message);
        return this.length; 
      }
    }

    public byte[] Message 
    { 
      get
      {
        Debug.Assert(this.PacketType == NetPacketType.Message);
        return this.message;
      }
    }

    public NetPacket()
    {
      this.PacketType = NetPacketType.Invalid;
      this.length = 0;
      this.message = new byte[MESSAGE_BUFFER_SIZE];
    }

    public void Initialize(NetPacketType type)
    {
      this.PacketType = type;
      this.length = 0;
    }

    #region Network I/O
    /// <summary>
    /// Copies the data from the input buffer and stores it internally.
    /// Returns false if the length had bad data.
    /// </summary>
    internal bool NetInput(byte[] sourceBuffer, int receivedBytes)
    {
      Debug.Assert(sourceBuffer.Length >= NetPacket.MESSAGE_BUFFER_SIZE);

      this.PacketType = (NetPacketType)sourceBuffer[0];
      this.length = ((int)sourceBuffer[1] << 8) + sourceBuffer[2];

      if (receivedBytes == (this.length + 3))
      {
        Array.Copy(sourceBuffer, 3, this.message, 0, this.length);
        return true;
      }

      this.PacketType = NetPacketType.Invalid;
      return false;
    }

    /// <summary>
    /// Copies this packet to the given buffer and returns the send length.
    /// </summary>
    internal int NetOutput(byte[] destinationBuffer)
    {
      if (this.PacketType == NetPacketType.Invalid)
        throw new InvalidOperationException("Can't send invalid packet!");

      Debug.Assert(destinationBuffer.Length >= NetPacket.MESSAGE_BUFFER_SIZE);

      destinationBuffer[0] = (byte)this.PacketType;
      destinationBuffer[1] = (byte)(this.length >> 8);
      destinationBuffer[2] = (byte)this.length;
      Array.Copy(this.message, 0, destinationBuffer, 3, this.length);

      return this.length + 3;
    }
    #endregion

    private void Reset()
    {
      this.PacketType = NetPacketType.Invalid;
      this.length = 0;
    }
  }
}
