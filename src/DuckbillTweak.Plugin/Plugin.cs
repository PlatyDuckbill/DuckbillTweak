using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Chat;
using DuckbillTweak.Plugin.Loot;
using DuckbillTweak.Plugin.Shops;
using DuckbillTweak.Plugin.UI;

namespace DuckbillTweak.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly WindowSystem windows = new("DuckbillTweak");
    private readonly List<IDisposable> services = [];
    private readonly List<string> registeredCommands = [];
    private readonly Configuration config;
    private readonly SettingsWindow settings;
    private readonly MerchantWindow merchantWindow;
    private readonly LootWindow lootWindow;
    private readonly PvpBubbleService bubbles;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IDataManager data,
        IUnlockState unlocks, IGameInventory inventory, IClientState client, IPlayerState player,
        ICondition conditions, ITargetManager target, IGameGui gameGui, IFramework framework,
        IPluginLog log, IObjectTable objects, IChatGui chat, IPartyList party)
    {
        this.pluginInterface = pluginInterface; this.commands = commands;
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Version != 1) throw new InvalidOperationException("Unsupported DuckbillTweak configuration version.");
        config.ShopFilter ??= new();
        config.ShopFilter.Categories ??= [];
        config.ShopFilter.CategoryHideOwned ??= [];
        config.ShopFilter.Search ??= "";
        config.BubbleSeconds = float.IsFinite(config.BubbleSeconds) ? Math.Clamp(config.BubbleSeconds, 2, 15) : 6;
        config.BubbleWidth = float.IsFinite(config.BubbleWidth) ? Math.Clamp(config.BubbleWidth, 180, 500) : 300;
        try
        {
            var merchants = new MerchantService(data, unlocks, inventory, client, player, conditions, target,
                gameGui, framework, log, config);
            services.Add(merchants);
            var loot = new LootService(chat, data, client, player, party, framework, config);
            services.Add(loot);
            bubbles = new(chat, client, player, conditions, objects, gameGui, framework, config);
            services.Add(bubbles);
            merchantWindow = new(merchants, config, Save);
            lootWindow = new(loot, config);
            settings = new(config, Save, () => merchantWindow.IsOpen = true, () => lootWindow.IsOpen = true);
            merchants.OpenWindow = () => merchantWindow.IsOpen = true;
            loot.OpenWindow = () => lootWindow.IsOpen = true;
            windows.AddWindow(settings); windows.AddWindow(merchantWindow); windows.AddWindow(lootWindow);
            foreach (var command in new[] { "/duckbilltweak", "/dbtw" })
            {
                if (!commands.AddHandler(command, new CommandInfo(OnCommand)
                    { HelpMessage = "DuckbillTweak 설정. shop: 상인 보조, loot: 입찰 보조" }))
                    throw new InvalidOperationException($"Command already registered: {command}");
                registeredCommands.Add(command);
            }
            pluginInterface.UiBuilder.Draw += Draw;
            pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
            pluginInterface.UiBuilder.OpenMainUi += OpenSettings;
        }
        catch
        {
            foreach (var command in registeredCommands) commands.RemoveHandler(command);
            foreach (var service in services.AsEnumerable().Reverse()) service.Dispose();
            windows.RemoveAllWindows();
            throw;
        }
    }

    private void Save() => pluginInterface.SavePluginConfig(config);
    private void OpenSettings() => settings.IsOpen = true;
    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "shop": merchantWindow.IsOpen = true; break;
            case "loot": lootWindow.IsOpen = true; break;
            default: OpenSettings(); break;
        }
    }

    private void Draw()
    {
        windows.Draw();
        BubbleRenderer.Draw(bubbles, config);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        foreach (var command in registeredCommands) commands.RemoveHandler(command);
        foreach (var service in services.AsEnumerable().Reverse()) service.Dispose();
        windows.RemoveAllWindows();
    }
}
