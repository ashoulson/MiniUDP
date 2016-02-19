using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MiniNet
{
  public static class NetDebug
  {
    [Conditional("DEBUG")]
    public static void LogError(object message)
    {
      System.Diagnostics.Debug.Print("ERROR: " + message.ToString());
    }

    [Conditional("DEBUG")]
    public static void LogWarning(object message)
    {
      System.Diagnostics.Debug.Print("WARNING: " + message.ToString());
    }

    [Conditional("DEBUG")]
    public static void Assert(bool condition)
    {
      System.Diagnostics.Debug.Assert(condition);
    }

    [Conditional("DEBUG")]
    public static void Assert(bool condition, string message)
    {
      System.Diagnostics.Debug.Assert(condition, message);
    }
  }
}
