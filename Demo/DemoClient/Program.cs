/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2015-2018 - Alexander Shoulson - http://ashoulson.com
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

using System;
using System.Text;

using MiniUDP;
using DemoUtil;

class Program
{
  private static NetPeer peer;
  private static int payloadCount = 0;
  private static int notificationCount = 0;

  static void Main(string[] args)
  {
    NetConfig.LatencySimulation = true;
    Connector client = new Connector("Sample1.1", false);

    Clock fastClock = new Clock(0.02f);
    Clock slowClock = new Clock(1.0f);
    fastClock.OnFixedUpdate += SendPayload;
    slowClock.OnFixedUpdate += SendNotification;

    Program.peer = client.Connect("127.0.0.1:42324");

    while (true)
    {
      fastClock.Tick();
      slowClock.Tick();
      client.Update();

      if (Console.KeyAvailable)
      {
        ConsoleKeyInfo key = Console.ReadKey(true);
        switch (key.Key)
        {
          case ConsoleKey.F1:
            client.Stop();
            return;

          default:
            break;
        }
      }
    }
  }

  private static void SendNotification()
  {
    byte[] data = Encoding.UTF8.GetBytes("Notification " + notificationCount);
    Program.peer.QueueNotification(data, (ushort)data.Length);
    notificationCount++;
  }

  private static void SendPayload()
  {
    byte[] data = Encoding.UTF8.GetBytes("Payload " + payloadCount);
    Program.peer.SendPayload(data, (ushort)data.Length);
    payloadCount++;
  }
}
