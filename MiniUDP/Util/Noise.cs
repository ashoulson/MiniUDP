/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
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

#if DEBUG
using System;
using System.Linq;

namespace MiniUDP.Util
{
  internal class Noise
  {
    private const int HASH_MASK = 255;
    private int[] Hash = 
    {
      151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,
      140, 36,103, 30, 69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,
      247,120,234, 75,  0, 26,197, 62, 94,252,219,203,117, 35, 11, 32,
       57,177, 33, 88,237,149, 56, 87,174, 20,125,136,171,168, 68,175,
       74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
       60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54,
       65, 25, 63,161,  1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
      200,196,135,130,116,188,159, 86,164,100,109,198,173,186,  3, 64,
       52,217,226,250,124,123,  5,202, 38,147,118,126,255, 82, 85,212,
      207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
      119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,
      129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
      218,246, 97,228,251, 34,242,193,238,210,144, 12,191,179,162,241,
       81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
      184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
      222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180,
    };

    private static float Lerp(float a, float b, float t)
    {
      return ((1.0f - t) * a) + (t * b);
    }

    public Noise()
    {
      this.Shuffle();
    }

    public float GetValue(long milliseconds, float speed)
    {
      float scaled = ((int)milliseconds / 1000.0f) * speed;
      int floor = (int)Math.Floor(scaled);
      float t = scaled - floor;
      float smoothT = t * t * (3 - 2 * t);

      int min = floor & Noise.HASH_MASK;
      int max = (min + 1) & Noise.HASH_MASK;

      float smoothed = Noise.Lerp(this.Hash[min], this.Hash[max], smoothT);
      return (smoothed / Noise.HASH_MASK);
    }

    private void Shuffle()
    {
      Random random = new Random();
      this.Hash = this.Hash.OrderBy(x => random.Next()).ToArray();
    }
  }
}
#endif
