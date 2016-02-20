using System;
using System.Collections.Generic;

using MiniUDP;

class Program
{
  static void Main(string[] args)
  {
    new Client("127.0.0.1:44325", 0.02).Run();
  }
}
