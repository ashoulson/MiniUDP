using System;
using System.Text;
using System.Collections.Generic;

using MiniUDP;
using SampleCommon;

class Program
{
  private static NetPeer peer;
  private static int payloadCount = 0;
  private static int notificationCount = 0;

  static void Main(string[] args)
  {
    Connector client = new Connector("Sample1.0", false);

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
    Program.peer.QueueNotification(data, data.Length);
    notificationCount++;
  }

  private static void SendPayload()
  {
    byte[] data = Encoding.UTF8.GetBytes("Payload " + payloadCount);
    Program.peer.SendPayload(data, data.Length);
    payloadCount++;
  }
}
