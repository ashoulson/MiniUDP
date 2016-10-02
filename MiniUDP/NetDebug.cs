/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
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
using System.Diagnostics;

namespace MiniUDP
{
  public interface INetDebugLogger
  {
    void LogMessage(object message);
    void LogWarning(object message);
    void LogError(object message);
  }

  internal class NetConsoleLogger : INetDebugLogger
  {
    public void LogError(object message)
    {
      NetConsoleLogger.Log("ERROR: " + message, ConsoleColor.Red);
    }

    public void LogWarning(object message)
    {
      NetConsoleLogger.Log("WARNING: " + message, ConsoleColor.Yellow);
    }

    public void LogMessage(object message)
    {
      NetConsoleLogger.Log("INFO: " + message, ConsoleColor.Gray);
    }

    private static void Log(object message, ConsoleColor color)
    {
      ConsoleColor current = Console.ForegroundColor;
      Console.ForegroundColor = color;
      Console.WriteLine(message);
      Console.ForegroundColor = current;
    }
  }

  public static class NetDebug
  {
    public static INetDebugLogger Logger = new NetConsoleLogger();

    [Conditional("DEBUG")]
    public static void LogMessage(object message)
    {
      if (NetDebug.Logger != null)
        lock (NetDebug.Logger)
          NetDebug.Logger.LogMessage(message);
    }

    [Conditional("DEBUG")]
    public static void LogWarning(object message)
    {
      if (NetDebug.Logger != null)
        lock (NetDebug.Logger)
          NetDebug.Logger.LogWarning(message);
    }

    [Conditional("DEBUG")]
    public static void LogError(object message)
    {
      if (NetDebug.Logger != null)
        lock (NetDebug.Logger)
          NetDebug.Logger.LogError(message);
    }

    [Conditional("DEBUG")]
    public static void Assert(bool condition)
    {
      if (condition == false)
        NetDebug.LogError("Assert Failed!");
    }

    [Conditional("DEBUG")]
    public static void Assert(bool condition, object message)
    {
      if (condition == false)
        NetDebug.LogError("Assert Failed: " + message);
    }
  }
}
