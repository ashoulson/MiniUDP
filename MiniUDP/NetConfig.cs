using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniUDP
{
  internal class NetConfig
  {
    internal const int DATA_BUFFER_SIZE = 2048;
    internal const int MAX_MESSAGE_SIZE = 1400;
    internal const double CONNECTION_RETRY_RATE = 0.5;
    internal const double CONNECTION_TIME_OUT = 10.0;
  }
}
