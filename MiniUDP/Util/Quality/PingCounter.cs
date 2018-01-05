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
  /// <summary>
  /// A sliding history window of outgoing ping information and support
  /// for cross-referencing incoming pongs against that history.
  /// </summary>
  internal class PingCounter
  {
    private readonly long[] pingTimes;
    private readonly byte[] pingSequences;
    private byte currentPingSeq;

    public PingCounter()
    {
      this.pingTimes = new long[NetQuality.PING_HISTORY];
      this.pingSequences = new byte[NetQuality.PING_HISTORY];
      for (int i = 0; i < this.pingTimes.Length; i++)
        this.pingTimes[i] = -1;
    }

    /// <summary>
    /// Creates a new outgoing ping. Stores both that ping's sequence
    /// and the time it was created.
    /// </summary>
    public byte CreatePing(long curTime)
    {
      this.currentPingSeq++;
      int index = this.currentPingSeq % NetQuality.PING_HISTORY;
      this.pingTimes[index] = curTime;
      this.pingSequences[index] = this.currentPingSeq;
      return this.currentPingSeq;
    }

    /// <summary>
    /// Returns the time the ping was created for the given pong.
    /// Checks to make sure the stored slot corresponds to the sequence.
    /// </summary>
    public long ConsumePong(byte pongSeq)
    {
      int index = pongSeq % NetQuality.PING_HISTORY;
      if (this.pingSequences[index] != pongSeq)
        return -1;

      long pingTime = this.pingTimes[index];
      this.pingTimes[index] = -1;
      return pingTime;
    }
  }
}