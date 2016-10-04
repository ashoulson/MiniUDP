/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2015-2016 - Alexander Shoulson - http://ashoulson.com
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
using System.Net.Sockets;
using System.Text;

using MiniUDP;

namespace SampleCommon
{
  public class Connector
  {
    private readonly NetCore connection;

    public Connector(string version, bool allowConnections)
    {
      this.connection = new NetCore(version, allowConnections);
      this.connection.PeerConnected += Connection_PeerConnected;
    }

    public void Update()
    {
      this.connection.PollEvents();
    }

    private void Connection_PeerConnected(NetPeer peer, string token)
    {
      Console.WriteLine(peer.EndPoint + " peer connected: " + token);

      peer.PeerClosedError += Peer_PeerClosedError;
      peer.PeerClosedTimeout += Peer_PeerClosedTimeout;
      peer.PeerClosedShutdown += Peer_PeerClosedShutdown;
      peer.PeerClosedKicked += Peer_PeerClosedKicked;
      peer.PayloadReceived += Peer_PayloadReceived;
      peer.NotificationReceived += Peer_NotificationReceived;
    }

    private void Peer_PayloadReceived(NetPeer peer, byte[] data, int dataLength)
    {
      //Console.WriteLine(peer.EndPoint + " got payload: \"" + Encoding.UTF8.GetString(data, 0, dataLength) + "\"");
    }

    private void Peer_NotificationReceived(NetPeer peer, byte[] data, int dataLength)
    {
      Console.WriteLine(peer.EndPoint + " got notification: \"" + Encoding.UTF8.GetString(data, 0, dataLength) + "\"");
      Console.WriteLine(
        peer.Traffic.Ping + "ms " + 
        (peer.Traffic.LocalLoss * 100.0f) + "% " + 
        (peer.Traffic.RemoteLoss * 100.0f) + "% " +
        (peer.Traffic.LocalDrop * 100.0f) + "% " +
        (peer.Traffic.RemoteDrop * 100.0f) + "%");
    }

    private void Peer_PeerClosedTimeout(NetPeer peer)
    {
      Console.WriteLine(peer.EndPoint + " peer closed due to timeout");
    }

    private void Peer_PeerClosedError(NetPeer peer, SocketError error)
    {
      Console.WriteLine(peer.EndPoint + " peer closed due to error: " + error);
    }

    private void Peer_PeerClosedShutdown(NetPeer peer)
    {
      Console.WriteLine(peer.EndPoint + " peer closed due to shutdown");
    }

    private void Peer_PeerClosedKicked(NetPeer peer, NetKickReason reason, byte userReason)
    {
      if (reason == NetKickReason.User)
        Console.WriteLine(peer.EndPoint + " peer closed remotely due to user reason: " + userReason);
      else
        Console.WriteLine(peer.EndPoint + " peer closed remotely due to internal reason: " + reason);
    }

    public void Host(int port)
    {
      this.connection.Host(port);
    }

    public NetPeer Connect(string address, string token = "")
    {
      NetPeer host = 
        this.connection.Connect(NetUtil.StringToEndPoint(address), token);

      host.ConnectTimedOut += Host_ConnectTimedOut;
      host.ConnectAccepted += Host_ConnectAccepted;
      host.ConnectRejected += Host_ConnectRejected;

      // TODO: This is really inconvenient. Consolidate some of these,
      // especially the Connect/ConnectAccepted events
      host.PeerClosedError += Peer_PeerClosedError;
      host.PeerClosedTimeout += Peer_PeerClosedTimeout;
      host.PeerClosedShutdown += Peer_PeerClosedShutdown;
      host.PeerClosedKicked += Peer_PeerClosedKicked;
      host.PayloadReceived += Peer_PayloadReceived;
      host.NotificationReceived += Peer_NotificationReceived;

      return host;
    }

    private void Host_ConnectTimedOut(NetPeer peer)
    {
      Console.WriteLine(peer.EndPoint + " connection attempt timed out");
    }

    private void Host_ConnectAccepted(NetPeer peer, string token)
    {
      Console.WriteLine(peer.EndPoint + " connection accepted: " + token);
    }

    private void Host_ConnectRejected(NetPeer peer, NetRejectReason reason)
    {
      Console.WriteLine(peer.EndPoint + " connection rejected: " + reason);
    }

    public void Stop()
    {
      this.connection.Stop();
    }
  }
}