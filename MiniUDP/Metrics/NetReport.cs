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
  public struct NetReport
  {
    public float CarrierCount { get { return this.carrierCount; } }
    public float PayloadCount { get { return this.payloadCount; } }
    public float OtherCount { get { return this.otherCount; } }
    public float CarrierTotal { get { return this.carrierTotal; } }
    public float PayloadTotal { get { return this.payloadTotal; } }
    public float OtherTotal { get { return this.otherTotal; } }

    public float AllCount
    {
      get
      {
        return
          this.carrierCount +
          this.payloadCount +
          this.otherCount;
      }
    }

    public float AllTotal
    {
      get
      {
        return
          this.carrierTotal +
          this.payloadTotal +
          this.otherTotal;
      }
    }

    private readonly float carrierCount;
    private readonly float payloadCount;
    private readonly float otherCount;
    private readonly float carrierTotal;
    private readonly float payloadTotal;
    private readonly float otherTotal;

    public static NetReport operator +(NetReport a, NetReport b)
    {
      return new NetReport(
        a.carrierCount + b.carrierCount,
        a.payloadCount + b.payloadCount,
        a.otherCount + b.otherCount,
        a.carrierTotal + b.carrierTotal,
        a.payloadTotal + b.payloadTotal,
        a.otherTotal + b.otherTotal);
    }

    public NetReport(
      float carrierCount,
      float payloadCount,
      float otherCount,
      float carrierTotal,
      float payloadTotal,
      float otherTotal)
    {
      this.carrierCount = carrierCount;
      this.payloadCount = payloadCount;
      this.otherCount = otherCount;
      this.carrierTotal = carrierTotal;
      this.payloadTotal = payloadTotal;
      this.otherTotal = otherTotal;
    }
  }
}
