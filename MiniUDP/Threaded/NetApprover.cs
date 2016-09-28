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

using System;
using System.Collections.Generic;
using System.Net;

namespace MiniUDP
{
  /// <summary>
  /// Receives an incoming connection and decides whether to approve or reject
  /// that connection based on user logic and the challenge data received.
  /// 
  /// Note that this approval happens on the BACKGROUND thread, so do not use
  /// any live main-thread data in the approval process unless you really know
  /// what you're doing in terms of multithreading.
  /// </summary>
  public abstract class NetApprover
  {
    private readonly NetByteBuffer reasonBuffer;

    public NetApprover()
    {
      this.reasonBuffer = 
        new NetByteBuffer(NetConst.MAX_PROTOCOL_DATA_SIZE);
    }

    internal bool CheckApproval(
      IPEndPoint source, 
      NetProtocolPacket packet,
      out NetByteBuffer rejectReason)
    {
      this.reasonBuffer.Reset();
      rejectReason = this.reasonBuffer;

      bool result = this.Approve(source, packet.data, this.reasonBuffer);

      packet.data.Rewind(); // We'll want to read it again later
      return result;
    }

    protected abstract bool Approve(
      IPEndPoint source,
      INetByteReader challenge,
      INetByteWriter rejectReason);
  }
}
