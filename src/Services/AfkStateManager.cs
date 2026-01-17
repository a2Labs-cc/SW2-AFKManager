using System;
using System.Collections.Generic;
using System.Linq;
using AFKManager.Helpers;
using AFKManager.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace AFKManager.Services;

public class AfkStateManager
{
  private readonly ISwiftlyCore _core;
  private readonly Dictionary<ulong, AfkState> _afkStates = new();

  public AfkStateManager(ISwiftlyCore core)
  {
    _core = core;
  }

  public void Clear()
  {
    _afkStates.Clear();
  }

  public void CheckAfk(Config config, DateTime now, bool skipChecks, HashSet<ulong> inBuyZone, Action<IPlayer, Config> onPunish)
  {
    if (skipChecks)
    {
      ResetStates(now);
      return;
    }

    var players = _core.PlayerManager.GetAllPlayers();
    if (players == null)
      return;

    HashSet<ulong> seen = new();

    foreach (var player in players)
    {
      if (!player.IsValid || player.IsFakeClient)
        continue;

      var controller = player.Controller;
      if (controller != null && controller.TeamNum == 1)
      {
        _afkStates.Remove(player.SteamID);
        continue;
      }

      seen.Add(player.SteamID);

      if (config.AfkSkipFlag != null && config.AfkSkipFlag.Count > 0)
      {
        if (_core.Permission.PlayerHasPermissions(player.SteamID, config.AfkSkipFlag))
        {
          _afkStates.Remove(player.SteamID);
          continue;
        }
      }

      var pawn = player.Pawn;
      if (pawn == null)
        continue;

      var pos = pawn.AbsOrigin;
      if (pos == null)
        continue;

      var ang = pawn.V_angle;

      if (!_afkStates.TryGetValue(player.SteamID, out var state))
      {
        _afkStates[player.SteamID] = new AfkState(pos.Value, ang, now);
        continue;
      }

      bool inactive = pos.Value.X == state.LastPos.X
                      && pos.Value.Y == state.LastPos.Y
                      && ang.X == state.LastAng.X
                      && ang.Y == state.LastAng.Y;

      if (!inactive)
      {
        state.LastPos = pos.Value;
        state.LastAng = ang;
        state.LastMoveActivity = now;
        state.LastLookActivity = now;
        state.LastActivity = (state.LastMoveActivity > state.LastLookActivity) ? state.LastMoveActivity : state.LastLookActivity;
        state.LastWarn = DateTime.MinValue;
        state.Warnings = 0;
        state.C4Transferred = false;
        PlayerHelper.ClearCenterHtml(_core, player, config);
        continue;
      }

      var warnInterval = TimeSpan.FromSeconds(Math.Max(1.0f, config.AfkWarnInterval));
      if (!PlayerHelper.ShouldWarn(state.LastWarn, state.LastActivity, warnInterval, now))
        continue;

      state.LastWarn = now;
      state.Warnings++;

      if (!state.C4Transferred && config.AfkTransferC4AfterWarnings > 0 && state.Warnings >= config.AfkTransferC4AfterWarnings)
      {
        TryTransferC4(player, config, inBuyZone);
      }

      EmitWarningSound(player, config);
      PlayerHelper.SendChatPrefixed(player, config, _core.Translation.GetPlayerLocalizer(player)["afk.warn", state.Warnings, config.AfkPunishAfterWarnings]);
      PlayerHelper.SendCenterHtml(_core, player, config, _core.Translation.GetPlayerLocalizer(player)["afk.warn.center", state.Warnings, config.AfkPunishAfterWarnings]);

      if (state.Warnings < config.AfkPunishAfterWarnings)
        continue;

      onPunish(player, config);
      _afkStates.Remove(player.SteamID);
    }

    var toRemove = _afkStates.Keys.Where(id => !seen.Contains(id)).ToList();
    foreach (var id in toRemove)
      _afkStates.Remove(id);
  }

  public bool HasAfkWarnings(ulong steamId)
  {
    return _afkStates.TryGetValue(steamId, out var state) && state.Warnings > 0;
  }

