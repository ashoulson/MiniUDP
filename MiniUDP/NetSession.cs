using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace MiniUDP
{
  internal class NetSession
  {
    private readonly Dictionary<IPEndPoint, NetPeer> peers;
    private readonly Queue<NetNotification> pending;
    private readonly Queue<NetNotification> processing;
    private readonly NetSocketIO socket;
    private readonly Stopwatch timer;

    private readonly NetSessionPacket sessionReusable;

    public NetSession(Socket socket)
    {
      this.peers = new Dictionary<IPEndPoint, NetPeer>();
      this.pending = new Queue<NetNotification>();
      this.processing = new Queue<NetNotification>();
      this.socket = new NetSocketIO(socket);
      this.timer = new Stopwatch();

      this.sessionReusable = new NetSessionPacket();
    }

    /// <summary>
    /// Called from the main thread
    /// </summary>
    public void AddNotification(NetPeer target, NetNotification notification)
    {
      notification.Target = target;
      lock (this.pending)
        this.pending.Enqueue(notification);
    }

    /// <summary>
    /// Receives notifications from the main thread and assigns them to peers
    /// </summary>
    private void ReadPendingNotifications()
    {
      // Consume everything
      lock (this.pending)
        while (this.pending.Count > 0)
          this.processing.Enqueue(this.pending.Dequeue());

      // Dispatch to peers
      while (this.processing.Count > 0)
      {
        NetNotification notification = this.processing.Dequeue();
        if ((notification.Target != null) && notification.Target.IsConnected)
          notification.Target.QueueNotification(notification);
      }
    }

    /// <summary>
    /// Sends a session packet to the given peer.
    /// </summary>
    private bool SendSessionPacket(NetPeer peer)
    {
      this.sessionReusable.remoteLoss = 0; // TODO
      this.sessionReusable.notifyAck = 0; // TODO
      this.sessionReusable.pingSequence = 0; // TODO
      this.sessionReusable.pongSequence = 0; // TODO
      this.sessionReusable.pongProcessTime = 0; // TODO

      foreach (NetNotification notification in peer.outgoing)
        if (this.sessionReusable.TryAdd(notification) == false)
          break;

      bool result = this.socket.TrySend(peer.EndPoint, this.sessionReusable);
      this.sessionReusable.Reset();
      return result;
    }
  }
}
