using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Chat;
using DuckbillTweak.Plugin.Combat;
using DuckbillTweak.Plugin.Emotes;
using DuckbillTweak.Plugin.Loot;
using DuckbillTweak.Plugin.Native;
using DuckbillTweak.Plugin.Shops;
using KamiToolKit;

namespace DuckbillTweak.Plugin;

public sealed class Plugin : IAsyncDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPluginLog log;
    private readonly IGameGui gui;
    private readonly IPartyList party;
    private readonly IAddonEventManager events;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IContextMenu contextMenu;
    private readonly IChatGui chat;
#if DEBUG
    private readonly PositionalProbe probe;
    private readonly EmoteProbe emoteProbe;
    private readonly EmoteSyncExperiment emoteSync;
#endif
    private readonly IGameInteropProvider interop;
    private readonly IFlyTextGui flyText;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly ICondition conditions;
    private readonly ITargetManager targets;
    private readonly Configuration config;
    private readonly MerchantService merchants;
    private readonly LootService loot;
    private readonly List<string> registeredCommands = [];
    private readonly ISigScanner scanner;
    private NativeSettingsAddon? settings;
    private NativeChangelogAddon? changelog;
    private NativePositionalColorAddon? colors;
    private NativeColorPickerAddon? colorPicker;
    private NativeCurrencyAddon? currency;
    private NativeLootAddon? lootPopup;
    private NativeShopController? shop;
    private readonly List<NativeExchangeController> exchanges = [];
    private NativeLootInteraction? lootInteraction;
    private NativePvpBubbles? bubbles;
    private NativeBubbleOrder? bubbleOrder;
    private PositionalFlyText? positional;
    private EmoteFollow? emoteFollow;
    private bool toolkitInitialized;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IDataManager data,
        IUnlockState unlocks, IGameInventory inventory, IClientState client, IPlayerState player,
        ICondition conditions, ITargetManager target, IGameGui gameGui, IFramework framework,
        IPluginLog log, IChatGui chat, IPartyList party, IAddonEventManager events, IAddonLifecycle addonLifecycle, IContextMenu contextMenu,
        IGameInteropProvider interop, IFlyTextGui flyText, IObjectTable objects, ISigScanner scanner)
    {
        this.scanner = scanner;
        this.pluginInterface = pluginInterface; this.commands = commands; this.framework = framework;
        this.client = client; this.log = log; this.gui = gameGui; this.party = party; this.events = events;
        this.addonLifecycle = addonLifecycle;
        this.contextMenu = contextMenu;
        this.chat = chat; this.interop = interop; this.flyText = flyText; this.objects = objects; this.data = data; this.conditions = conditions; this.targets = target;
#if DEBUG
        probe = new(interop, flyText, objects, data, log, framework);
        emoteProbe = new(framework, objects, target, log);
        emoteSync = new(framework, objects, target, chat, log);
#endif
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Version is < 1 or > 3) throw new InvalidOperationException("Unsupported configuration version.");
        config.Version = 3;
        config.LastSeenChangelogVersion ??= "";
        if (MigratePositionalDisplay(config)) pluginInterface.SavePluginConfig(config);
        merchants = new(data, unlocks, inventory, client, player, conditions, target, gameGui, framework, log, config, pluginInterface);
        try { loot = new(chat, data, client, player, party, framework, config, log); }
        catch { merchants.Dispose(); throw; }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // No global DLL/game gate: Dalamud decides loadability, and each feature checks its own contract.
            // Executable-specific code (PvP patch, currency exchange fields) is resolved from KR signatures in the
            // running game; a failed contract disables only its own feature.
            var environment = await Task.Run(NativeEnvironment.Inspect, cancellationToken);
            log.Information("{Environment}", environment.Diagnostic);
            var code = await Task.Run(() => NativeRuntimeCode.Inspect(scanner), cancellationToken);
            log.Information("Native code contract: PvpBubbles={Pvp} ({PvpReason}); ShopExchangeCurrency={Exchange} ({ExchangeReason})",
                code.Pvp.Available, code.Pvp.Reason, code.Exchange.Available, code.Exchange.Reason);
            toolkitInitialized = true;
            await KamiToolKitLibrary.InitializeAsync(pluginInterface);
            await framework.Run(() =>
            {
                merchants.NativeOwnershipEnabled = true;
                loot.NativeObservationEnabled = true;
                changelog = new NativeChangelogAddon(config, Save) { InternalName = "DuckbillTweakChangelog", Title = "업데이트 내역", Size = new Vector2(650, 420) };
                colorPicker = new NativeColorPickerAddon { InternalName = "DuckbillTweakColorPicker", Title = "색 선택", Size = new Vector2(400, 425) };
                colors = new NativePositionalColorAddon(config, Save, colorPicker) { InternalName = "DuckbillTweakPositionalColors", Title = "측후·콤보 표시 세부 설정", Size = new Vector2(640, 390) };
                bubbles = new(framework, client, log, config, code.Pvp);
                positional = new(interop, framework, flyText, objects, client, data, log, config);
                settings = new NativeSettingsAddon(config, Save, () => changelog.Open(), () => colors.Open(),
                    bubbles.Available, code.Exchange.Available, positional.Available) { InternalName = "DuckbillTweakSettings", Title = "DuckbillTweak", Size = new Vector2(580, 510) };
                currency = new NativeCurrencyAddon(merchants) { InternalName = "DuckbillTweakCurrency", Title = "필요 재화", Size = new Vector2(760, 586) };
                lootPopup = new NativeLootAddon(loot, config) { InternalName = "DuckbillTweakRolls", Title = "입찰 현황", Size = new Vector2(420, 420) };
                shop = new(merchants, currency, log, addonLifecycle);
                exchanges.Add(new("InclusionShop", merchants, currency, log, addonLifecycle));
                exchanges.Add(new("ShopExchangeItem", merchants, currency, log, addonLifecycle));
                // The compact row map, page count, records and selected tab are addon fields read from KR instructions.
                if (code.Exchange.Value is { } layout) exchanges.Add(new("ShopExchangeCurrency", merchants, currency, log, addonLifecycle, layout));
                else log.Warning("ShopExchangeCurrency tools disabled: {Reason}", code.Exchange.Reason);
                lootInteraction = new(events, addonLifecycle, contextMenu, gui, config, lootPopup, log);
                bubbleOrder = new(framework, client, log);
                emoteFollow = new(contextMenu, framework, objects, client, targets, conditions, data, chat, log, config, Save);
            });
            foreach (var command in new[] { "/duckbilltweak", "/dbtw" })
            {
                if (!commands.AddHandler(command, new CommandInfo(OnCommand) { HelpMessage = "DuckbillTweak 기능 설정" }))
                    throw new InvalidOperationException($"Command already registered: {command}");
                registeredCommands.Add(command);
            }
            pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
            pluginInterface.UiBuilder.OpenMainUi += OpenSettings;
        }
        catch { await DisposeAsync(); throw; }
    }

    private void Save() => pluginInterface.SavePluginConfig(config);

    /// <summary>
    /// Moves positional/combo display values that still equal an earlier default or preset to the current ones.
    /// Values the user picked by hand are left alone. Returns true when anything changed.
    /// </summary>
    private static bool MigratePositionalDisplay(Configuration config)
    {
        var changed = false;
        if (NativePositionalColorAddon.PreviousDefaults.Contains((config.PositionalHitColor, config.PositionalMissColor)))
        {
            (config.PositionalHitColor, config.PositionalMissColor) = (NativePositionalColorAddon.DefaultHit, NativePositionalColorAddon.DefaultMiss);
            changed = true;
        }
        if (config.ComboFailColor == NativePositionalColorAddon.PreviousDefaultCombo)
        {
            config.ComboFailColor = NativePositionalColorAddon.DefaultCombo;
            changed = true;
        }
        var colors = (config.PositionalHitColor, config.PositionalMissColor, config.ComboFailColor);
        foreach (var (old, current) in NativePositionalColorAddon.PreviousPresets)
            if (colors == old)
            {
                (config.PositionalHitColor, config.PositionalMissColor, config.ComboFailColor) = current;
                changed = true;
                break;
            }
        foreach (var (old, current) in NativePositionalColorAddon.PreviousHitIcons)
            if (config.PositionalHitIcon == old) { config.PositionalHitIcon = current; changed = true; }
        foreach (var (old, current) in NativePositionalColorAddon.PreviousMissIcons)
            if (config.PositionalMissIcon == old) { config.PositionalMissIcon = current; changed = true; }
        return changed;
    }
    private void OpenSettings() => _ = framework.Run(() => settings?.Open());
    private void OnCommand(string command, string arguments)
    {
        var argument = arguments.Trim();
        if (argument.Equals("changelog", StringComparison.OrdinalIgnoreCase))
            _ = framework.Run(() => changelog?.Open());
#if DEBUG
        else if (argument.Equals("probe", StringComparison.OrdinalIgnoreCase))
            _ = framework.Run(() =>
            {
                // Research-only toggle (Debug builds only): logs own statuses, target names and positions to dalamud.log.
                var enabled = probe.Toggle();
                chat.Print(enabled ? "측후 조사 로그 켜짐: dalamud.log에 [PositionalProbe]로 기록합니다." : "측후 조사 로그 꺼짐.", "DuckbillTweak");
            });
        else if (argument.Equals("emoteprobe", StringComparison.OrdinalIgnoreCase))
            _ = framework.Run(() =>
            {
                // Research-only toggle (Debug builds only): logs own/target emote timelines to dalamud.log.
                var enabled = emoteProbe.Toggle();
                chat.Print(enabled ? "감정표현 조사 로그 켜짐: dalamud.log에 [EmoteProbe]로 기록합니다." : "감정표현 조사 로그 꺼짐.", "DuckbillTweak");
            });
        else if (argument.StartsWith("emotesync", StringComparison.OrdinalIgnoreCase))
        {
            // Research experiment (Debug builds only): "/dbtw emotesync [leadMs]".
            var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lead = parts.Length > 1 && int.TryParse(parts[1], out var value) ? value : 0;
            _ = framework.Run(() => emoteSync.Start(lead));
        }
#endif
        else OpenSettings();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        foreach (var command in registeredCommands) commands.RemoveHandler(command);
        await framework.Run(() =>
        {
#if DEBUG
            probe.Dispose(); emoteProbe.Dispose(); emoteSync.Dispose();
#endif
            positional?.Dispose(); emoteFollow?.Dispose(); bubbleOrder?.Dispose(); bubbles?.Dispose(); lootInteraction?.Dispose(); shop?.Dispose();
            foreach (var exchange in exchanges) exchange.Dispose();
            merchants.Dispose(); loot.Dispose();
        });
        if (settings is not null) await settings.DisposeAsync();
        if (changelog is not null) await changelog.DisposeAsync();
        if (colors is not null) await colors.DisposeAsync();
        if (colorPicker is not null) await colorPicker.DisposeAsync();
        if (currency is not null) await currency.DisposeAsync();
        if (lootPopup is not null) await lootPopup.DisposeAsync();
        if (toolkitInitialized) await KamiToolKitLibrary.DisposeAsync();
    }
}
