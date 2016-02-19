using System;
using System.Collections.Generic;

using System.Net;
using System.Net.Sockets;

using CommonTools;
using UnityEngine;

namespace MiniNet
{
  public class NetSocket
  {
    private const int MAX_BUFFER_SIZE = 2048;
    private const float CONNECTION_RETRY_RATE = 0.5f;
    private const int CONNECTION_MAX_RETRIES = 20;

    #region Internal Classes
    private class PendingConnection : IPoolable
    {
      #region IPoolable Members
      Pool IPoolable.Pool { get; set; }
      void IPoolable.Reset() { this.Reset(); }
      #endregion

      public IPEndPoint EndPoint { get; set; }
      public NetPacket Packet { get; set; }

      public float LastAttempt { get; set; }
      public int Retries { get; set; }

      public PendingConnection()
      {
        this.Reset();
      }

      public void Initialize(
        IPEndPoint endPoint,
        NetPacket packet)
      {
        this.EndPoint = endPoint;
        this.Packet = packet;
        this.LastAttempt = float.NegativeInfinity;
        this.Retries = CONNECTION_MAX_RETRIES;
      }

      /// <summary>
      /// Returns true iff it's time to retry.
      /// </summary>
      public bool RetryDue(float currentTime)
      {
        return (currentTime > (this.LastAttempt + CONNECTION_RETRY_RATE));
      }

      /// <summary>
      /// Returns true iff we should keep retrying.
      /// </summary>
      public bool LogRetry(float currentTime)
      {
        this.Retries--;
        this.LastAttempt = currentTime;

        return (this.Retries >= 0);
      }

      private void Reset()
      {
        if (this.Packet != null)
          Pool.Free(this.Packet);

        this.EndPoint = null;
        this.Packet = null;
        this.LastAttempt = float.NegativeInfinity;
        this.Retries = NetSocket.CONNECTION_MAX_RETRIES;
      }
    }
    #endregion

    #region Static Methods
    public static IPEndPoint StringToEndPoint(string address)
    {
      string[] split = address.Split(':');
      string stringAddress = split[0];
      string stringPort = split[1];

      int port = int.Parse(stringPort);
      IPAddress ipaddress = IPAddress.Parse(stringAddress);
      IPEndPoint endpoint = new IPEndPoint(ipaddress, port);

      if (endpoint == null) 
        throw new ArgumentException("Failed to parse address: " + address);
      return endpoint;
    }
    #endregion

    #region Properties and Fields
    private byte[] dataBuffer;

    private Socket netSocket;
    private Dictionary<IPEndPoint, PendingConnection> pendingConnections;

    private GenericPool<NetPacket> packetPool;
    private GenericPool<PendingConnection> pendingConnectionPool;
    #endregion

    public NetSocket()
    {
      this.dataBuffer = new byte[NetSocket.MAX_BUFFER_SIZE];

      this.netSocket = null;
      this.pendingConnections = 
        new Dictionary<IPEndPoint, PendingConnection>();

      this.packetPool = new GenericPool<NetPacket>();
      this.pendingConnectionPool = new GenericPool<PendingConnection>();
    }

    /// <summary>
    /// Starts the socket using the supplied endpoint.
    /// If the port is taken, the given port will be incremented to a free port.
    /// </summary>
    public void StartSocket(int port)
    {
      this.netSocket =
        new Socket(
          AddressFamily.InterNetwork,
          SocketType.Dgram,
          ProtocolType.Udp);

      this.netSocket.ReceiveBufferSize = NetSocket.MAX_BUFFER_SIZE;
      this.netSocket.SendBufferSize = NetSocket.MAX_BUFFER_SIZE;
      this.netSocket.Blocking = false;

      try
      {
        this.netSocket.Bind(new IPEndPoint(IPAddress.Any, port));
      }
      catch (SocketException exception)
      {
        if (exception.ErrorCode == 10048)
          Debug.LogWarning("Port " + port + " unavailable!");
        else
          Debug.LogError(exception.Message);
        return;
      }
    }

