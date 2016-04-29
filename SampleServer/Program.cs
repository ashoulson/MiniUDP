using System;
using System.Collections.Generic;

using MiniUDP;

class Program
{
  static void Main(string[] args)
  {

    NetWindow window = new NetWindow();

    //Console.WriteLine(window.SetBit(0));
    //Console.WriteLine(window.SetBit(7));
    //Console.WriteLine(window.SetBit(31));
    //Console.WriteLine(window.SetBit(127));

    //Console.WriteLine(window.SetBit(0));
    //Console.WriteLine(window.SetBit(7));
    //Console.WriteLine(window.SetBit(31));
    //Console.WriteLine(window.SetBit(127));

    //Console.WriteLine(window);

    //window.Shift(33);

    Console.WriteLine(window);

    Console.ReadLine();
    //Server server = new Server(44325);
    //server.Start();

    //while(true)
    //{
    //  server.Update();

    //  if (Console.KeyAvailable)
    //  {
    //    ConsoleKeyInfo key = Console.ReadKey(true);
    //    switch (key.Key)
    //    {
    //      case ConsoleKey.F1:
    //        server.Stop();
    //        return;

    //      default:
    //        break;
    //    } 
    //  }
    //}
  }
}
