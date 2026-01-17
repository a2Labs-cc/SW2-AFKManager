using System;
using System.Collections.Generic;
using System.Linq;
using AFKManager.Helpers;
using AFKManager.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace AFKManager.Services;

public class SpectatorStateManager
{
  private readonly ISwiftlyCore _core;
  private readonly Dictionary<ulong, SpecState> _specStates = new();
  private readonly HashSet<ulong> _movedToSpecByPlugin = new();

  public SpectatorStateManager(ISwiftlyCore core)
  {
    _core = core;
  }

  public void Clear()
  {
    _specStates.Clear();
    _movedToSpecByPlugin.Clear();
  }

  public void MarkMovedToSpec(ulong steamId)
  {
    _movedToSpecByPlugin.Add(steamId);
  }

  public void CheckSpectators(Config config, DateTime now)
  {
    var allPlayers = _core.PlayerManager.GetAllPlayers();
    if (allPlayers == null)
      return;

    int playersOnline = allPlayers.Count(p => p.IsValid);
    if (playersOnline < config.SpecKickMinPlayers)
    {
      _specStates.Clear();
      return;
    }

    var warnInterval = TimeSpan.FromSeconds(Math.Max(1.0f, config.SpecWarnInterval));
    HashSet<ulong> seenSpec = new();

    foreach (var player in _core.PlayerManager.GetAllPlayers())
    {
      if (!player.IsValid || player.IsFakeClient)
        continue;

      if (config.SpecSkipFlag != null && config.SpecSkipFlag.Count > 0)
      {
        if (_core.Permission.PlayerHasPermissions(player.SteamID, config.SpecSkipFlag))
        {
          _specStates.Remove(player.SteamID);
          continue;
        }
      }

      var controller = player.Controller;
      if (controller == null)
      {
        PlayerHelper.ClearCenterHtml(_core, player, config);
        _specStates.Remove(player.SteamID);
        _movedToSpecByPlugin.Remove(player.SteamID);
        continue;
      }

      if (controller.TeamNum != 1)
      {
        PlayerHelper.ClearCenterHtml(_core, player, config);
        _specStates.Remove(player.SteamID);
        _movedToSpecByPlugin.Remove(player.SteamID);
        continue;
      }

      if (config.SpecKickOnlyMovedByPlugin && !_movedToSpecByPlugin.Contains(player.SteamID))
      {
        PlayerHelper.ClearCenterHtml(_core, player, config);
        _specStates.Remove(player.SteamID);
        continue;
      }

      seenSpec.Add(player.SteamID);

      if (!_specStates.TryGetValue(player.SteamID, out var state))
      {
        _specStates[player.SteamID] = new SpecState(now);
        continue;
      }

      if (!PlayerHelper.ShouldWarn(state.LastWarn, state.Entered, warnInterval, now))
        continue;

      state.LastWarn = now;
      state.Warnings++;

      EmitWarningSound(player, config);
      PlayerHelper.SendChatPrefixed(player, config, _core.Translation.GetPlayerLocalizer(player)["spec.warn", state.Warnings, config.SpecKickAfterWarnings]);
      PlayerHelper.SendCenterHtml(_core, player, config, _core.Translation.GetPlayerLocalizer(player)["spec.warn.center", state.Warnings, config.SpecKickAfterWarnings]);

      if (state.Warnings < config.SpecKickAfterWarnings)
        continue;

      player.Kick(_core.Localizer["spec.kick.reason"], default(ENetworkDisconnectionReason));
      _specStates.Remove(player.SteamID);
      _movedToSpecByPlugin.Remove(player.SteamID);
    }

    var toRemove = _specStates.Keys.Where(id => !seenSpec.Contains(id)).ToList();
    foreach (var id in toRemove)
      _specStates.Remove(id);
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
