using System.IO;
using System.Windows.Media;
using AmazonClientless.Enums;
using CommonPlugin;
using CommonPlugin.Enums;
using CommonPlugin.Resources;
using Linguini.Shared.Types.Bundle;
using Playnite;

namespace AmazonClientless;

public class AmazonClientlessPlugin : Plugin
{
    private static readonly ILogger Logger = LogManager.GetLogger();
    public const string Id = "hawkeye116477.AmazonClientless";
    public const string LibraryName = "Amazon Games";
    public const string ShortPluginName = "Amazon Clientless";
    public static IPlayniteApi PlayniteApi { get; private set; } = null!;

    public AmazonClientlessPluginSettings Settings { get; set; } = null!;
    public static AmazonClientlessPlugin Instance { get; private set; } = null!;
    public CommonHelpers CommonHelpers { get; set; } = null!;


    public AmazonClientlessPlugin()
    {
        LibrarySettings = new LibrarySupport
        {
            LibraryName = LibraryName,
            CanCloseOriginalClient = false,
            CanOpenOriginalClient = false,
            ProvidesStoreMetadata = true,
            CanImportPlaytime = false,
            CanImportPlaySessions = false,
            HasCustomGameImport = false,
        };
    }

    public override async Task InitializeAsync(InitializeArgs args)
    {
        PlayniteApi = args.Api;
        Instance = this;
        Settings = AmazonClientlessSettingsHandler.LoadPluginSettings();
        LoadLocalization();
        CommonHelpers = new CommonHelpers(PlayniteApi);
        CommonHelpers.LoadNeededResources();
    }

    private static void LoadLocalization()
    {
        var currentLanguage = PlayniteApi.Settings.Language;
        LocalizationManager.Instance.SetLanguage(currentLanguage);
        var commonFluentArgs = new Dictionary<string, IFluentType>
        {
            { "launcherName", (FluentString)ShortPluginName },
            { "pluginShortName", (FluentString)ShortPluginName },
            { "originalPluginShortName", (FluentString)"Amazon" },
            { "updatesSourceName", (FluentString)"Amazon" }
        };
        LocalizationManager.Instance.SetCommonArgs(commonFluentArgs);
    }

    public static AmazonClientlessPluginSettings GetSettings()
    {
        return Instance.Settings;
    }

    public override async ValueTask DisposeAsync()
    {
        // If you need to gracefully dispose of some resources on application shutdown, do it here.
    }

    public override async Task<CollectDiagnosticDataArgsAsyncResult?> CollectDiagnosticDataArgsAsync(CollectDiagnosticDataArgs args)
    {
        // Implement this method if you want to gather custom data when user generates diagnostics data for your plugin.
        // This can be run manually by user from addons view or on crash dialog that detected your plugin to be the source of the crash.
        // If the method is missing, Playnite collects extension log.
        return null;
    }


    public override ICollection<MenuItemDescriptor> GetGameMenuItemDescriptors(GetGameMenuItemDescriptorsArgs args)
    {
        return
        [
            new MenuItemDescriptor($"gameMenu.{Id}", ShortPluginName),
        ];
    }