    public void Connect(IPEndPoint endPoint)
    {
      // We don't actually send out a packet immediately, but we add the
      // connection request to the set of pending connection requests to be
      // sent out next time we cycle through them (at a fixed frequency).

      if (this.pendingConnections.ContainsKey(endPoint) == false)
      {
        NetPacket packet = this.packetPool.Allocate();
        PendingConnection pending = this.pendingConnectionPool.Allocate();

        packet.Initialize(NetPacketType.Connect);
        pending.Initialize(endPoint, packet);

        this.pendingConnections.Add(endPoint, pending);
      }
    }

    private void RetryConnections(float currentTime)
    {
      List<PendingConnection> toRemove = new List<PendingConnection>();
      
      foreach (PendingConnection pending in this.pendingConnections.Values)
      {
        if (pending.RetryDue(currentTime))
        {
          this.TrySend(pending.Packet, pending.EndPoint);
          if (pending.LogRetry(currentTime) == false)
            toRemove.Add(pending);
        }
      }

      foreach (PendingConnection pending in toRemove)
        this.pendingConnections.Remove(pending.EndPoint);
    }

    /// <summary> 
    /// The starting point for incoming data. Attempts to read from OS socket.
    /// Returns false if the read failed or there was nothing to read.
    /// </summary>
    private NetPacket TryReceive(out IPEndPoint source)
    {
      source = null;

      try
      {
        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
        int receiveCount = 
          this.netSocket.ReceiveFrom(
            this.dataBuffer,
            this.dataBuffer.Length, 
            SocketFlags.None, 
            ref endPoint);

        if (receiveCount > 0)
        {
          source = endPoint as IPEndPoint;
          NetPacket packet = this.packetPool.Allocate();
          if (packet.NetInput(this.dataBuffer, receiveCount))
            return packet;
        }

        return null;
      }
      catch
      {
        return null;
      }
    }

    /// <summary> 
    /// Attempts to send data to endpoint via OS socket. 
    /// Returns false if the send failed.
    /// </summary>
    private bool TrySend(
      NetPacket packet,
      IPEndPoint destination)
    {
      try
      {
        int bytesToSend = packet.NetOutput(this.dataBuffer);
        int bytesSent = 
          this.netSocket.SendTo(
            this.dataBuffer,
            bytesToSend, 
            SocketFlags.None, 
            destination);

        return (bytesSent == bytesToSend);
      }
      catch
      {
        return false;
      }
    }











    ///// <summary> All active connections. Includes both client and server connections. </summary>
    //internal readonly List<NetConnection> Connections = new List<NetConnection>();

    //private readonly Dictionary<IPEndPoint, NetConnection> endpointToConnection =
    //    new Dictionary<IPEndPoint, NetConnection>();

    //private readonly List<NetStream> connectingData = new List<NetStream>();
    //private readonly List<uint> connectingTimes = new List<uint>();
    //private readonly List<int> connectingRetriesRemaining = new List<int>();

    ///// <summary> Dummy NetConnection used to represent self for authority checks. </summary>
    //internal NetConnection Self;

    ///// <summary> Returns the port number for this socket. 0 if socket not yet initialized. </summary>
    //public int Port
    //{
    //  get { return Self.Endpoint.Port; }
    //}

    ///// <summary> Returns the IP address and port this socket is listening on. E.g., "192.168.1.1:17603" </summary>
    //public string Address
    //{
    //  get { return Self.Endpoint.Address.ToString(); }
    //}

    ///// <summary> The current number of total connections. Includes clients, servers, and peers.
    ///// Compared against MaxConnections to determine if incoming connections should be refused. </summary>
    //public int ConnectionCount { get { return Connections.Count; } }

    ///// <summary> If ConnectionCount == MaxConnections, incoming connections will be refused. Outgoing connections
    ///// are counted in ConnectionCount, but are allowed to exceed MaxConnections. </summary>
    //public int MaxConnections { get; set; }

    ///// <summary> Sets Unity's Application.targetFrameRate. It is recommended to set this to a resonable
    ///// number such as 60 so that timing-related functionality remains more consistent. </summary>
    //public int TargetFrameRate = 60;

    ///// <summary> All incoming connections are refused when set to false. Clients should be false. </summary>
    //public bool AcceptConnections { get; set; }

    ///// <summary> Starts the socket using an automatically selected endpoint. </summary>
    //public void StartSocket()
    //{
    //  StartSocket(new IPEndPoint(IPAddress.Any, 0));
    //}

