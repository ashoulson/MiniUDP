using System;
using System.Collections.Generic;

using SampleCommon;

class Program
{
  static void Main(string[] args)
  {
    Connector server = new Connector("Sample1.1", true);

    server.Host(42324);

    while (true)
    {
      server.Update();

      if (Console.KeyAvailable)
      {
        ConsoleKeyInfo key = Console.ReadKey(true);
        switch (key.Key)
        {
          case ConsoleKey.F1:
            server.Stop();
            return;

          default:
            break;
        }
      }
    }
  }
}
