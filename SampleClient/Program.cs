using System;
using System.Collections.Generic;

using MiniUDP;

class Program
{
  static void Main(string[] args)
  {
    Client client = new Client("127.0.0.1:42324");
    //client.Start();

    while(true)
    {
      //client.Update();


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
}
