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
    public const int MAX_PACKET_READS = 20;

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
    /// Data buffer size used for packet I/O. 
    /// Don't change this without a good reason.
    /// </summary>
    internal const int DATA_BUFFER_SIZE = 2048;

    /// <summary>
    /// The maximum message size that a packet can contain, based on known
    /// MTUs for internet traffic. Don't change this without a good reason.
    /// </summary>
    internal const int MAX_MESSAGE_SIZE = 1271;
  }
}
