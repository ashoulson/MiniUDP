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

using System.Collections.Generic;

namespace MiniUDP
{
  /// <summary>
  /// A sliding history window of bandwidth usage for either outgoing or
  /// incoming network traffic.
  /// </summary>
  public class NetBandwidth
  {
    private RingBuffer<NetReport> reports;
    private long updateTimeTrimmed;
    private List<NetReport> reusableList;

    private float carrierCount;
    private float payloadCount;
    private float otherCount;
    private float carrierTotal;
    private float payloadTotal;
    private float otherTotal;

    internal NetBandwidth(int historyLength, long startTime)
    {
      this.reports = new RingBuffer<NetReport>(historyLength);
      this.updateTimeTrimmed = startTime / 1000;
      this.reusableList = new List<NetReport>();
      this.ResetData();
    }

    public NetReport ComputeAverage()
    {
      return this.reports.ComputeAverage();
    }

    public NetReport ComputeTotal()
    {
      return this.reports.ComputeTotal();
    }

    public void Update(long currentTime)
    {
      long trimmedTime = currentTime / 1000;
      long distance = trimmedTime - this.updateTimeTrimmed;
      this.updateTimeTrimmed = trimmedTime;

      for (int i = 0; i < distance; i++)
      {
        this.reports.Push(
          new NetReport(
            this.carrierCount,
            this.payloadCount,
            this.otherCount,
            this.carrierTotal,
            this.payloadTotal,
            this.otherTotal));
        this.ResetData();
      }
    }

    public IEnumerable<NetReport> GetReports()
    {
      this.reusableList.Clear();
      this.reports.Reverse(this.reusableList);
      return this.reusableList;
    }

    internal void AddCarrier(int size)
    {
      this.carrierCount += 1;
      this.carrierTotal += size;
    }

    internal void AddPayload(int size)
    {
      this.payloadCount += 1;
      this.payloadTotal += size;
    }

    internal void AddOther(int size)
    {
      this.otherCount += 1;
      this.otherTotal += size;
    }

    private void ResetData()
    {
      this.carrierCount = 0.0f;
      this.payloadCount = 0.0f;
      this.otherCount = 0.0f;
      this.carrierTotal = 0.0f;
      this.payloadTotal = 0.0f;
      this.otherTotal = 0.0f;
    }
  }
}
