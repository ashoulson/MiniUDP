using System;
using System.Collections.Generic;

using MiniUDP;

class Program
{
  enum FooEnum : byte
  {
    Invalid = 0x00,

    Connect = 0x10, // Fresh connection, requires acknowledgement
    Connected = 0x20, // Acknowledgement of receipt of a connection
    Disconnect = 0x30, // Disconnected message, may or may not arrive
    Message = 0x40, // General packet payload holding data
  }

  static void Main(string[] args)
  {
    Server server = new Server(44325);
    server.Start();

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
