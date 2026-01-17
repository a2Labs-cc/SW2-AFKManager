using System;
using System.Collections.Generic;
using System.Linq;
using AFKManager.Helpers;
using AFKManager.Models;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace AFKManager.Services;

public class AntiCampStateManager
{
  private readonly ISwiftlyCore _core;
  private readonly Dictionary<ulong, AntiCampState> _antiCampStates = new();

  public AntiCampStateManager(ISwiftlyCore core)
  {
    _core = core;
  }

  public void Clear()
  {
    _antiCampStates.Clear();
  }

  public void CheckAntiCamp(Config config, DateTime now, bool bombPlanted, Func<ulong, bool> hasAfkWarnings)
  {
    if (config.AntiCampPunishAfterWarnings <= 0)
    {
      _antiCampStates.Clear();
      return;
    }

    if (config.AntiCampSkipBombPlanted && bombPlanted)
    {
      _antiCampStates.Clear();
      return;
    }

    var warnInterval = TimeSpan.FromSeconds(Math.Max(1.0f, config.AntiCampWarnInterval));
    float radius = Math.Max(0.0f, config.AntiCampRadius);
    float radiusSq = radius * radius;
    HashSet<ulong> seen = new();

    foreach (var player in _core.PlayerManager.GetAllPlayers())
    {
      if (!player.IsValid || player.IsFakeClient)
        continue;

      var controller = player.Controller;
      if (controller == null)
      {
        _antiCampStates.Remove(player.SteamID);
        continue;
      }

      if (controller.TeamNum == 1 || controller.TeamNum == config.AntiCampSkipTeam)
      {
        _antiCampStates.Remove(player.SteamID);
        continue;
      }

      if (config.AntiCampSkipFlag != null && config.AntiCampSkipFlag.Count > 0)
      {
        if (_core.Permission.PlayerHasPermissions(player.SteamID, config.AntiCampSkipFlag))
        {
          _antiCampStates.Remove(player.SteamID);
          continue;
        }
      }

      if (hasAfkWarnings(player.SteamID))
      {
        _antiCampStates.Remove(player.SteamID);
        continue;
      }

      var pawn = player.Pawn;
      var pos = pawn?.AbsOrigin;
      if (pawn == null || pos == null)
      {
        _antiCampStates.Remove(player.SteamID);
        continue;
      }

      var angle = pawn.V_angle;

      seen.Add(player.SteamID);

      if (!_antiCampStates.TryGetValue(player.SteamID, out var state))
      {
        _antiCampStates[player.SteamID] = new AntiCampState(pos.Value, angle, now);
        continue;
      }

      float dx = pos.Value.X - state.LastPos.X;
      float dy = pos.Value.Y - state.LastPos.Y;
      float dz = pos.Value.Z - state.LastPos.Z;
      float distSq = dx * dx + dy * dy + dz * dz;

      if (distSq > radiusSq)
      {
        state.LastPos = pos.Value;
        state.LastAng = angle;
        state.CampStart = now;
        state.LastWarn = DateTime.MinValue;
        state.Warnings = 0;
        PlayerHelper.ClearCenterHtml(_core, player, config);
        continue;
      }

      state.LastAng = angle;

      if ((now - state.CampStart) < warnInterval)
        continue;

      if (!PlayerHelper.ShouldWarn(state.LastWarn, state.CampStart, warnInterval, now))
        continue;

      state.LastWarn = now;
      state.Warnings++;

      EmitWarningSound(player, config);
      PlayerHelper.SendChatPrefixed(player, config, _core.Translation.GetPlayerLocalizer(player)["anticamp.warn", state.Warnings, config.AntiCampPunishAfterWarnings]);
      PlayerHelper.SendCenterHtml(_core, player, config, _core.Translation.GetPlayerLocalizer(player)["anticamp.warn.center", state.Warnings, config.AntiCampPunishAfterWarnings]);

      if (state.Warnings < config.AntiCampPunishAfterWarnings)
        continue;
      ApplyPunishment(player, config);

      state.CampStart = now;
      state.LastWarn = DateTime.MinValue;
      state.Warnings = 0;
    }

    var toRemove = _antiCampStates.Keys.Where(id => !seen.Contains(id)).ToList();
    foreach (var id in toRemove)
      _antiCampStates.Remove(id);
  }

  private void ApplyPunishment(IPlayer player, Config config)
  {
    var punishment = (config.AntiCampPunishment ?? "slap").Trim().ToLowerInvariant();
    var steamId = player.SteamID;
    var slapDamage = config.AntiCampSlapDamage;
    
    PlayerHelper.ExecuteOnPlayer(_core, steamId, p => {
      if (punishment == "slay")
      {
        p.Pawn?.CommitSuicide(false, true);
      }
      else
      {
        SlapPlayer(p, slapDamage, config);
      }
    });
  }

  private void SlapPlayer(IPlayer player, int damage, Config config)
  {
    var pawn = player.Pawn;
    if (pawn == null)
      return;

    try
    {
      if (pawn.Health <= 0)
        return;

      if (damage > 0)
      {
        pawn.Health -= damage;

        if (pawn.Health <= 0)
        {
          pawn.CommitSuicide(true, true);
          PlayerHelper.SendChatPrefixed(player, config, _core.Translation.GetPlayerLocalizer(player)["anticamp.slay.self"]);
          return;
        }
      }

      var origin = pawn.AbsOrigin;
      var rotation = pawn.AbsRotation;
      
      if (origin == null || rotation == null)
        return;

      var currentVel = pawn.AbsVelocity;
      var random = new Random();
      
      var newVel = new SwiftlyS2.Shared.Natives.Vector(
        currentVel.X + (random.Next(180) + 50) * (random.Next(2) == 1 ? -1 : 1),
        currentVel.Y + (random.Next(180) + 50) * (random.Next(2) == 1 ? -1 : 1),
        currentVel.Z + random.Next(200) + 100
      );

      player.Teleport(origin.Value, rotation.Value, newVel);
      
      if (damage > 0)
        PlayerHelper.SendChatPrefixed(player, config, _core.Translation.GetPlayerLocalizer(player)["anticamp.slap.self_damage", damage]);
      else
        PlayerHelper.SendChatPrefixed(player, config, _core.Translation.GetPlayerLocalizer(player)["anticamp.slap.self"]);
    }
    catch (Exception ex)
    {
      _core.Logger.LogError($"[AFKManager] Error slapping player: {ex.Message}");
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
}
