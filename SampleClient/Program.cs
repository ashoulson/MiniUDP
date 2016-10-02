using System;
using System.Text;
using System.Collections.Generic;

using MiniUDP;
using SampleCommon;

class Program
{
  private static NetPeer peer;
  private static int count = 0;

  static void Main(string[] args)
  {
    Connector client = new Connector("Sample1.0", false);

    Clock clock = new Clock(2.0f);
    clock.OnFixedUpdate += Clock_OnFixedUpdate;
    Program.peer = client.Connect("127.0.0.1:42324");

    while (true)
    {
      clock.Tick();
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

  private static void Clock_OnFixedUpdate()
  {
    byte[] data;
    data = Encoding.UTF8.GetBytes("Payload " + count);
    Program.peer.SendPayload(data, data.Length);
    data = Encoding.UTF8.GetBytes("Notification " + count);
    Program.peer.QueueNotification(data, data.Length);
    count++;
  }
}
