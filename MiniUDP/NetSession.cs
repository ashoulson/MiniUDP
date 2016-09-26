using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace MiniUDP
{
  internal class NetSession
  {
    private readonly Dictionary<IPEndPoint, NetPeer> peers;

    private readonly Queue<NetNotifyData> pending;
    private readonly Queue<NetNotifyData> processing;
    private readonly NetSocketIO socket;

    public NetSession(NetSocketIO socket)
    {
      this.pending = new Queue<NetNotifyData>();
      this.processing = new Queue<NetNotifyData>();
      this.socket = socket;
    }

    public void AddNotify(NetNotifyData notify)
    {
      lock (this.pending)
        this.pending.Enqueue(notify);
    }

    private void ReadPendingNotifications()
    {
      // Consume everything
      lock (this.pending)
        while (this.pending.Count > 0)
          this.processing.Enqueue(this.pending.Dequeue());

      // Dispatch to peers
      while (this.processing.Count > 0)
      {
        NetNotifyData notification = this.processing.Dequeue();
        if ((notification.Target != null) && notification.Target.IsConnected)
          notification.Target.QueueNotification(notification);
      }
    }
  }
}
