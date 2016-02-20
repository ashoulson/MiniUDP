using System;
using System.Collections.Generic;

using MiniUDP;

internal class Server
{
  private const int BUFFER_SIZE = 2048;

  private int port;
  private NetSocket netSocket;
  private Clock updateClock;

  // I/O buffer for reading and writing packet data
  private byte[] buffer;

  public Server(int port, double tickRate = 0.02)
  {
    this.port = port;
    this.buffer = new byte[BUFFER_SIZE];

    this.netSocket = new NetSocket();
    this.netSocket.Connected += this.OnConnected;

    this.updateClock = new Clock(tickRate);
    updateClock.OnFixedUpdate += this.OnFixedUpdate;
  }

  public void Run()
  {
    this.netSocket.Bind(this.port);
    this.updateClock.Start();

    while (true)
      this.updateClock.Tick();
  }

  private void OnFixedUpdate()
  {
    this.netSocket.Poll();
    this.netSocket.Transmit();
  }

  private void OnConnected(NetPeer peer)
  {
    Console.WriteLine("Connected: " + peer.ToString());
    peer.MessagesWaiting += this.OnPeerMessagesWaiting;
  }

  private void OnPeerMessagesWaiting(NetPeer source)
  {
    foreach (int length in source.ReadReceived(this.buffer))
    {
      byte sequence = this.buffer[9];
      Console.WriteLine("Received " + sequence + " from " + source.ToString());
      source.QueueOutgoing(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, sequence }, 10);
    }
  }
}
