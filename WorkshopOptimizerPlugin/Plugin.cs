using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using WorkshopOptimizerPlugin.Windows;
using Dalamud.Interface.ImGuiNotification;

namespace WorkshopOptimizerPlugin;

public sealed class Plugin : IDalamudPlugin
{
    public static string Name => "Workshop Optimizer Plugin";

    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("WorkshopOptimizerPlugin");

    public readonly Icons Icons;
    public static Notification NotifObject = new Notification();

    [PluginService]
    public static IChatGui ChatGui { get; private set; } = null!;

    [PluginService]
    public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    public static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    public static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService] 
    public static INotificationManager NotificationManager { get; private set; } = null!;

    private const string CommandName = "/wso";

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin() {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        Icons = new Icons(PluginInterface, TextureProvider);
        NotifObject.Title = "Workshop Optimizer";

        this.ConfigWindow = new ConfigWindow(this);
        this.MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Optimize the workshop"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainUI;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
    }

    public void ShowNotification(string message, NotificationType type = NotificationType.Info)
    {
        NotifObject.Content = message;
        NotifObject.Type = type;
        NotificationManager.AddNotification(NotifObject);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        Icons.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.IsOpen = true;
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void OpenMainUI()
    {
        MainWindow.IsOpen = true;
    }

    public void OpenConfigUI()
    {
        ConfigWindow.IsOpen = true;
    }
}