  private void ResetStates(DateTime now)
  {
    var players = _core.PlayerManager.GetAllPlayers();
    if (players == null)
      return;

    foreach (var player in players)
    {
      if (!player.IsValid || player.IsFakeClient)
        continue;

      var pawn = player.Pawn;
      var pos = pawn?.AbsOrigin;
      if (pawn == null || pos == null)
        continue;

      var ang = pawn.V_angle;
      if (_afkStates.TryGetValue(player.SteamID, out var state))
      {
        state.LastPos = pos.Value;
        state.LastAng = ang;
        state.LastMoveActivity = now;
        state.LastLookActivity = now;
        state.LastActivity = now;
        state.LastWarn = DateTime.MinValue;
        state.Warnings = 0;
        state.C4Transferred = false;
      }
      else
      {
        _afkStates[player.SteamID] = new AfkState(pos.Value, ang, now);
      }
    }
  }

  private void EmitWarningSound(IPlayer player, Config config)
  {
    var soundName = config.WarningSound;
    if (string.IsNullOrWhiteSpace(soundName))
      return;

    using var soundEvent = new SoundEvent() {
      Name = soundName,
      SourceEntityIndex = -1
    };

    soundEvent.Recipients.AddRecipient(player.PlayerID);
    soundEvent.Emit();
  }

  private void TryTransferC4(IPlayer afkPlayer, Config config, HashSet<ulong> inBuyZone)
  {
    try
    {
      var pawn = afkPlayer.Pawn;
      if (pawn == null)
        return;

      if (config.AfkTransferC4OnlyFromBuyZone && !inBuyZone.Contains(afkPlayer.SteamID))
        return;

      var weaponServices = pawn.WeaponServices;
      if (weaponServices == null)
        return;

    var teammates = _core.PlayerManager.GetTAlive()
        .Where(p => p.IsValid && p.SteamID != afkPlayer.SteamID)
        .ToList();

    if (teammates.Count == 0)
      return;

    var receiver = teammates[Random.Shared.Next(teammates.Count)];
    var receiverPawn = receiver.Pawn;
    if (receiverPawn?.ItemServices == null)
      return;

    var afkSteamId = afkPlayer.SteamID;
    var receiverSteamId = receiver.SteamID;
    _core.Scheduler.NextTick(() => {
      var currentPlayers = _core.PlayerManager.GetAllPlayers().ToList();
      var afk = currentPlayers.FirstOrDefault(p => p.IsValid && p.SteamID == afkSteamId);
      var recv = currentPlayers.FirstOrDefault(p => p.IsValid && p.SteamID == receiverSteamId);
      if (afk == null || recv == null)
        return;

      var afkPawn = afk.Pawn;
      if (afkPawn == null)
        return;

      if (config.AfkTransferC4OnlyFromBuyZone && !inBuyZone.Contains(afkSteamId))
        return;

      var ws = afkPawn.WeaponServices;
      if (ws == null)
        return;

      var currentC4 = FindWeaponByDesignerName(ws, "weapon_c4");
      if (currentC4 == null)
        return;

      var recvPawn = recv.Pawn;
      if (recvPawn?.ItemServices == null)
        return;

      ws.RemoveWeapon(currentC4);
      recvPawn.ItemServices.GiveItem("weapon_c4");

      if (_afkStates.TryGetValue(afkSteamId, out var state))
        state.C4Transferred = true;

      var afkName = PlayerHelper.GetPlayerName(afk);
      var recvName = PlayerHelper.GetPlayerName(recv);
      PlayerHelper.BroadcastChatLocalized(_core, config, "bomb.transfer.broadcast", recvName, afkName);

      PlayerHelper.SendChatPrefixed(recv, config, _core.Translation.GetPlayerLocalizer(recv)["bomb.transfer.receiver"]);
      PlayerHelper.SendChatPrefixed(afk, config, _core.Translation.GetPlayerLocalizer(afk)["bomb.transfer.afk"]);
    });
    }
    catch
    {
      // Silently catch C4 transfer errors
    }
  }

  private static CBasePlayerWeapon? FindWeaponByDesignerName(CPlayer_WeaponServices weaponServices, string designerName)
  {
    foreach (var handle in weaponServices.MyWeapons)
    {
      if (!handle.IsValid)
        continue;

      var weapon = handle.Value;
      if (weapon == null)
        continue;

      if (string.Equals(weapon.DesignerName, designerName, StringComparison.OrdinalIgnoreCase))
        return weapon;
    }

    return weaponServices.MyValidWeapons.FirstOrDefault(w => string.Equals(w.DesignerName, designerName, StringComparison.OrdinalIgnoreCase));
  }
}
