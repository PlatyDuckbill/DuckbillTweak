using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Chat;
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
    private readonly Configuration config;
    private readonly MerchantService merchants;
    private readonly LootService loot;
    private readonly List<string> registeredCommands = [];
    private NativeExecutable executable = new(false, 0, "not inspected");
    private NativeSettingsAddon? settings;
    private NativeChangelogAddon? changelog;
    private NativeCurrencyAddon? currency;
    private NativeLootAddon? lootPopup;
    private NativeShopController? shop;
    private readonly List<NativeExchangeController> exchanges = [];
    private NativeLootInteraction? lootInteraction;
    private NativePvpBubbles? bubbles;
    private NativeBubbleOrder? bubbleOrder;
    private bool toolkitInitialized;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IDataManager data,
        IUnlockState unlocks, IGameInventory inventory, IClientState client, IPlayerState player,
        ICondition conditions, ITargetManager target, IGameGui gameGui, IFramework framework,
        IPluginLog log, IChatGui chat, IPartyList party, IAddonEventManager events, IAddonLifecycle addonLifecycle, IContextMenu contextMenu)
    {
        this.pluginInterface = pluginInterface; this.commands = commands; this.framework = framework;
        this.client = client; this.log = log; this.gui = gameGui; this.party = party; this.events = events;
        this.addonLifecycle = addonLifecycle;
        this.contextMenu = contextMenu;
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Version is < 1 or > 3) throw new InvalidOperationException("Unsupported configuration version.");
        config.Version = 3;
        config.LastSeenChangelogVersion ??= "";
        merchants = new(data, unlocks, inventory, client, player, conditions, target, gameGui, framework, log, config, pluginInterface);
        try { loot = new(chat, data, client, player, party, framework, config, log); }
        catch { merchants.Dispose(); throw; }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // No global DLL/game gate: Dalamud decides loadability, and each feature checks its own contract.
            // Only executable-bound code (PvP patch, currency exchange row map) requires the exact KR executable.
            var environment = await Task.Run(NativeEnvironment.Inspect, cancellationToken);
            log.Information("{Environment}", environment.Diagnostic);
            executable = await Task.Run(NativeExecutable.Inspect, cancellationToken);
            log.Information("Native executable contract: Matches={Matches}; {Reason}; Features=PvpBubbles,ShopExchangeCurrency",
                executable.Matches, executable.Reason);
            toolkitInitialized = true;
            await KamiToolKitLibrary.InitializeAsync(pluginInterface);
            await framework.Run(() =>
            {
                merchants.NativeOwnershipEnabled = true;
                loot.NativeObservationEnabled = true;
                changelog = new NativeChangelogAddon(config, Save) { InternalName = "DuckbillTweakChangelog", Title = "업데이트 내역", Size = new Vector2(650, 420) };
                settings = new NativeSettingsAddon(config, Save, () => changelog.Open(), executable.Matches) { InternalName = "DuckbillTweakSettings", Title = "DuckbillTweak", Size = new Vector2(580, 348) };
                currency = new NativeCurrencyAddon(merchants) { InternalName = "DuckbillTweakCurrency", Title = "필요 재화", Size = new Vector2(760, 586) };
                lootPopup = new NativeLootAddon(loot, config) { InternalName = "DuckbillTweakRolls", Title = "입찰 현황", Size = new Vector2(420, 420) };
                shop = new(merchants, currency, log, addonLifecycle);
                exchanges.Add(new("InclusionShop", merchants, currency, log, addonLifecycle));
                exchanges.Add(new("ShopExchangeItem", merchants, currency, log, addonLifecycle));
                // The compact row map and selected-tab byte are raw offsets inspected on the exact KR executable.
                if (executable.Matches) exchanges.Add(new("ShopExchangeCurrency", merchants, currency, log, addonLifecycle));
                else log.Warning("ShopExchangeCurrency tools disabled: {Reason}", executable.Reason);
                lootInteraction = new(events, addonLifecycle, contextMenu, gui, config, lootPopup, log);
                bubbles = new(framework, client, log, config, executable);
                bubbleOrder = new(framework, client, log);
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
    private void OpenSettings() => _ = framework.Run(() => settings?.Open());
    private void OnCommand(string command, string arguments)
    {
        if (arguments.Trim().Equals("changelog", StringComparison.OrdinalIgnoreCase))
            _ = framework.Run(() => changelog?.Open());
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
            bubbleOrder?.Dispose(); bubbles?.Dispose(); lootInteraction?.Dispose(); shop?.Dispose();
            foreach (var exchange in exchanges) exchange.Dispose();
            merchants.Dispose(); loot.Dispose();
        });
        if (settings is not null) await settings.DisposeAsync();
        if (changelog is not null) await changelog.DisposeAsync();
        if (currency is not null) await currency.DisposeAsync();
        if (lootPopup is not null) await lootPopup.DisposeAsync();
        if (toolkitInitialized) await KamiToolKitLibrary.DisposeAsync();
    }
}
