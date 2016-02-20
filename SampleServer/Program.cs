using System;
using System.Collections.Generic;

using MiniUDP;

class Program
{
  static void Main(string[] args)
  {
    new Server(44325, 0.02).Run();
  }
}
