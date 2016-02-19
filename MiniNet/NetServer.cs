using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace MiniNet
{
  public class NetServer : NetConnector
  {
    public event Action<NetPeer> Connected;

    private Dictionary<IPEndPoint, NetPeer> clients;

    public NetServer()
    {
      this.clients = new Dictionary<IPEndPoint, NetPeer>();
    }

    /// <summary>
    /// Starts the socket using the supplied endpoint.
    /// If the port is taken, the given port will be incremented to a free port.
    /// </summary>
    protected void Start(int port)
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

    protected override bool PreProcess(NetPacket packet, IPEndPoint source)
    {
      switch (packet.PacketType)
      {
        case NetPacketType.Connect:
          this.ConnectReceived(source);
          return false;

        case NetPacketType.Message:
          return true;

        default:
          NetDebug.LogWarning("Invalid packet type for server");
          return false;
      }
    }

    private void ConnectReceived(IPEndPoint source)
    {
      NetPeer peer = this.GetPeer(source);
      if (peer == null)
      {
        peer = new NetPeer(source);
        this.AddPeer(peer);

        if (this.Connected != null)
          this.Connected.Invoke(peer);
      }

      peer.QueueOutgoing(this.AllocatePacket(NetPacketType.Connected));
    }
  }
}
