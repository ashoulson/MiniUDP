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
  public struct NetVersion
  {
    internal readonly byte major;
    internal readonly byte minor;
    internal readonly ushort revision;

    public NetVersion(byte major, byte minor, ushort revision)
    {
      this.major = major;
      this.minor = minor;
      this.revision = revision;
    }

    internal bool Equals(NetVersion other)
    {
      return
        (this.major == other.major) &&
        (this.minor == other.minor) &&
        (this.revision == other.revision);
    }
  }

  internal enum NetPacketType : byte
  {
    INVALID = 0,

    // Protocol-Level Messages
    ConnectRequest,
    ConnectAccept,
    ConnectReject,
    Disconnect,
    Ping,
    Pong,

    // User Data Carriers
    Payload,
    Notification,
  }

  internal enum NetRejectReason : byte
  {
    INVALID = 0,

    BadVersion,
    Closed,
    Full,
  }

  internal enum NetDisconnectReason : byte
  {
    INVALID = 0,

    Timeout,
    Shutdown,
    Error,
    User,
  }

  internal enum NetEventType : byte
  {
    INVALID = 0,

    PeerConnected,
    PeerTimedOut,
    PeerDisconnected,
    PeerSocketError,

    Connected,
    Rejected,
    Notification,
    Payload,
  }

  public class NetConst
  {
    #region Socket Config
    internal const int SOCKET_BUFFER_SIZE = 2048;
    internal const int SOCKET_TTL = 255;
    #endregion

    #region Packet
    public const int MAX_DATA_SIZE = 1200;
    public const int MAX_NOTIFICATION_PACK = MAX_DATA_SIZE + NetEvent.HEADER_SIZE;
    public const byte MIN_DISCONNECT_REASON = 100;
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
