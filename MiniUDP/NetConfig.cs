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
  public class NetConfig
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
    public const int MAX_NOTIFICATION_DATA_SIZE = MAX_SESSION_DATA_SIZE - NetNotification.NOTIFICATION_HEADER_SIZE;
    #endregion

    #region Counts
    public const int MAX_PENDING_NOTIFICATIONS = 100;
    #endregion

    /// <summary>
    /// Maximum packets we will read during a poll.
    /// </summary>
    public const int MAX_PACKET_READS = 500;

    /// <summary>
    /// Maximum packets we will read from a given peer.
    /// </summary>
    public const int MAX_PACKETS_PER_PEER = 20;

    /// <summary>
    /// Rate at which to resend a "Connecting" message when attempting to
    /// establish a connection with a peer.
    /// </summary>
    public const double CONNECTION_RETRY_RATE = 0.5;

    /// <summary>
    /// Timeout delay (in ms) for connections with peers.
    /// </summary>
    public const long CONNECTION_TIME_OUT = 15000;

    /// <summary>
    /// Timeout delay (in ms) attempting to establish a connection with a peer.
    /// </summary>
    public const long CONNECTION_ATTEMPT_TIME_OUT = 10000;

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
