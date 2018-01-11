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
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MiniUDP
{
  public delegate void NetPeerConnectEvent(
    NetPeer peer, 
    string token);

  public delegate void NetPeerCloseEvent(
    NetPeer peer,
    NetCloseReason reason,
    byte userKickReason,
    SocketError error);
 
  public delegate void NetPeerPayloadEvent(
    NetPeer peer,
    byte[] data,
    ushort dataLength);

  public delegate void NetPeerNotificationEvent(
    NetPeer peer,
    byte[] data,
    ushort dataLength);

  public class NetCore
  {
    public event NetPeerConnectEvent PeerConnected;
    public event NetPeerCloseEvent PeerClosed;
    public event NetPeerPayloadEvent PeerPayload;
    public event NetPeerNotificationEvent PeerNotification;
    
    private readonly NetController controller;
    private Thread controllerThread;

    public NetCore(string version, bool allowConnections)
    {
      if (version == null)
        version = "";
      if (Encoding.UTF8.GetByteCount(version) > NetConfig.MAX_VERSION_BYTES)
        throw new ApplicationException("Version string too long");

      this.controller = new NetController(version, allowConnections);
    }

    public NetPeer Connect(IPEndPoint endpoint, string token)
    {
      NetPeer peer = this.AddConnection(endpoint, token);
      this.Start();
      return peer;
    }

    public void Host(int port)
    {
      this.controller.Bind(port);
      this.Start();
    }

    private void Start()
    {
      this.controllerThread = 
        new Thread(new ThreadStart(this.controller.Start));
      this.controllerThread.IsBackground = true;
      this.controllerThread.Start();
    }

    public NetPeer AddConnection(IPEndPoint endpoint, string token)
    {
      if (token == null)
        token = "";
      if (Encoding.UTF8.GetByteCount(token) > NetConfig.MAX_TOKEN_BYTES)
        throw new ApplicationException("Token string too long");

      NetPeer pending = this.controller.BeginConnect(endpoint, token);
      pending.SetCore(this);
      return pending;
    }

    public void Stop(int timeout = 1000)
    {
      this.controller.Stop();
      this.controllerThread.Join(timeout);
      this.controller.Close();
    }

    public void PollEvents()
    {
      NetEvent evnt;
      while (this.controller.TryReceiveEvent(out evnt))
      {
        NetPeer peer = evnt.Peer;

        // No events should fire if the user closed the peer
        if (peer.ClosedByUser == false)
        {
          switch (evnt.EventType)
          {
            case NetEventType.PeerConnected:
              peer.SetCore(this);
              peer.OnPeerConnected();
              this.PeerConnected?.Invoke(peer, peer.Token);
              break;

            case NetEventType.PeerClosed:
              peer.OnPeerClosed(evnt.CloseReason, evnt.UserKickReason, evnt.SocketError);
              this.PeerClosed?.Invoke(peer, evnt.CloseReason, evnt.UserKickReason, evnt.SocketError);
              break;

            case NetEventType.Payload:
              peer.OnPayloadReceived(evnt.EncodedData, evnt.EncodedLength);
              this.PeerPayload?.Invoke(peer, evnt.EncodedData, evnt.EncodedLength);
              break;

            case NetEventType.Notification:
              peer.OnNotificationReceived(evnt.EncodedData, evnt.EncodedLength);
              this.PeerNotification?.Invoke(peer, evnt.EncodedData, evnt.EncodedLength);
              break;
            
            default:
              throw new NotImplementedException();
          }
        }

        this.controller.RecycleEvent(evnt);
      }
    }

    /// <summary>
    /// Immediately sends out a disconnect message to a peer.
    /// </summary>
    internal void SendKick(NetPeer peer, byte reason)
    {
      this.controller.SendKick(peer, reason);
    }

    /// <summary>
    /// Immediately sends out a payload to a peer.
    /// </summary>
    internal SocketError SendPayload(
      NetPeer peer,
      ushort sequence,
      byte[] data,
      ushort length)
    {
      return this.controller.SendPayload(peer, sequence, data, length);
    }

    /// <summary>
    /// Adds an outgoing notification to the controller processing queue.
    /// </summary>
    internal void QueueNotification(
      NetPeer peer,
      byte[] buffer,
      ushort length)
    {
      this.controller.QueueNotification(peer, buffer, length);
    }
  }
}
