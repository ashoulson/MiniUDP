using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniUDP
{
  public class NetConfig
  {
    /// <summary>
    /// Rate at which to resend a "Connecting" message when attempting to
    /// establish a connection with a peer.
    /// </summary>
    public static double ConnectionRetryRate = 0.5;

    /// <summary>
    /// Timeout delay for connections with peers.
    /// </summary>
    public static double ConnectionTimeOut = 10.0;

    /// <summary>
    /// Timeout delay attempting to establish a connection with a peer.
    /// </summary>
    public static double ConnectionAttemptTimeOut = 10.0;

    /// <summary>
    /// Data buffer size used for packet I/O. 
    /// Don't change this without a good reason.
    /// </summary>
    internal const int DATA_BUFFER_SIZE = 2048;

    /// <summary>
    /// The maximum message size that a packet can contain, based on known
    /// MTUs for internet traffic. Don't change this without a good reason.
    /// </summary>
    internal const int MAX_MESSAGE_SIZE = 1400;

  }
}
