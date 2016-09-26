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
  public delegate void SocketError(Exception exception);

  public class NetSocketIO
  {
    public static NetSocketIO Create()
    {
      Socket socket = 
        new Socket(
          AddressFamily.InterNetwork,
          SocketType.Dgram,
          ProtocolType.Udp);
      NetSocketIO.ConfigureSocket(socket);
      return new NetSocketIO(socket);
    }

    /// <summary>
    /// Configures a socket for sending/receiving data.
    /// 
    /// Should only be called on the main thread.
    /// </summary>
    private static void ConfigureSocket(Socket socket)
    {
      socket.ReceiveBufferSize = NetConfig.SOCKET_BUFFER_SIZE;
      socket.SendBufferSize = NetConfig.SOCKET_BUFFER_SIZE;
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
    }

    private readonly byte[] dataBuffer;
    private readonly Socket socket;

    private NetSocketIO(Socket socket)
    {
      this.dataBuffer = new byte[NetConfig.SOCKET_BUFFER_SIZE];
      this.socket = socket;
    }

    public NetSocketIO Clone()
    {
      return new NetSocketIO(this.socket);
    }

    /// <summary>
    /// Starts the socket using the supplied endpoint.
    /// 
    /// Should only be called on the main thread.
    /// </summary>
    public void Bind(int port)
    {
      try
      {
        this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
      }
      catch (SocketException exception)
      {
        if (exception.ErrorCode == 10048)
          NetDebug.LogError("Port " + port + " unavailable!");
        else
          NetDebug.LogError(exception.Message);
        return;
      }
    }

    /// <summary> 
    /// Attempts to read from OS socket. Returns false if the read fails
    /// or if there is nothing to read.
    /// </summary>
    private bool TryReceive(
      out IPEndPoint source,
      out byte[] data,
      out int receivedBytes)
    {
      source = null;
      data = null;
      receivedBytes = 0;

      if (this.socket.Poll(0, SelectMode.SelectRead) == false)
        return false;

      try
      {
        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
        receivedBytes =
          this.socket.ReceiveFrom(
            this.dataBuffer,
            this.dataBuffer.Length,
            SocketFlags.None,
            ref endPoint);

        if (receivedBytes > 0)
        {
          source = endPoint as IPEndPoint;
          data = this.dataBuffer;
          return true;
        }

        return false;
      }
      catch (Exception exception)
      {
        NetDebug.LogError("Receive failed: " + exception.Message);
        NetDebug.LogError(exception.StackTrace);
        return false;
      }
    }

    /// <summary> 
    /// Attempts to send data to endpoint via OS socket. 
    /// Returns false if the send failed.
    /// </summary>
    private bool TrySend(
      IPEndPoint destination,
      byte[] data,
      int sendBytes)
    {
      try
      {
        int bytesSent =
          this.socket.SendTo(
            data,
            sendBytes,
            SocketFlags.None,
            destination);

        return (bytesSent == sendBytes);
      }
      catch (Exception exception)
      {
        NetDebug.LogError("Send failed: " + exception.Message);
        NetDebug.LogError(exception.StackTrace);
        return false;
      }
    }
  }
}
