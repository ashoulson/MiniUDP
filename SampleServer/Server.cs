using System;
using System.Collections.Generic;

using MiniUDP;

internal class Server
{
  private const int BUFFER_SIZE = 2048;
  private const float TICK_RATE = 0.02f;

  private int port;
  private NetSocket netSocket;
  private Clock updateClock;

  // I/O buffer for reading and writing packet data
  private byte[] buffer;

  public Server(int port)
  {
    this.port = port;
    this.buffer = new byte[BUFFER_SIZE];

    this.netSocket = new NetSocket();
    this.netSocket.Connected += this.OnConnected;
    this.netSocket.Disconnected += this.OnDisconnected;
    this.netSocket.TimedOut += this.OnTimedOut;

    this.updateClock = new Clock(Server.TICK_RATE);
    updateClock.OnFixedUpdate += this.OnFixedUpdate;
  }

  public void Start()
  {
    this.netSocket.Bind(this.port);
    this.updateClock.Start();
  }

  public void Update()
  {
    this.updateClock.Tick();
  }

  public void Stop()
  {
    this.netSocket.Shutdown();
    this.netSocket.Transmit();
  }

  private void OnFixedUpdate()
  {
    Console.WriteLine(this.netSocket.time.Second);
    this.netSocket.Poll();
    this.netSocket.Transmit();
  }

  private void OnConnected(NetPeer peer)
  {
    Console.WriteLine("Connected: " + peer.ToString() + " (" + this.netSocket.PeerCount + ")");
    peer.MessagesReady += this.OnPeerMessagesReady;
  }

  private void OnDisconnected(NetPeer peer)
  {
    Console.WriteLine("Disconnected: " + peer.ToString() + " (" + this.netSocket.PeerCount + ")");
  }

  void OnTimedOut(NetPeer peer)
  {
    Console.WriteLine("Timed Out: " + peer.ToString() + " (" + this.netSocket.PeerCount + ")");
  }

  private void OnPeerMessagesReady(NetPeer source)
  {
    foreach (int length in source.ReadReceived(this.buffer))
    {
      byte sequence = this.buffer[9];
      //Console.WriteLine(
      //  "Received " + 
      //  sequence + 
      //  " from " + 
      //  source.ToString() + 
      //  " " + 
      //  source.Statistics.GetPing() + 
      //  "ms " + 
      //  (source.Statistics.GetLoss(25) * 100.0f) + 
      //  "%");
      source.EnqueueSend(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, sequence }, 10);
    }
  }
}