    // This will get called every time a menu item is to be loaded in the UI.
    public override ICollection<MenuItemImpl>? GetGameMenuItems(GetGameMenuItemsArgs args)
    {
        var menuItems = new List<MenuItemImpl>();
        if (args.ItemId != $"gameMenu.{Id}")
        {
            return menuItems;
        }

        var pluginGames = args.Games.Where(i => i.LibraryId == Id).ToList();
        if (pluginGames.Count <= 0)
        {
            return menuItems;
        }

        var installedPluginGames =
            pluginGames.Where(i => i.InstallState == InstallState.Installed).ToList();
        if (pluginGames.Count >= 1)
        {
            if (installedPluginGames.Count >= 1)
            {
                AmazonClientlessGameMenuActions installedMenuActions = new(installedPluginGames);
                if (pluginGames.Count == 1)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.CommonMove),
                        async _ => { await installedMenuActions.OpenMoveGameWindow(); }
                      , icon: CommonIcons.MoveIcon)
                    );
                }
                else
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                        // TODO: Add uninstall action
                        async _ => {  },
                        icon: CommonIcons.UninstallIcon
                    ));
                }
                menuItems.Add(new MenuItemImpl(
                    LocalizationManager.Instance.GetString(LOC.CommonRepair),
                    _ =>
                    {
                        // TODO: Add repair action
                    }, icon: CommonIcons.RepairIcon
                ));
                menuItems.Add(new MenuItemImpl(
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCheckForUpdates),
                    async _ => { await installedMenuActions.OpenCheckForGamesUpdatesWindow(); },
                    icon: CommonIcons.UpdateIcon
                ));
            }
            else
            {
                var notInstalledPluginGames =
                    pluginGames.Where(i => i.InstallState == InstallState.Uninstalled).ToList();
                if (notInstalledPluginGames.Count > 1)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame),
                        _ =>
                        {
                            // TODO: Add installer
                        }, icon: CommonIcons.InstallIcon
                    ));
                }
            }
        }


        return menuItems;
    }

    // AppMenu works exactly the same as GameMenu related methods.
    // Use can use this to add new items to the main menu and tray menu.
    public override ICollection<MenuItemDescriptor>? GetAppMenuItemDescriptors(GetAppMenuItemDescriptorsArgs args)
    {
        return [];
    }

    public override ICollection<MenuItemImpl>? GetAppMenuItems(GetAppMenuItemsArgs args)
    {
        return null;
    }

    // Implement this if you want to provide custom view and functionality for game edit dialog.
    // If you want to allow users to change your game data that way.
    public override async Task<GameEditSessionHandler?> GetGameEditHandlerAsync(GetGameEditHandlerArgs args)
    {
        if (args.Games is [{ LibraryId: Id }])
        {
            return new AmazonClientlessGameEditSessionHandler(args.Games[0]);
        }

        return null;
    }

    // Implement this if you want to provide settings view functionality for your plugin that will be shown on addons views.
    public override async Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
    {
        return new AmazonClientlessSettingsHandler(this);
    }


    public override async Task<List<ImportableGame>> GetGamesAsync(LibraryGetGamesArgs args)
    {
        var launcher = new AmazonClientlessLauncher();
        var allGames = new List<ImportableGame>();
        var importableInstalledGames = launcher.ConvertInstalledToImportableGames();
        Exception? importError = null;

        if (Settings.ImportInstalledGames)
        {
            try
            {
                Logger.Debug($"Found {importableInstalledGames.Count} installed Amazon games.");
                allGames.AddRange([.. importableInstalledGames.Values]);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import installed Amazon games.");
                importError = e;
            }
        }

        if (Settings.ConnectAccount)
        {
            try
            {
                var libraryGames = await launcher.GetLibraryGames();
                Logger.Debug($"Found {libraryGames.Count} library Amazon games.");
                if (!Settings.ImportUninstalledGames)
                {
                    libraryGames = [.. libraryGames.Where(lg => importableInstalledGames.ContainsKey(lg.GameId))];
                }

                foreach (var game in libraryGames)
                {
                    if (importableInstalledGames.TryGetValue(game.GameId, out var installed))
                    {
                        installed.PlayTime = game.PlayTime;
                        installed.LastPlayedDate = game.LastPlayedDate;
                    }
                    else
                    {
                        allGames.Add(game);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import linked account GOG games details.");
                importError = e;
            }
        }

        if (importError is not null)
        {
            PlayniteApi.Notifications.Add(new NotificationMessage(
                "amazon_clientless_import_error",
                LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLibraryImportError,
                    new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LibraryName }) +
                Environment.NewLine + importError.Message,
                NotificationSeverity.Error,
                async () => await PlayniteApi.MainView.OpenPluginSettingsAsync(Id)));
        }
        else
        {
            PlayniteApi.Notifications.Remove("amazon_clientless_import_error");
        }

        return allGames;
    }


    // This will get called when game is being started. If your plugin knows how to start the game,
    // because you are a library plugin and you imported this game, or you are just providing alternative
    // ways of running games, this is where you do it.
    public override async Task<List<PlayController>> GetPlayActionsAsync(GetPlayActionsArgs args)
    {
        if (args.Game.LibraryId != Id)
        {
            return await base.GetPlayActionsAsync(args);
        }

        return [new AmazonClientlessPlayController(args.Game)];
    }

    public override async Task<List<InstallController>> GetInstallActionsAsync(GetInstallActionsArgs args)
    {
        // Implement this if you know how to install args.Game.
        return [];
    }

    public override async Task<List<UninstallController>> GetUninstallActionsAsync(GetUninstallActionsArgs args)
    {
        // Implement this if you know how to uninstall args.Game.
        return [];
    }

    // Implement this method if you are implementing metadata provider via MetadataSettings.
    public override async Task<MetadataProvider?> GetMetadataProviderAsync(GetMetadataProviderArgs args)
    {
        return new AmazonClientlessMetadataProvider();
    }

    public void SavePluginSettings(AmazonClientlessPluginSettings settings)
    {
        var settingsFile = Path.Combine(PlayniteApi.UserDataDir, "settings.json");
        FileSystem.WriteStringToFile(settingsFile, Serialization.ToJson(settings, true));
    }

    public static long GetNextClearingTime(ClearCacheTime frequency)
    {
        DateTimeOffset? clearingTime = null;
        DateTimeOffset now = DateTime.UtcNow;
        switch (frequency)
        {
            case ClearCacheTime.Day:
                clearingTime = now.AddDays(1);
                break;
            case ClearCacheTime.Week:
                clearingTime = now.AddDays(7);
                break;
            case ClearCacheTime.Month:
                clearingTime = now.AddMonths(1);
                break;
            case ClearCacheTime.ThreeMonths:
                clearingTime = now.AddMonths(3);
                break;
            case ClearCacheTime.SixMonths:
                clearingTime = now.AddMonths(6);
                break;
        }

        return clearingTime?.ToUnixTimeSeconds() ?? 0;
    }

    public static long GetNextUpdateCheckTime(UpdatePolicy frequency)
    {
        DateTimeOffset? updateTime = null;
        DateTimeOffset now = DateTime.UtcNow;
        switch (frequency)
        {
            case UpdatePolicy.PlayniteLaunch:
                updateTime = now;
                break;
            case UpdatePolicy.Day:
                updateTime = now.AddDays(1);
                break;
            case UpdatePolicy.Week:
                updateTime = now.AddDays(7);
                break;
            case UpdatePolicy.Month:
                updateTime = now.AddMonths(1);
                break;
            case UpdatePolicy.ThreeMonths:
                updateTime = now.AddMonths(3);
                break;
            case UpdatePolicy.SixMonths:
                updateTime = now.AddMonths(6);
                break;
        }

        return updateTime?.ToUnixTimeSeconds() ?? 0;
    }
}