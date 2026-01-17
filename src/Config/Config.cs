
using System.Collections.Generic;

namespace AFKManager;

public class Config
{
  public string ChatPrefix { get; set; } = "AFKManager |";
  public string ChatPrefixColor { get; set; } = "Red";
  public bool CenterHtmlAlerts { get; set; } = false;
  public int AfkPunishAfterWarnings { get; set; } = 3;
  public string AfkPunishment { get; set; } = "spectator";
  public float AfkWarnInterval { get; set; } = 5.0f;
  public int AfkTransferC4AfterWarnings { get; set; } = 1;
  public bool AfkTransferC4OnlyFromBuyZone { get; set; } = true;
  public float SpecWarnInterval { get; set; } = 20.0f;
  public int SpecKickAfterWarnings { get; set; } = 5;
  public int SpecKickMinPlayers { get; set; } = 5;
  public bool SpecKickOnlyMovedByPlugin { get; set; } = false;
  public List<string> SpecSkipFlag { get; set; } = new() { "admin.root", "admin.ban" };
  public List<string> AfkSkipFlag { get; set; } = new() { "admin.root", "admin.ban" };
  public List<string> AntiCampSkipFlag { get; set; } = new() { "admin.root", "admin.ban" };
  public string WarningSound { get; set; } = "UIPanorama.ui_custom_lobby_dialog_slide";
  public bool SkipWarmup { get; set; } = true;
  public float AntiCampRadius { get; set; } = 130.0f;
  public string AntiCampPunishment { get; set; } = "slap";
  public int AntiCampSlapDamage { get; set; } = 0;
  public float AntiCampWarnInterval { get; set; } = 5.0f;
  public int AntiCampPunishAfterWarnings { get; set; } = 3;
  public bool AntiCampSkipBombPlanted { get; set; } = true;
  public int AntiCampSkipTeam { get; set; } = 3;
  public float Timer { get; set; } = 5.0f;
}
