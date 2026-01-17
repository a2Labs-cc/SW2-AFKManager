using System;
using SwiftlyS2.Shared.Natives;

namespace AFKManager.Models;

public sealed class AfkState
{
  public Vector LastPos;
  public QAngle LastAng;
  public DateTime LastMoveActivity;
  public DateTime LastLookActivity;
  public DateTime LastActivity;
  public DateTime LastWarn;
  public int Warnings;
  public bool C4Transferred;

  public AfkState(Vector pos, QAngle ang, DateTime now)
  {
    LastPos = pos;
    LastAng = ang;
    LastMoveActivity = now;
    LastLookActivity = now;
    LastActivity = now;
    LastWarn = DateTime.MinValue;
    Warnings = 0;
    C4Transferred = false;
  }
}
