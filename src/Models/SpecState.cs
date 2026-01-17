using System;

namespace AFKManager.Models;

public sealed class SpecState
{
  public DateTime Entered;
  public DateTime LastWarn;
  public int Warnings;

  public SpecState(DateTime now)
  {
    Entered = now;
    LastWarn = DateTime.MinValue;
    Warnings = 0;
  }
}
