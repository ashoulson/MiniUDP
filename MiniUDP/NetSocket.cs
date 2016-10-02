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
  public delegate void SocketFailed(SocketException exception);

  internal interface INetSocketReader
  {
    SocketError TryReceive(
      out IPEndPoint source,
      out byte[] buffer,
      out int length);
  }

  internal interface INetSocketWriter
  {
    SocketError TrySend(
      IPEndPoint destination,
      byte[] buffer,
      int length);
  }

  /// <summary>
  /// Since raw sockets are thread safe, we use a global socket singleton
  /// between the two threads for the sake of convenience.
  /// </summary>
  internal class NetSocket
  {
    private class Reader : INetSocketReader
    {
      private readonly NetSocket socket;
      private readonly byte[] receiveBuffer;

      internal Reader(NetSocket socket)
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

    private class Writer : INetSocketWriter
    {
      private readonly NetSocket socket;

      internal Writer(NetSocket socket)
      {
        this.socket = socket;
      }

      public SocketError TrySend(
        IPEndPoint destination,
        byte[] buffer,
        int length)
      {
        return this.socket.TrySend(destination, buffer, length);
      }
    }

    public static bool Succeeded(SocketError error)
    {
      return (error == SocketError.Success);
    }

    public static bool Empty(SocketError error)
    {
      return (error == SocketError.NoData);
    }

    // https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.aspx
    // We don't need a lock for writing, but we do for reading because polling
    // and receiving are two different non-atomic actions. In practice we
    // should only ever be reading from the socket on one thread anyway.
    private object readLock;
    private Socket rawSocket;

    internal NetSocket()
    {
      this.readLock = new object();
      this.rawSocket =
        new Socket(
          AddressFamily.InterNetwork,
          SocketType.Dgram,
          ProtocolType.Udp);

      this.rawSocket.ReceiveBufferSize = NetConfig.SOCKET_BUFFER_SIZE;
      this.rawSocket.SendBufferSize = NetConfig.SOCKET_BUFFER_SIZE;
      this.rawSocket.Blocking = false;

      try
      {
        // Ignore port unreachable (connection reset by remote host)
        const uint IOC_IN = 0x80000000;
        const uint IOC_VENDOR = 0x18000000;
        uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
        this.rawSocket.IOControl(
          (int)SIO_UDP_CONNRESET, 
          new byte[] { 0 }, 
          null);
      }
      catch
      {
        // Not always supported
        NetDebug.LogWarning(
          "Failed to set control code for ignoring ICMP port unreachable.");
      }
    }

    internal INetSocketReader CreateReader()
    {
      return new Reader(this);
    }

    internal INetSocketWriter CreateWriter()
    {
      return new Writer(this);
    }

    internal SocketError Bind(int port)
    {
      try
      {
        this.rawSocket.Bind(new IPEndPoint(IPAddress.Any, port));
      }
      catch (SocketException exception)
      {
        return exception.SocketErrorCode;
      }
      return SocketError.Success;
    }

    internal void Close()
    {
      this.rawSocket.Close();
    }

    /// <summary> 
    /// Attempts to send data to endpoint via OS socket. 
    /// Returns false if the send failed.
    /// </summary>
    private SocketError TrySend(
      IPEndPoint destination,
      byte[] buffer,
      int length)
    {
      try
      {
        int bytesSent =
          this.rawSocket.SendTo(
            buffer,
            length,
            SocketFlags.None,
            destination);
        if (bytesSent == length)
          return SocketError.Success;
        return SocketError.MessageSize;
      }
      catch (SocketException exception)
      {
        NetDebug.LogError("Send failed: " + exception.Message);
        NetDebug.LogError(exception.StackTrace);
        return exception.SocketErrorCode;
      }
    }

    /// <summary> 
    /// Attempts to read from OS socket. Returns false if the read fails
    /// or if there is nothing to read.
    /// </summary>
    private SocketError TryReceive(
      out IPEndPoint source,
      byte[] destBuffer,
      out int length)
    {
      source = null;
      length = 0;

      lock (this.readLock)
      {
        if (this.rawSocket.Poll(0, SelectMode.SelectRead) == false)
          return SocketError.NoData;

        try
        {
          EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

          length =
            this.rawSocket.ReceiveFrom(
              destBuffer,
              destBuffer.Length,
              SocketFlags.None,
              ref endPoint);

          if (length > 0)
          {
            source = endPoint as IPEndPoint;
            return SocketError.Success;
          }

          return SocketError.NoData;
        }
        catch (SocketException exception)
        {
          NetDebug.LogError("Receive failed: " + exception.Message);
          NetDebug.LogError(exception.StackTrace);
          return exception.SocketErrorCode;
        }
      }
    }
  }
}
