using System;
using SwiftlyS2.Shared.Natives;

namespace AFKManager.Models;

public sealed class AntiCampState
{
  public Vector LastPos;
  public QAngle LastAng;
  public DateTime CampStart;
  public DateTime LastWarn;
  public int Warnings;

  public AntiCampState(Vector pos, QAngle ang, DateTime now)
  {
    LastPos = pos;
    LastAng = ang;
    CampStart = now;
    LastWarn = DateTime.MinValue;
    Warnings = 0;
  }
}
