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
  public enum NetRejectReason : byte
  {
    INVALID = 0,

    BadVersion,
    Closed,
    Full,
    Disconnected,
  }

  public enum NetKickReason : byte
  {
    INVALID = 0,

    User,
    Error,
    Timeout,
    Shutdown,
  }

  internal enum NetPacketType : byte
  {
    INVALID = 0,

    Connect,
    Disconnect,
    ConnectAccept,
    ConnectReject,
    Carrier,
    Payload,
  }

  internal enum NetEventType : byte
  {
    INVALID = 0,

    Notification,
    Payload,

    PeerConnected,
    PeerClosedError,
    PeerClosedTimeout,
    PeerClosedShutdown,
    PeerClosedKicked,

    ConnectTimedOut,
    ConnectAccepted,
    ConnectRejected,
  }

  public class NetConfig
  {
    #region Configurable
    public static int ShortTickRate = 250;
    public static int LongTickRate = 1000;
    public static int SleepTime = 1;
    #endregion

    #region Socket Config
    internal const int SOCKET_BUFFER_SIZE = 2048;
    #endregion

    #region Packet
    public const int MAX_DATA_SIZE = 1200;
    public const int MAX_NOTIFICATION_PACK = MAX_DATA_SIZE + NetEvent.HEADER_SIZE;
    public const int MAX_VERSION_BYTES = (1 << (8 * sizeof(byte))) - 1;
    public const int MAX_TOKEN_BYTES = (1 << (8 * sizeof(byte))) - 1;
    #endregion

    #region Timing
    public const long CONNECTION_TIME_OUT = 15000; // How long to wait before disconnecting a quiet peer
    #endregion

    #region Counts
    public const int MAX_PENDING_NOTIFICATIONS = 100;
    public const int MAX_PACKET_READS = 50;
    #endregion

    #region Misc
    internal const byte DONT_NOTIFY_PEER = 0;
    internal const byte DEFAULT_USER_REASON = 255;
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
