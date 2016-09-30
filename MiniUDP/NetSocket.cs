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
using System.Net.Sockets;
using System.Threading;

namespace MiniUDP
{
  public delegate void SocketFailed(SocketException exception);

  public class NetSocket
  {
    public event SocketFailed BindFailed;
    public event SocketFailed ReceiveFailed;
    public event SocketFailed SendFailed;

    internal static Socket CreateRawSocket()
    {
      Socket socket = 
        new Socket(
          AddressFamily.InterNetwork,
          SocketType.Dgram,
          ProtocolType.Udp);

      socket.ReceiveBufferSize = NetConst.SOCKET_BUFFER_SIZE;
      socket.SendBufferSize = NetConst.SOCKET_BUFFER_SIZE;
      socket.Blocking = false;

      try
      {
        const uint IOC_IN = 0x80000000;
        const uint IOC_VENDOR = 0x18000000;
        uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
        socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
      }
      catch
      {
        NetDebug.LogWarning(
          "Failed to set control code for ignoring ICMP port unreachable.");
      }

      return socket;
    }

    private readonly byte[] receiveBuffer;
    private readonly Socket socket;

    internal NetSocket(Socket socket)
    {
      this.receiveBuffer = new byte[NetConst.SOCKET_BUFFER_SIZE];
      this.socket = socket;
    }

    /// <summary>
    /// Starts the socket using the supplied endpoint.
    /// </summary>
    internal bool Bind(int port)
    {
      try
      {
        this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
        return true;
      }
      catch (SocketException exception)
      {
        if (exception.ErrorCode == (int)SocketError.AddressAlreadyInUse)
          NetDebug.LogError("Port " + port + " unavailable");
        else
          NetDebug.LogError(exception.Message);
        this.BindFailed?.Invoke(exception);
        return false;
      }
    }

    internal void Close()
    {
      this.socket.Close();
    }

    /// <summary> 
    /// Attempts to read from OS socket. Returns false if the read fails
    /// or if there is nothing to read.
    /// </summary>
    internal bool TryReceive(
      out IPEndPoint source,
      out byte[] buffer,
      out int length)
    {
      source = null;
      buffer = null;
      length = 0;

      if (this.socket.Poll(0, SelectMode.SelectRead) == false)
        return false;

      try
      {
        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

        length =
          this.socket.ReceiveFrom(
            this.receiveBuffer,
            NetConst.SOCKET_BUFFER_SIZE,
            SocketFlags.None,
            ref endPoint);

        if (length > 0)
        {
          source = endPoint as IPEndPoint;
          buffer = this.receiveBuffer;
          return true;
        }

        return false;
      }
      catch (SocketException exception)
      {
        NetDebug.LogError("Receive failed: " + exception.Message);
        NetDebug.LogError(exception.StackTrace);
        this.ReceiveFailed?.Invoke(exception);
        return false;
      }
    }

    /// <summary> 
    /// Attempts to send data to endpoint via OS socket. 
    /// Returns false if the send failed.
    /// </summary>
    internal bool TrySend(
      IPEndPoint destination,
      byte[] buffer,
      int length)
    {
      try
      {
        int bytesSent =
          this.socket.SendTo(
            buffer,
            length,
            SocketFlags.None,
            destination);
        return (bytesSent == length);
      }
      catch (SocketException exception)
      {
        NetDebug.LogError("Send failed: " + exception.Message);
        NetDebug.LogError(exception.StackTrace);
        this.SendFailed?.Invoke(exception);
        return false;
      }
    }
  }
}
