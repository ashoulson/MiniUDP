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

using System.Net;
using System.Net.Sockets;

namespace MiniUDP
{
  /// <summary>
  /// A class for receiving and buffering data from a socket. Note that this
  /// class is NOT thread safe and must not be shared across threads.
  /// </summary>
  internal class NetReceiver
  {
    private readonly NetSocket socket;
    private readonly byte[] receiveBuffer;

    internal NetReceiver(NetSocket socket)
    {
      this.socket = socket;
      this.receiveBuffer = new byte[NetConfig.SOCKET_BUFFER_SIZE];
    }

    public SocketError TryReceive(
      out IPEndPoint source,
      out byte[] buffer,
      out int length)
    {
      buffer = this.receiveBuffer;
      return
        this.socket.TryReceive(
          out source,
          this.receiveBuffer,
          out length);
    }
  }
}