    ///// <summary>
    ///// Starts the socket using an address in the following format: "192.168.1.1:17010"
    ///// If the port is taken, the given port will be incremented to a free port.
    ///// </summary>
    //public void StartSocket(string fullAddress)
    //{
    //  StartSocket(StringToEndPoint(fullAddress));
    //}


    ///// <summary> Closes the socket and performs cleanup for active connections. </summary>
    //public void Shutdown()
    //{
    //  DisconnectAll();
    //  socket.Close();
    //  socket = null;
    //}

    ///// <summary> Byte values for connection control commands. </summary>
    //private enum ByteCmd : byte
    //{
    //  Connect = 245,
    //  ConnectToPeer = 244,
    //  RefuseConnection = 243,
    //  Disconnect = 240
    //}

    ///// <summary> Attempts to connect to a server with address format: "192.168.1.1:17001" </summary>
    //public void Connect(string fullAddress)
    //{
    //  Connect(StringToEndPoint(fullAddress));
    //}

    ///// <summary> Attempts to connect to a peer with address format: "192.168.1.1:17001" </summary>
    //public void ConnectToPeer(string fullAddress)
    //{
    //  ConnectToPeer(StringToEndPoint(fullAddress));
    //}


    ///// <summary> Returns true if socket is currently attempting a connection with supplied endpoint. </summary>
    //public bool ConnectingTo(IPEndPoint endpoint)
    //{
    //  return connectingEndpoints.Contains(endpoint);
    //}

    ///// <summary> Handles connecting status. Tracks attempted connections and (re)sends connection data. </summary>
    //private void Connect(IPEndPoint ep, NetStream approvalData)
    //{
    //  if (!connectingEndpoints.Contains(ep))
    //  {
    //    // We aren't currently trying to connect to this endpoint, add to lists:
    //    connectingEndpoints.Add(ep);
    //    connectingTimes.Add(NetTime.Milliseconds());
    //    connectingData.Add(approvalData);
    //    connectingRetriesRemaining.Add(4);
    //    NetLog.Info("Connecting to: " + ep);
    //  }
    //  else
    //  {
    //    // We are already trying to connect, update attempt data:
    //    int index = connectingEndpoints.IndexOf(ep);
    //    connectingRetriesRemaining[index]--;
    //    if (connectingRetriesRemaining[index] <= 0)
    //    {
    //      // Retried max amount of times, notify failure:
    //      RemoveFromConnecting(ep, false);
    //      return;
    //    }
    //    connectingTimes[index] = NetTime.Milliseconds();
    //    NetLog.Info("Retrying connection to: " + ep);
    //  }
    //  // Send the connection request data to the endpoint:
    //  SendStream(ep, approvalData);
    //}

    ///// <summary> Cleans up a connection attempt and returns true if it is a peer connection. </summary>
    //internal bool RemoveFromConnecting(IPEndPoint ep, bool successful)
    //{
    //  bool isPeer = false;
    //  if (!successful)
    //  {
    //    NetLog.Info("Failed to connect to: " + ep);
    //    Events.FailedToConnect(ep);
    //  }
    //  int index = connectingEndpoints.IndexOf(ep);
    //  connectingTimes.RemoveAt(index);
    //  connectingRetriesRemaining.RemoveAt(index);
    //  connectingEndpoints.Remove(ep);
    //  if (connectingData[index].Data[0] == (byte)ByteCmd.ConnectToPeer) isPeer = true;
    //  connectingData[index].Release();
    //  connectingData.RemoveAt(index);
    //  return isPeer;
    //}


    ///// <summary> Closes all connections. </summary>
    //public void DisconnectAll()
    //{
    //  for (int i = Connections.Count - 1; i >= 0; i--) Disconnect(Connections[i]);
    //}

    ///// <summary> Closes the supplied connection. </summary>
    //public void Disconnect(NetConnection connection)
    //{
    //  Connections.Remove(connection);
    //  endpointToConnection.Remove(connection.Endpoint);

    //  if (connection.IsPeer) Events.PeerDisconnected(connection);
    //  else if (connection.IsServer) Events.DisconnectedFromServer(connection);
    //  else Events.ClientDisconnected(connection);
    //}

