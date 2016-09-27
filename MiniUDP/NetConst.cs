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

namespace MiniUDP
{
  internal enum NetPacketType : byte
  {
    INVALID = 0,

    Protocol, // Protocol-level packet (connect, disconnect, etc.)
    Session,  // Session packet containing notifications and ping headers
    Payload,  // Raw data payload
  }

  internal enum NetProtocolType : byte
  {
    INVALID = 0,

    ConnectRequest,
    ConnectAccept,

    ConnectReject_BadID,  // This has a very, very low chance of happening
    ConnectReject_Full,   // Server is full
    ConnectReject_Closed, // We're not accepting connections at all
    ConnectReject_Custom, // Another reason, contained in the data

    Disconnect,
  }

  internal enum NetEventType : byte
  {
    INVALID = 0,

    StartConnect, // Main Thread -> Background Thread

    PeerConnecting,
    PeerTimedOut,
    PeerDisconnected,
    PeerSocketError,

    Connected,
    Rejected,
    Notification,
    Payload,
  }

  internal enum NetPeerStatus
  {
    Pending,
    Connected,
    Closed,
  }

  public class NetConst
  {
    #region Socket Config
    internal const int SOCKET_BUFFER_SIZE = 2048;
    internal const int SOCKET_TTL = 255;
    #endregion

    #region Packet Sizes
    public const int MAX_PACKET_SIZE = 1264;
    public const int MAX_PAYLOAD_DATA_SIZE = MAX_PACKET_SIZE - NetPayloadPacket.PAYLOAD_HEADER_SIZE;
    public const int MAX_PROTOCOL_DATA_SIZE = MAX_PACKET_SIZE - NetProtocolPacket.PROTOCOL_HEADER_SIZE;
    public const int MAX_SESSION_DATA_SIZE = MAX_PACKET_SIZE - NetSessionPacket.SESSION_HEADER_SIZE;
    public const int MAX_NOTIFICATION_DATA_SIZE = MAX_SESSION_DATA_SIZE - NetEvent.EVENT_HEADER_SIZE;
    #endregion

    #region Timing
    public const long CONNECTION_TIME_OUT = 15000;         // How long to wait before disconnecting a quiet peer
    public const long CONNECTION_ATTEMPT_TIME_OUT = 10000; // How long to try to connect for
    public const int TICK_RATE = 200;                      // How often we do tick for sending session packets
    public const int LONG_TICK_RATE = 600;                 // After this many ms, the next tick is a long tick
    public const int THREAD_SLEEP_TIME = 1;                // Thread sleep time during update
    #endregion

    #region Counts
    public const int MAX_PENDING_NOTIFICATIONS = 100;
    public const int MAX_PACKET_READS = 50;
    #endregion

    /// <summary>
    /// The delay (in ms) before we consider a connection to be spiking after
    /// receiving no traffic (and report 100% packet loss).
    /// </summary>
    internal const long SPIKE_TIME = 2000;

    /// <summary>
    /// Window size used when computing traffic statistic averages
    /// </summary>
    internal const int TRAFFIC_WINDOW_LENGTH = 20;

    /// <summary>
    /// Number of packets for which to keep a ping history. Should be roughly
    /// equal to your send rate times the spike seconds, with some tolerance.
    /// </summary>
    internal const int PING_HISTORY_LENGTH = 100;
  }
}
