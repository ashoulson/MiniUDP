//using System;
//using System.Threading;
//using System.Collections.Generic;

//using MiniUDP;

//internal class Client
//{
//  private const double HEARTBEAT_RATE = 0.1f;
//  private const int BUFFER_SIZE = 2048;

//  private string hostAddress;
//  private NetSocket netSocket;
//  private Clock updateClock;
//  private NetPeer serverPeer;

//  // I/O buffer for reading and writing packet data
//  private byte[] buffer;

//  private double lastHeartbeat;
//  private byte sequence;

//  public Client(string hostAddress, double tickRate = Client.HEARTBEAT_RATE)
//  {
//    this.hostAddress = hostAddress;
//    this.buffer = new byte[BUFFER_SIZE];

//    this.netSocket = new NetSocket();
//    this.netSocket.UseWhiteList = true;
//    this.netSocket.AddToWhiteList(hostAddress);
//    this.netSocket.Connected += this.OnConnected;
//    this.netSocket.Disconnected += this.OnDisconnected;
//    this.netSocket.TimedOut += this.OnTimedOut;
//    this.netSocket.ConnectFailed += this.OnConnectFailed;

//    this.updateClock = new Clock(tickRate);
//    updateClock.OnFixedUpdate += this.OnFixedUpdate;
//  }

//  public void Start()
//  {
//    this.netSocket.Connect(this.hostAddress);
//    this.updateClock.Start();
//  }

//  public void Update()
//  {
//    this.netSocket.Receive();
//    this.updateClock.Tick();
//    this.netSocket.Transmit();
//    Thread.Sleep(1);
//  }

//  public void Stop()
//  {
//    this.netSocket.Shutdown();
//    this.netSocket.Transmit();
//  }

//  private void SendHeartbeat()
//  {
//    if (this.serverPeer != null)
//    {
//      if ((this.lastHeartbeat + Client.HEARTBEAT_RATE) < Clock.Time)
//      {
//        this.serverPeer.EnqueueSend(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, this.sequence }, 10);
//        this.lastHeartbeat = Clock.Time;
//        this.sequence++;
//      }
//    }
//  }

//  private void OnFixedUpdate()
//  {
//    this.netSocket.Poll();
//    this.SendHeartbeat();
//  }

//  private void OnConnected(NetPeer peer)
//  {
//    Console.WriteLine("Connected to server");
//    this.lastHeartbeat = Clock.Time;
//    this.serverPeer = peer;

//    peer.MessagesReady += this.OnPeerMessagesReady;
//  }

//  private void OnDisconnected(NetPeer peer)
//  {
//    Console.WriteLine("Disconnected from server");
//    this.serverPeer = null;
//  }

//  private void OnPeerMessagesReady(NetPeer peer)
//  {
//    foreach (int length in peer.ReadReceived(this.buffer))
//    {
//      byte sequence = this.buffer[9];
//      Console.WriteLine("Received " + sequence + " from " + peer.ToString());
//    }
//  }

//  private void OnTimedOut(NetPeer peer)
//  {
//    Console.WriteLine("Server connection timed out");
//    this.serverPeer = null;
//  }

//  private void OnConnectFailed(string address)
//  {
//    Console.WriteLine("Connection to " + address + " failed, retrying...");
//    this.netSocket.Connect(address);
//  }
//}
