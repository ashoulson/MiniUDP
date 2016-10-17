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
using System.Net;

namespace MiniUDP
{
  public static class NetUtil
  {
    /// <summary>
    /// Validates that a given kick reason is acceptable for a remote kick.
    /// </summary>
    internal static NetCloseReason ValidateKickReason(NetCloseReason reason)
    {
      switch (reason)
      {
        case NetCloseReason.RejectNotHost:
          return reason;
        case NetCloseReason.RejectFull:
          return reason;
        case NetCloseReason.RejectVersion:
          return reason;
        case NetCloseReason.KickTimeout:
          return reason;
        case NetCloseReason.KickShutdown:
          return reason;
        case NetCloseReason.KickError:
          return reason;
        case NetCloseReason.KickUserReason:
          return reason;
      }

      NetDebug.LogError("Bad kick reason: " + reason);
      return NetCloseReason.INVALID;
    }

    /// <summary>
    /// Compares two bytes a - b with wrap-around arithmetic.
    /// </summary>
    internal static int ByteSeqDiff(byte a, byte b)
    {
      // Assumes a sequence is 8 bits
      return ((a << 24) - (b << 24)) >> 24;
    }

    /// <summary>
    /// Compares two ushorts a - b with wrap-around arithmetic.
    /// </summary>
    internal static int UShortSeqDiff(ushort a, ushort b)
    {
        // Assumes a stamp is 16 bits
        return ((a << 16) - (b << 16)) >> 16;
    }

    /// <summary>
    /// Returns an IPv4 IP.IP.IP.IP string as an IPEndpoint.
    /// </summary>
    public static IPEndPoint IPToEndPoint(string ip, int port)
    {
      return new IPEndPoint(IPAddress.Parse(ip), port);
    }

    /// <summary>
    /// Returns an DNS address string as an IPEndpoint.
    /// </summary>
    public static IPEndPoint AddressToEndPoint(string address, int port)
    {
      IPAddress[] addresses = Dns.GetHostAddresses(address);
      if (addresses.Length == 1)
        return new IPEndPoint(addresses[0], port);
      throw new ArgumentException(
        "Failed to uniquely resolve address: " + 
        address);
    }
  }
}
