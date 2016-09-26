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

namespace MiniUDP
{
  #region Read/Write Interfaces
  public interface INetDisconnectRead
  {
    INetByteReader Reason { get; }
  }

  public interface INetDisconnectWrite
  {
    INetByteWriter Reason { get; }
  }
  #endregion

  internal class NetProtocolPacket 
    : INetPoolable<NetProtocolPacket>
    , INetDisconnectRead
    , INetDisconnectWrite
  {
    #region Interface
    void INetPoolable<NetProtocolPacket>.Reset() { this.Reset(); }
    INetByteReader INetDisconnectRead.Reason { get { return this.dataBuffer; } }
    INetByteWriter INetDisconnectWrite.Reason { get { return this.dataBuffer; } }
    #endregion

    private readonly NetByteBuffer dataBuffer;
    private NetProtocolType protocolType;

    public NetProtocolPacket()
    {
      this.dataBuffer = new NetByteBuffer();
      this.Reset();
    }

    private void Initialize(NetProtocolType protocolType)
    {
      this.protocolType = protocolType;
      this.dataBuffer.Reset();

      this.dataBuffer.Write((byte)NetPacketType.Protocol);
      this.dataBuffer.Write((byte)this.protocolType);
    }

    private void Reset()
    {
      this.protocolType = NetProtocolType.INVALID;
      this.dataBuffer.Reset();
    }

    internal void Load(byte[] source, int sourceLength)
    {
      this.dataBuffer.Load(source, sourceLength);

      this.dataBuffer.ReadByte(); // Skip packet type
      this.protocolType = (NetProtocolType)this.dataBuffer.ReadByte();
    }

    internal int Store(byte[] destination)
    {
      return this.dataBuffer.Store(destination);
    }
  }
}
