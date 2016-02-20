using System;
using System.Collections.Generic;

using MiniUDP;

internal class Client
{
  private const double HEARTBEAT_RATE = 0.5f;
  private const int BUFFER_SIZE = 2048;

  public event PeerEvent Connected;

  private string hostAddress;
  private NetSocket netSocket;
  private Clock updateClock;
  private NetPeer serverPeer;

  // I/O buffer for reading and writing packet data
  private byte[] buffer;

  private double lastHeartbeat;
  private byte sequence;

  public Client(string hostAddress, double tickRate = 0.02)
  {
    this.hostAddress = hostAddress;
    this.buffer = new byte[BUFFER_SIZE];

    this.netSocket = new NetSocket();
    this.netSocket.UseWhiteList = true;
    this.netSocket.AddToWhiteList(hostAddress);
    this.netSocket.Connected += this.OnConnected;

    this.updateClock = new Clock(tickRate);
    updateClock.OnFixedUpdate += this.OnFixedUpdate;
  }

  public void Run()
  {
    this.netSocket.Connect(this.hostAddress);
    this.updateClock.Start();

    while (true)
      this.updateClock.Tick();
  }

  private void SendHeartbeat()
  {
    if (this.serverPeer != null)
    {
      if ((this.lastHeartbeat + Client.HEARTBEAT_RATE) < Clock.Time)
      {
        this.serverPeer.QueueOutgoing(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, this.sequence }, 10);
        this.lastHeartbeat = Clock.Time;
        this.sequence++;
      }
    }
  }

  private void OnFixedUpdate()
  {
    this.netSocket.Poll();
    this.SendHeartbeat();
    this.netSocket.Transmit();
  }

  private void OnConnected(NetPeer peer)
  {
    Console.WriteLine("Connected to server");
    this.lastHeartbeat = Clock.Time;
    this.serverPeer = peer;

    peer.MessagesWaiting += this.OnPeerMessagesWaiting;
  }

  private void OnPeerMessagesWaiting(NetPeer source)
  {
    foreach (int length in source.ReadReceived(this.buffer))
    {
      byte sequence = this.buffer[9];
      Console.WriteLine("Received " + sequence + " from " + source.ToString());
    }
  }

  private void OnTimedOut()
  {
    Console.WriteLine("Timed out");
  }
}