    ///// <summary> Returns true if OS socket has data available for read. </summary>
    //private bool CanReceive()
    //{
    //  return socket.Poll(0, SelectMode.SelectRead);
    //}



    //private void Update()
    //{
    //  ReceiveAll();
    //}

    //private void LateUpdate()
    //{
    //  EndFrameTasks();
    //}

    ///// <summary> Receives data until CanReceive is no longer true (receive buffer empty). </summary>
    //private void ReceiveAll()
    //{
    //  if (socket == null) return;

    //  while (CanReceive())
    //  {
    //    var readStream = NetStream.Create();
    //    readStream.Socket = this;
    //    int received = 0;
    //    if (!TryReceive(readStream.Data, readStream.Data.Length, ref received, ref endPoint)) return;
    //    readStream.Length = received << 3;
    //    ProcessReceived(endPoint, received, readStream);
    //  }
    //}

    //internal void SendStream(NetConnection connection, NetStream stream)
    //{
    //  SendStream(connection.Endpoint, stream);
    //}

    //internal void SendStream(IPEndPoint endpoint, NetStream stream)
    //{
    //  TrySend(stream.Data, stream.Pos + 7 >> 3, endpoint);
    //}

    //private void ProcessReceived(IPEndPoint endpoint, int bytesReceived, NetStream readStream)
    //{
    //  if (EndpointConnected(endpoint)) endpointToConnection[endpoint].ReceiveStream(readStream);
    //  else ProcessUnconnected(endpoint, bytesReceived, readStream);
    //}

    //private void ProcessUnconnected(IPEndPoint endpoint, int bytesReceived, NetStream readStream)
    //{
    //  if (connectingEndpoints.Contains(endpoint)) ReceiveConnectionResponse(endpoint, bytesReceived, readStream);
    //  else if (bytesReceived > 0)
    //  {
    //    byte cmd = readStream.ReadByte();
    //    if (cmd == (byte)ByteCmd.Connect) ReceiveConnectionRequest(endPoint, readStream);
    //    else if (cmd == (byte)ByteCmd.ConnectToPeer) ReceivePeerConnectionRequest(endPoint, readStream);
    //  }
    //}

    //private void ReceiveConnectionResponse(IPEndPoint endpoint, int bytesReceived, NetStream readStream)
    //{
    //  bool isPeer = RemoveFromConnecting(endpoint, true);
    //  if (bytesReceived == 1 && readStream.Data[0] == (byte)ByteCmd.RefuseConnection)
    //  {
    //    NetLog.Info("Connection refused by: " + endpoint);
    //    return;
    //  }
    //  var connection = CreateConnection(endpoint, true, isPeer);
    //  if (bytesReceived > 1) connection.ReceiveStream(readStream);
    //}

    //private void ReceiveConnectionRequest(IPEndPoint endpoint, NetStream readStream)
    //{
    //  if (!AcceptConnections || Connections.Count >= MaxConnections || !Events.ClientApproval(endpoint, readStream))
    //  {
    //    NetLog.Info("Refused connection: " + endpoint);
    //    TrySend(new[] { (byte)ByteCmd.RefuseConnection }, 1, endpoint);
    //  }
    //  else CreateConnection(endpoint, false, false);
    //}

    //private void ReceivePeerConnectionRequest(IPEndPoint endpoint, NetStream readStream)
    //{
    //  if (!AcceptConnections || Connections.Count >= MaxConnections || !Events.PeerApproval(endpoint, readStream))
    //  {
    //    NetLog.Info("Refused peer connection: " + endpoint);
    //    TrySend(new[] { (byte)ByteCmd.RefuseConnection }, 1, endpoint);
    //  }
    //  else CreateConnection(endpoint, false, true);
    //}

    ///// <summary> Iterates through pending connections and retries any timeouts. </summary>
    //private void CheckForTimeouts()
    //{
    //  if (connectingEndpoints.Count == 0) return;
    //  for (int i = connectingEndpoints.Count - 1; i >= 0; i--)
    //  {
    //    if (NetTime.Milliseconds() - connectingTimes[i] > 2000) Connect(connectingEndpoints[i], connectingData[i]);
    //  }
    //}

