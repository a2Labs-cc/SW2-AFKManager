using System;
using System.Linq;
using AFKManager;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace AFKManager.Helpers;

public static class PlayerHelper
{
  public static void ExecuteOnPlayer(ISwiftlyCore core, ulong steamId, Action<IPlayer> action)
  {
    core.Scheduler.NextTick(() => {
      var player = core.PlayerManager.GetAllPlayers()
          .FirstOrDefault(x => x.IsValid && x.SteamID == steamId);
      if (player != null)
        action(player);
    });
  }

  public static void SendCenterHtml(ISwiftlyCore core, IPlayer player, Config config, string html, int? durationMs = null)
  {
    if (!config.CenterHtmlAlerts)
      return;

    if (!player.IsValid)
      return;

    int duration = durationMs ?? 1500;
    if (duration <= 0)
      duration = 1500;

    core.Scheduler.NextTick(() => {
      if (!player.IsValid)
        return;
      player.SendCenterHTML(html, duration);
    });
  }

  public static void ClearCenterHtml(ISwiftlyCore core, IPlayer player, Config config)
  {
    if (!config.CenterHtmlAlerts)
      return;

    if (!player.IsValid)
      return;

    core.Scheduler.NextTick(() => {
      if (!player.IsValid)
        return;
      player.SendCenterHTML(string.Empty, 1);
    });
  }

  public static string GetPlayerName(IPlayer player)
  {
    var name = player.Controller?.PlayerName;
    return string.IsNullOrWhiteSpace(name) ? player.SteamID.ToString() : name;
  }

  public static void BroadcastChat(ISwiftlyCore core, string message)
  {
    core.Scheduler.NextTick(() => {
      foreach (var player in core.PlayerManager.GetAllPlayers())
      {
        if (!player.IsValid)
          continue;
        player.SendChat(message);
      }
    });
  }

  public static string ApplyPrefix(Config config, string message)
  {
    var prefix = config.ChatPrefix ?? string.Empty;
    var prefixColor = config.ChatPrefixColor ?? string.Empty;
    if (string.IsNullOrWhiteSpace(prefix))
      return message;

    if (!string.IsNullOrWhiteSpace(prefixColor))
      return $"[{prefixColor.Trim().ToLowerInvariant()}]{prefix} [default]{message}";

    return $"{prefix} {message}";
  }

  public static void SendChatPrefixed(IPlayer player, Config config, string message)
  {
    player.SendChat(ApplyPrefix(config, message));
  }

  public static void BroadcastChatPrefixed(ISwiftlyCore core, Config config, string message)
  {
    BroadcastChat(core, ApplyPrefix(config, message));
  }

  public static void BroadcastChatLocalized(ISwiftlyCore core, Config config, string key, params object[] args)
  {
    var message = core.Localizer[key, args];
    BroadcastChatPrefixed(core, config, message);
  }

  public static bool ShouldWarn(DateTime lastWarn, DateTime lastActivity, TimeSpan interval, DateTime now)
  {
    if (lastWarn == DateTime.MinValue)
      return (now - lastActivity) >= interval;
    return (now - lastWarn) >= interval;
  }
}
