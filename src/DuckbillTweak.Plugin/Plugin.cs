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
    private readonly IChatGui chat;
    private readonly Configuration config;
    private readonly MerchantService merchants;
    private readonly LootService loot;
    private readonly List<string> registeredCommands = [];
    private NativeCompatibility compatibility = new(false, 0, "UI 초기화 중");
    private NativeSettingsAddon? settings;
    private NativeCurrencyAddon? currency;
    private NativeLootAddon? lootPopup;
    private NativeShopController? shop;
    private readonly List<NativeExchangeController> exchanges = [];
    private NativeLootInteraction? lootInteraction;
    private NativePvpBubbles? bubbles;
    private bool toolkitInitialized;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IDataManager data,
        IUnlockState unlocks, IGameInventory inventory, IClientState client, IPlayerState player,
        ICondition conditions, ITargetManager target, IGameGui gameGui, IFramework framework,
        IPluginLog log, IChatGui chat, IPartyList party, IAddonEventManager events, IAddonLifecycle addonLifecycle, IContextMenu contextMenu)
    {
        this.pluginInterface = pluginInterface; this.commands = commands; this.framework = framework;
        this.client = client; this.log = log; this.gui = gameGui; this.party = party; this.events = events; this.chat = chat;
        this.addonLifecycle = addonLifecycle;
        this.contextMenu = contextMenu;
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Version is < 1 or > 2) throw new InvalidOperationException("Unsupported configuration version.");
        config.Version = 2;
        config.ShopFilter ??= new(); config.ShopFilter.Categories ??= []; config.ShopFilter.CategoryHideOwned ??= [];
        config.ShopFilter.Search ??= ""; config.MerchantCategoryHideOwned ??= []; config.ExchangeCategoryHideOwned ??= [];
        if (!Enum.IsDefined(config.ShopFilter.Trade)) config.ShopFilter.Trade = Core.Shops.TradeFilter.All;
        merchants = new(data, unlocks, inventory, client, player, conditions, target, gameGui, framework, log, config);
        try { loot = new(chat, data, client, player, party, framework, config, log); }
        catch { merchants.Dispose(); throw; }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            compatibility = await Task.Run(NativeCompatibility.Inspect, cancellationToken);
            if (compatibility.Supported)
            {
                toolkitInitialized = true;
                await KamiToolKitLibrary.InitializeAsync(pluginInterface);
                await framework.Run(() =>
                {
                    merchants.NativeOwnershipEnabled = true;
                    settings = new NativeSettingsAddon(config, Save) { InternalName = "DuckbillTweakSettings", Title = "DuckbillTweak", Size = new Vector2(580, 302) };
                    currency = new NativeCurrencyAddon(merchants, config, Save) { InternalName = "DuckbillTweakCurrency", Title = "필요 재화", Size = new Vector2(610, 504) };
                    lootPopup = new NativeLootAddon(loot, config) { InternalName = "DuckbillTweakRolls", Title = "입찰 현황", Size = new Vector2(370, 362) };
                    shop = new(merchants, config, Save, currency, log);
                    exchanges.Add(new("InclusionShop", merchants, config, Save, currency, log));
                    exchanges.Add(new("ShopExchangeItem", merchants, config, Save, currency, log));
                    exchanges.Add(new("ShopExchangeCurrency", merchants, config, Save, currency, log));
                    lootInteraction = new(events, addonLifecycle, contextMenu, gui, party, config, lootPopup, merchants, log);
                    bubbles = new(framework, client, log, config, compatibility);
                });
            }
            else log.Warning("Native UI disabled: {Reason}", compatibility.Reason);
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
    private void OpenSettings()
    {
        if (!compatibility.Supported) { chat.PrintError(compatibility.Reason); return; }
        _ = framework.Run(() => settings?.Open());
    }
    private void OnCommand(string command, string arguments) => OpenSettings();

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        foreach (var command in registeredCommands) commands.RemoveHandler(command);
        await framework.Run(() =>
        {
            bubbles?.Dispose(); lootInteraction?.Dispose(); shop?.Dispose();
            foreach (var exchange in exchanges) exchange.Dispose();
            merchants.Dispose(); loot.Dispose();
        });
        if (settings is not null) await settings.DisposeAsync();
        if (currency is not null) await currency.DisposeAsync();
        if (lootPopup is not null) await lootPopup.DisposeAsync();
        if (toolkitInitialized) await KamiToolKitLibrary.DisposeAsync();
    }
}