    ///// <summary> Timeouts, disconnects, heartbeats, forced-acks, etc. need to be performed at end of frame. </summary>
    //private void EndFrameTasks()
    //{
    //  uint currentTime = NetTime.Milliseconds();
    //  for (int i = Connections.Count - 1; i >= 0; i--) Connections[i].EndOfFrame(currentTime);
    //  CheckForTimeouts();
    //}

    //internal bool EndpointConnected(IPEndPoint ep)
    //{
    //  return endpointToConnection.ContainsKey(ep);
    //}

    //internal NetConnection EndpointToConnection(IPEndPoint ep)
    //{
    //  return endpointToConnection[ep];
    //}

    ///// <summary> Adds a new NetConnection to the connection list. </summary>
    //internal NetConnection CreateConnection(IPEndPoint ep, bool isServer, bool isPeer)
    //{
    //  bool wasServer = false;
    //  // Connection cannot be both server and peer:
    //  if (isPeer)
    //  {
    //    isServer = false;
    //    wasServer = true;
    //  }

    //  var connection = new NetConnection(isServer, isPeer, this, ep);

    //  Connections.Add(connection);
    //  endpointToConnection.Add(ep, connection);
    //  if (isPeer) NetLog.Info("Peer connection created: " + ep);
    //  else if (isServer) NetLog.Info("Server connection created: " + ep);
    //  else NetLog.Info("Client connection created: " + ep);

    //  if (ProtocolAuthority && !isServer && !wasServer)
    //  {
    //    SendConnectionRequirements(connection);
    //    Rpc.SendLocalAssignments(connection);
    //  }
    //  else if (connection.IsPeer && Rpc.IdCount == RpcInfoCache.Count) Events.PeerConnected(connection);
    //  else if (isServer && Rpc.IdCount == RpcInfoCache.Count) Events.ConnectedToServer(connection);
    //  else if (!isServer && !isPeer) Events.ClientConnected(connection);

    //  return connection;
    //}

    ///// <summary> Sends a reliable RPC that does not target a specific view. </summary>
    //public void Send(string methodName, NetConnection target, params object[] parameters)
    //{
    //  if (!Rpc.HasId(methodName))
    //  {
    //    NetLog.Error("Remote RPC does not have an assigned ID: " + methodName);
    //    return;
    //  }
    //  var message = NetMessage.Create(Rpc.NameToId(methodName), 0, parameters, true);
    //  target.Send(message);
    //}

    ///// <summary> Sends a request to the target connection without an associated view. </summary>
    //public Request<T> SendRequest<T>(string methodName, NetConnection target, params object[] parameters)
    //{
    //  return Request.Send<T>(methodName, target, parameters);
    //}

    ///// <summary> Dispatches received commands and RPCs based on the messageID. </summary>
    //internal void ReceiveMessage(NetMessage message, NetConnection connection)
    //{
    //  if (message.MessageId > 1800) Command.Dispatch(message, connection);
    //  else if (message.ViewId == 0) DispatchRpc(message, connection);
    //  else Events.MessageReceived(message, connection);

    //  message.Release();
    //}

    ///// <summary> Sends connection configuration requirements command to a new client connection. </summary>
    //private void SendConnectionRequirements(NetConnection connection)
    //{
    //  Command.Send((int)Cmd.ConnectionRequirements, connection, Rpc.IdCount);
    //}

    ///// <summary> Handles connection configuration requirements sent by server upon connection. </summary>
    //private void ReceiveConnectionRequirements(NetMessage message, NetConnection connection)
    //{
    //  if (!connection.IsServer && !connection.IsPeer) return;
    //  Rpc.WaitingForRpcs += (int)message.Parameters[0];
    //}

    ///// <summary> Sends command to server to signal that connection requirements have been met. </summary>
    //internal void SendRequirementsMet(NetConnection connection)
    //{
    //  Command.Send((int)Cmd.RequirementsMet, connection);
    //  if (connection.IsPeer) Events.PeerConnected(connection);
    //  else Events.ConnectedToServer(connection);
    //}

    ///// <summary> Handles RequirementsMet command sent by client to signal the client is ready. </summary>
    //private void ReceiveRequirementsMet(NetMessage message, NetConnection connection)
    //{
    //  if (!connection.IsPeer && !connection.IsServer) Events.ClientConnected(connection);
    //  else if (connection.IsPeer) Events.PeerConnected(connection);
    //}
  }
}
