using System.IO;
using System.Windows;
using AmazonClientless.Enums;
using CommonPlugin;
using CommonPlugin.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using Playnite;

namespace AmazonClientless;

public partial class AmazonClientlessPluginSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool ImportInstalledGames { get; set; } = true;
    
    [ObservableProperty]
    public partial bool ConnectAccount { get; set; } = false;
    
    [ObservableProperty]
    public partial bool ImportUninstalledGames { get; set; } = false;
    
    [ObservableProperty]
    public partial string GamesInstallationPath { get; set; } = "";
    
    [ObservableProperty]
    public partial int MaxWorkers { get; set; } = 0;
    
    [ObservableProperty]
    public partial bool UnattendedInstall { get; set; } = false;
    
    [ObservableProperty]
    public partial ClearCacheTime AutoClearCache { get; set; } = ClearCacheTime.Never;
    
    [ObservableProperty]
    public partial UpdatePolicy GamesUpdatePolicy { get; set; } = UpdatePolicy.Month;
    
    [ObservableProperty]
    public partial long NextClearingTime { get; set; } = 0;
    
    [ObservableProperty]
    public partial long NextGamesUpdateTime { get; set; } = 0;
    
    [ObservableProperty]
    public partial bool AutoUpdateGames { get; set; } = false;
}

[INotifyPropertyChanged]
public partial class AmazonClientlessSettingsHandler(AmazonClientlessPlugin plugin) : PluginSettingsHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger();

    [ObservableProperty]
    public partial AmazonClientlessPluginSettings? Settings { get; set; } = null;

    public static AmazonClientlessPluginSettings LoadPluginSettings()
    {
        AmazonClientlessPluginSettings? settings = null;
        var settingsFile = Path.Combine(AmazonClientlessPlugin.PlayniteApi.UserDataDir, "settings.json");
        if (File.Exists(settingsFile))
        {
            var content = FileSystem.ReadFileAsStringSafe(settingsFile);
            if (!Serialization.TryFromJson(content, out settings))
            {
                Logger.Error("Failed to load plugin settings.");
            }
        }

        return settings ?? new AmazonClientlessPluginSettings();
    }
    
    public override FrameworkElement GetEditView(GetSettingsViewArgs args)
    {
        return new AmazonClientlessSettingsView { DataContext = this };
    }

    public override async Task BeginEditAsync(BeginEditArgs args)
    {
        Settings = plugin.Settings.GetClone();
    }

    public override async Task CancelEditAsync(CancelEditArgs args)
    {
        // This gets called when a user decides to close the view and cancel any unsaved changes.
    }

    public override async Task EndEditAsync(EndEditArgs args)
    {
        if (plugin.Settings!.AutoClearCache != Settings!.AutoClearCache)
        {
            if (Settings.AutoClearCache != ClearCacheTime.Never)
            {
                Settings.NextClearingTime = AmazonClientlessPlugin.GetNextClearingTime(Settings.AutoClearCache);
            }
            else
            {
                Settings.NextClearingTime = 0;
            }
        }

        if (plugin.Settings.GamesUpdatePolicy != Settings.GamesUpdatePolicy)
        {
            if (Settings.GamesUpdatePolicy != UpdatePolicy.Never)
            {
                Settings.NextGamesUpdateTime = AmazonClientlessPlugin.GetNextUpdateCheckTime(Settings.GamesUpdatePolicy);
            }
            else
            {
                Settings.NextGamesUpdateTime = 0;
            }
        }
        
        plugin.Settings = Settings;
        plugin.SavePluginSettings(Settings);
    }

    public override async Task<ICollection<string>> VerifySettingsAsync(VerifySettingsArgs args)
    {
        // This is executed when saving changes. You can do verification on current state
        // and if you detect some incorrect settings, you can report it here to the user.
        return [];
    }
}