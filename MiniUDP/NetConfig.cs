using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniUDP
{
  public class NetConfig
  {
    /// <summary>
    /// Maximum packets we will read during a poll.
    /// </summary>
    public const int MAX_PACKET_READS = 500;

    /// <summary>
    /// Maximum packets we will read from a given peer.
    /// </summary>
    public const int MAX_PACKETS_PER_PEER = 3;

    /// <summary>
    /// Rate at which to resend a "Connecting" message when attempting to
    /// establish a connection with a peer.
    /// </summary>
    public const double CONNECTION_RETRY_RATE = 0.5;

    /// <summary>
    /// Timeout delay (in ms) for connections with peers.
    /// </summary>
    public const double CONNECTION_TIME_OUT = 10000;

    /// <summary>
    /// Timeout delay (in ms) attempting to establish a connection with a peer.
    /// </summary>
    public const long CONNECTION_ATTEMPT_TIME_OUT = 10000;

    /// <summary>
    /// The maximum message size that a packet can contain, based on known
    /// MTUs for internet traffic. Don't change this without a good reason.
    /// </summary>
    public const int MAX_PAYLOAD_SIZE = 1264;

    /// <summary>
    /// Data buffer size used for packet I/O. 
    /// Don't change this without a good reason.
    /// </summary>
    internal const int DATA_BUFFER_SIZE = 2048;

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
