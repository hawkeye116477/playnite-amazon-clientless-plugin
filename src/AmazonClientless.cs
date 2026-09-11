using System.IO;
using System.Windows;
using AmazonClientless.Enums;
using AmazonClientless.Models;
using CommonPlugin;
using CommonPlugin.Enums;
using CommonPlugin.Resources;
using Linguini.Shared.Types.Bundle;
using Playnite;
using PlayniteMod;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;

namespace AmazonClientless;

public class AmazonClientlessPlugin : Plugin
{
    private static readonly ILogger Logger = LogManager.GetLogger<AmazonClientlessPlugin>();
    public const string Id = "hawkeye116477.AmazonClientless";
    public const string LibraryName = "Amazon Games Clientless";
    public const string ShortPluginName = "Amazon Games Clientless";
    public static IPlayniteApi PlayniteApi { get; private set; } = null!;
    public AmazonClientlessPluginSettings Settings { get; set; } = null!;
    public static AmazonClientlessPlugin Instance { get; private set; } = null!;
    public CommonHelpers CommonHelpers { get; set; } = null!;
    public IUnifiedDownloadManagerApi UnifiedDownloadManagerApi { get; set; } = null!;
    public AmazonClientlessDownloadLogic UnifiedDownloadLogic { get; set; } = null!;
    public DownloadManagerData PluginDownloadData { get; set; } = null!;
    public Dictionary<string, InstalledGamesWrapper.Installed> InstalledAppList { get; set; } = [];
    public bool InstalledAppListModified { get; set; } = false;

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
        UnifiedDownloadLogic = new AmazonClientlessDownloadLogic();
        PluginDownloadData = AmazonClientlessDownloadLogic.LoadSavedDownloadData();
        InstalledAppList = AmazonClientlessGames.GetPluginInstalledAppList();
    }

    private static void LoadLocalization()
    {
        var currentLanguage = PlayniteApi.Settings.Language;
        LocalizationManager.Instance.SetLanguage(currentLanguage);
        var commonFluentArgs = new Dictionary<string, IFluentType>
        {
            { "launcherName", (FluentString)ShortPluginName },
            { "pluginShortName", (FluentString)ShortPluginName },
            { "originalPluginShortName", (FluentString)"Amazon Games" },
            { "updatesSourceName", (FluentString)"Amazon Games" }
        };
        LocalizationManager.Instance.SetCommonArgs(commonFluentArgs);
    }

    public static AmazonClientlessPluginSettings GetSettings()
    {
        return Instance.Settings;
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

    public override ICollection<MenuItemImpl> GetGameMenuItems(GetGameMenuItemsArgs args)
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
                if (pluginGames.Count == 1)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.CommonMove),
                        async _ => { await AmazonClientlessGameMenuActions.OpenMoveGameWindow(installedPluginGames[0]); }
                      , icon: CommonIcons.MoveIcon)
                    );
                }
                else
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                        async _ => { await AmazonClientlessUninstallController.LaunchUninstaller(installedPluginGames); },
                        icon: CommonIcons.UninstallIcon
                    ));
                }

                menuItems.Add(new MenuItemImpl(
                    LocalizationManager.Instance.GetString(LOC.CommonRepair),
                    _ => { AmazonClientlessGameMenuActions.OpenRepairWindow(installedPluginGames); },
                    icon: CommonIcons.RepairIcon
                ));
                if (pluginGames.Count == 1)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCheckForUpdates),
                        async _ => { await AmazonClientlessGameMenuActions.OpenCheckForGamesUpdatesWindow(installedPluginGames[0]); },
                        icon: CommonIcons.UpdateIcon
                    ));
                }
            }
            else
            {
                var notInstalledPluginGames =
                    pluginGames.Where(i => i.InstallState == InstallState.Uninstalled).ToList();
                if (notInstalledPluginGames.Count > 1)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame),
                        _ => { AmazonClientlessGameMenuActions.OpenInstallerWindow(notInstalledPluginGames); },
                        icon: CommonIcons.InstallIcon
                    ));
                }
            }
        }


        return menuItems;
    }

    public override ICollection<MenuItemDescriptor>? GetAppMenuItemDescriptors(GetAppMenuItemDescriptorsArgs args)
    {
        return
        [
            new MenuItemDescriptor($"appMenu.{Id}.Items", ShortPluginName),
        ];
    }

    public override ICollection<MenuItemImpl> GetAppMenuItems(GetAppMenuItemsArgs args)
    {
        var menuItems = new List<MenuItemImpl>();
        var childMenuItems = new List<MenuItemImpl>();
        if (args.ItemId == $"appMenu.{Id}.Items")
        {
            childMenuItems.Add(new MenuItemImpl(LocalizationManager.Instance.GetString(LOC.CommonCheckForGamesUpdatesButton),
                async _ =>
                {
                    var gamesUpdates = new Dictionary<string, UpdateInfo>();
                    var legendaryUpdateController = new AmazonClientlessUpdateController();
                    var updateCheckProgressOptions =
                        new GlobalProgressOptions(
                                LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates),
                                false)
                            { IsIndeterminate = true };
                    await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions,
                        async _ => { gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates(); }
                    );

                    var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                    {
                        ShowMaximizeButton = false
                    });
                    window.DataContext = gamesUpdates;
                    window.Title =
                        $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                    window.Content = new AmazonClientlessUpdaterView();
                    window.Owner = PlayniteApi.GetLastActiveWindow();
                    window.SizeToContent = SizeToContent.WidthAndHeight;
                    window.MinWidth = 600;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.ShowDialog();
                },
                icon: CommonIcons.UpdateIcon
            ));
            childMenuItems.Add(new MenuItemImpl(LocalizationManager.Instance.GetString(LOC.CommonFinishInstallation),
                async _ =>
                {
                    var installedAppList = AmazonClientlessGames.GetAllInstalledGames();
                    var gamesToCompleteInstall = installedAppList
                                                .Where(g => !AmazonClientlessGameSettingsViewModel.LoadGameSettings(g.Key).IsFullyInstalled)
                                                .ToList();
                    if (gamesToCompleteInstall.Count != 0)
                    {
                        var installProgressOptions =
                            new GlobalProgressOptions(
                                    LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation), false)
                                { IsIndeterminate = false };

                        await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(installProgressOptions, async Task (progress) =>
                            {
                                progress.SetProgressMaxValue(gamesToCompleteInstall.Count);
                                var current = 0;
                                foreach (var game in gamesToCompleteInstall)
                                {
                                    progress.SetText(
                                        $"{LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation)} ({game.Value.Name})");
                                    await AmazonClientlessGames.CompleteGameInstallation(game.Key, game.Value.Path);
                                    current++;
                                    progress.SetCurrentProgressValue(current);
                                }
                            }
                        );
                    }
                    else
                    {
                        await PlayniteApi.Dialogs.ShowMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.CommonNoFinishNeeded));
                    }
                }, icon: CommonIcons.FinishInstallationIcon));
            menuItems.Add(new MenuItemImpl(ShortPluginName, childMenuItems));
        }

        return menuItems;
    }

    public override async Task<GameEditSessionHandler?> GetGameEditHandlerAsync(GetGameEditHandlerArgs args)
    {
        if (args.Games is [{ LibraryId: Id }])
        {
            return new AmazonClientlessGameEditSessionHandler(args.Games[0]);
        }

        return null;
    }

    public override async Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
    {
        return new AmazonClientlessSettingsHandler(this);
    }

    public override async Task<List<ImportableGame>> GetGamesAsync(LibraryGetGamesArgs args)
    {
        var allGames = new List<ImportableGame>();
        var importableInstalledGames = AmazonClientlessGames.ConvertInstalledToImportableGames();
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
                var libraryGames = await AmazonClientlessGames.GetLibraryGames();
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
                        allGames.AddMissing(game);
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
        if (args.Game.LibraryId != Id)
        {
            return await base.GetInstallActionsAsync(args);
        }

        return [new AmazonClientlessInstallController(args.Game)];
    }

    public override async Task<List<UninstallController>> GetUninstallActionsAsync(GetUninstallActionsArgs args)
    {
        if (args.Game.LibraryId != Id)
        {
            return await base.GetUninstallActionsAsync(args);
        }

        return [new AmazonClientlessUninstallController(args.Game)];
    }

    public override async Task<MetadataProvider?> GetMetadataProviderAsync(GetMetadataProviderArgs args)
    {
        return new AmazonClientlessMetadataProvider();
    }

    public override async Task PostInitializationAsync(PostInitializationArgs args)
    {
        var result = await PlayniteApi.CallPluginAsync(new PluginCallRequestAsyncArgs(
            UnifiedDownloadManagerSharedProperties.Id,
            UnifiedDownloadManagerSharedProperties.GetApi));
        if (result is { Success: true, Value: IUnifiedDownloadManagerApi udmApi })
        {
            UnifiedDownloadManagerApi = udmApi;
        }
    }

    public override async Task<object?> OnPluginCallRequestAsync(PluginCallRequestAsyncArgs args)
    {
        return args.CallId == UnifiedDownloadManagerSharedProperties.GetDownloadLogic ? UnifiedDownloadLogic : null;
    }

    public void SavePluginSettings(AmazonClientlessPluginSettings settings)
    {
        var settingsFile = Path.Combine(PlayniteApi.UserDataDir, "settings.json");
        FileSystem.WriteStringToFile(settingsFile, Serialization.ToJson(settings, true));
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

    public override async Task OnApplicationStartupAsync(OnApplicationStartupArgs args)
    {
        var globalSettings = GetSettings();
        if (globalSettings.GamesUpdatePolicy != UpdatePolicy.Never)
        {
            var nextGamesUpdateTime = globalSettings.NextGamesUpdateTime;
            var udmInstalled = PlayniteApi.Addons.Plugins.Any(plugin =>
                plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
            if (nextGamesUpdateTime != 0 && udmInstalled)
            {
                DateTimeOffset now = DateTime.UtcNow;
                if (now.ToUnixTimeSeconds() >= nextGamesUpdateTime)
                {
                    globalSettings.NextGamesUpdateTime =
                        GetNextUpdateCheckTime(globalSettings.GamesUpdatePolicy);
                    SavePluginSettings(globalSettings);
                    var pluginUpdateController = new AmazonClientlessUpdateController();
                    var gamesUpdates = await pluginUpdateController.CheckAllGamesUpdates(true);
                    if (gamesUpdates.Count > 0)
                    {
                        var successUpdates = gamesUpdates.Where(i => i.Value.Status == UpdateStatus.Available)
                                                         .ToDictionary(i => i.Key, i => i.Value);
                        if (successUpdates.Count > 0)
                        {
                            if (globalSettings.AutoUpdateGames)
                            {
                                await pluginUpdateController.UpdateGame(successUpdates, "", true);
                            }
                            else
                            {
                                var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                                {
                                    ShowMaximizeButton = false
                                });
                                window.DataContext = successUpdates;
                                window.Title =
                                    $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                                window.Content = new AmazonClientlessUpdateController();
                                window.Owner = PlayniteApi.GetLastActiveWindow();
                                window.SizeToContent = SizeToContent.WidthAndHeight;
                                window.MinWidth = 600;
                                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                window.ShowDialog();
                            }
                        }
                        else if (gamesUpdates.Any(i => i.Value.Status == UpdateStatus.Error))
                        {
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                "LegendaryGamesUpdateCheckFail",
                                $"{LibraryName} {Environment.NewLine}{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage)}",
                                NotificationSeverity.Error));
                            Logger.Error("Failed to check for games updates");
                        }
                    }
                }
            }
        }
    }

    public override async Task OnApplicationShutdownAsync(OnApplicationShutdownArgs args)
    {
        var settings = GetSettings();
        if (settings.AutoClearCache != ClearCacheTime.Never)
        {
            var nextClearingTime = settings.NextClearingTime;
            if (nextClearingTime != 0)
            {
                DateTimeOffset now = DateTime.UtcNow;
                if (now.ToUnixTimeSeconds() >= nextClearingTime)
                {
                    AmazonClientlessCache.ClearCache();
                    settings.NextClearingTime = AmazonClientlessCache.GetNextClearingTime(settings.AutoClearCache);
                    SavePluginSettings(settings);
                }
            }
            else
            {
                settings.NextClearingTime = AmazonClientlessCache.GetNextClearingTime(settings.AutoClearCache);
                SavePluginSettings(settings);
            }
        }

        AmazonClientlessDownloadLogic.SaveDownloadData();
        if (InstalledAppListModified)
        {
            var commonHelpers = Instance.CommonHelpers;
            commonHelpers.SaveJsonSettingsToFile(InstalledAppList, "", "installed", true);
        }
    }
}