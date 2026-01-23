using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using AFKManager.Services;
using AFKManager.Helpers;

namespace AFKManager;

[PluginMetadata(Id = "AFKManager", Version = "1.0.1", Name = "AFKManager", Author = "aga", Description = "No description.")]
public partial class AFKManager : BasePlugin
{
  private static int _nextInstanceId;
  private static int _activeInstanceId;
  private readonly int _instanceId;
  private ServiceProvider? _serviceProvider;
  private IOptionsMonitor<Config>? _config;
  private const string ConfigSection = "AFKManager";
  private CancellationTokenSource? _afkTimer;
  private AfkStateManager? _afkStateManager;
  private AntiCampStateManager? _antiCampStateManager;
  private SpectatorStateManager? _spectatorStateManager;
  private bool _isWarmup;
  private bool _inFreezeTime;
  private bool _bombPlanted;
  private readonly System.Collections.Generic.HashSet<ulong> _inBuyZone = new();

  public AFKManager(ISwiftlyCore core) : base(core)
  {
    _instanceId = Interlocked.Increment(ref _nextInstanceId);
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void Load(bool hotReload)
  {
    if (Interlocked.CompareExchange(ref _activeInstanceId, _instanceId, 0) != 0)
      return;

    Core.Configuration
        .InitializeJsonWithModel<Config>("config.jsonc", ConfigSection)
        .Configure(builder => {
          builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
        });

    ServiceCollection services = new();
    services.AddSwiftly(Core)
            .AddOptionsWithValidateOnStart<Config>()
            .BindConfiguration(ConfigSection)
            .BindConfiguration("Main");

    _serviceProvider = services.BuildServiceProvider();
    _config = _serviceProvider.GetRequiredService<IOptionsMonitor<Config>>();

    _afkStateManager = new AfkStateManager(Core);
    _antiCampStateManager = new AntiCampStateManager(Core);
    _spectatorStateManager = new SpectatorStateManager(Core);

    _config.OnChange(OnConfigurationChanged);

    RegisterWarmupEvents();
    RegisterRoundEvents();
    RegisterBombEvents();
    RegisterBuyZoneEvents();
    StartAfkTimer();
  }

  public override void Unload()
  {
    StopAfkTimer();
    _afkStateManager = null;
    _antiCampStateManager = null;
    _spectatorStateManager = null;
    _serviceProvider?.Dispose();
    _serviceProvider = null;
    _config = null;

    if (_activeInstanceId == _instanceId)
      _activeInstanceId = 0;
  }

  private void StartAfkTimer()
  {
    StopAfkTimer();

    float period = _config?.CurrentValue.Timer ?? 5.0f;
    if (period <= 0)
      period = 5.0f;

    _afkTimer = Core.Scheduler.DelayAndRepeatBySeconds(period, period, CheckAfk);
    Core.Scheduler.StopOnMapChange(_afkTimer);
  }

  private void EnsureAfkTimerRunning()
  {
    if (_config == null)
      return;

    if (_afkTimer == null || _afkTimer.IsCancellationRequested)
      StartAfkTimer();
  }

  private void StopAfkTimer()
  {
    _afkTimer?.Cancel();
    _afkTimer?.Dispose();
    _afkTimer = null;
    _afkStateManager?.Clear();
    _antiCampStateManager?.Clear();
    _spectatorStateManager?.Clear();
  }

  private void OnConfigurationChanged(Config newConfig)
  {
    if (Math.Abs(newConfig.Timer - (_config?.CurrentValue.Timer ?? 0)) > 0.01f)
      StartAfkTimer();
  }

  private void CheckAfk()
  {
    if (_config == null || _afkStateManager == null || _antiCampStateManager == null || _spectatorStateManager == null)
      return;

    var cfg = _config.CurrentValue;
    var now = DateTime.UtcNow;

    bool skipAfkChecks = _inFreezeTime || (cfg.SkipWarmup && _isWarmup);

    _afkStateManager.CheckAfk(cfg, now, skipAfkChecks, _inBuyZone, ApplyAfkPunishment);

    if (!skipAfkChecks)
      _antiCampStateManager.CheckAntiCamp(cfg, now, _bombPlanted, _afkStateManager.HasAfkWarnings);

    _spectatorStateManager.CheckSpectators(cfg, now);
  }

  private void ApplyAfkPunishment(IPlayer player, Config cfg)
  {
    var action = (cfg.AfkPunishment ?? "spectator").Trim().ToLowerInvariant();
    var steamId = player.SteamID;

    PlayerHelper.ExecuteOnPlayer(Core, steamId, p => {
      var playerName = PlayerHelper.GetPlayerName(p);
      if (action == "kick")
      {
        PlayerHelper.BroadcastChatLocalized(Core, cfg, "afk.punish.kick.broadcast", playerName);
        p.Kick(Core.Localizer["afk.punish.kick.reason"], default(ENetworkDisconnectionReason));
        return;
      }

      if (action == "kill")
      {
        PlayerHelper.BroadcastChatLocalized(Core, cfg, "afk.punish.kill.broadcast", playerName);
        p.Pawn?.CommitSuicide(false, true);
        return;
      }

      PlayerHelper.BroadcastChatLocalized(Core, cfg, "afk.punish.spec.broadcast", playerName);
      p.ChangeTeam(Team.Spectator);
      _spectatorStateManager?.MarkMovedToSpec(steamId);
    });
  }

  private void RegisterWarmupEvents()
  {
    _isWarmup = false;
    Core.GameEvent.HookPre<EventRoundAnnounceWarmup>((@event) => {
      _isWarmup = true;
      return HookResult.Continue;
    });
  }

  private void RegisterRoundEvents()
  {
    _inFreezeTime = false;
    Core.GameEvent.HookPre<EventRoundStart>((@event) => {
      EnsureAfkTimerRunning();
      _inFreezeTime = true;
      _isWarmup = false;
      return HookResult.Continue;
    });

    Core.GameEvent.HookPre<EventRoundFreezeEnd>((@event) => {
      _inFreezeTime = false;
      return HookResult.Continue;
    });
  }

  private void RegisterBombEvents()
  {
    _bombPlanted = false;
    Core.GameEvent.HookPre<EventBombPlanted>((@event) => {
      _bombPlanted = true;
      return HookResult.Continue;
    });

    Core.GameEvent.HookPre<EventBombDefused>((@event) => {
      _bombPlanted = false;
      return HookResult.Continue;
    });

    Core.GameEvent.HookPre<EventBombExploded>((@event) => {
      _bombPlanted = false;
      return HookResult.Continue;
    });
  }

  private void RegisterBuyZoneEvents()
  {
    _inBuyZone.Clear();
    Core.GameEvent.HookPre<EventEnterBuyzone>((@event) => {
      var player = @event.UserIdPlayer;
      if (player != null && player.IsValid)
        _inBuyZone.Add(player.SteamID);
      return HookResult.Continue;
    });

    Core.GameEvent.HookPre<EventExitBuyzone>((@event) => {
      var player = @event.UserIdPlayer;
      if (player != null)
        _inBuyZone.Remove(player.SteamID);
      return HookResult.Continue;
    });
  }
}