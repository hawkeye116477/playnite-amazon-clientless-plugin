using System.IO;
using System.Windows;
using AmazonClientless.Models;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using Playnite;

namespace AmazonClientless;

public class AmazonClientlessInstallController(Game game) : InstallController("amazon_clientless_install",
    "Install using Amazon Clientless plugin", game.LibraryGameId!)
{
    public override async Task InstallAsync(InstallActionArgs args)
    {
        var installProperties = new DownloadProperties { DownloadAction = DownloadAction.Install };
        var installData = new List<DownloadManagerData.Download>
        {
            new() { GameId = game.LibraryGameId!, Name = game.Name, DownloadProperties = installProperties }
        };

        LaunchInstaller(installData);
        await GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
    }

    public static void LaunchInstaller(List<DownloadManagerData.Download> installData)
    {
        var playniteApi = AmazonClientlessPlugin.PlayniteApi;
        var window = playniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.DataContext = installData;
        window.Content = new AmazonClientlessGameInstallerView();
        window.Owner = playniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var title = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame);
        if (installData[0].DownloadProperties.DownloadAction == DownloadAction.Repair)
        {
            title = LocalizationManager.Instance.GetString(LOC.CommonRepair);
        }

        if (installData.Count == 1)
        {
            title = installData[0].Name;
        }

        window.Title = title;
        window.ShowDialog();
    }
}

public class AmazonClientlessUninstallController(Game game) : UninstallController("amazon_clientless_uninstall",
    "Uninstall using Amazon Clientless plugin", game.LibraryGameId!)
{
    private static readonly ILogger Logger = LogManager.GetLogger<AmazonClientlessUninstallController>();

    public override async Task UninstallAsync(UninstallActionArgs args)
    {
        var games = new List<Game>
        {
            game
        };
        await LaunchUninstaller(games);
        await GameUninstallationCancelledAsync(new GameUninstallCancelledArgs());
    }

    public static async Task LaunchUninstaller(List<Game> games)
    {
        var gamesCombined = string.Join(", ", games.Select(item => item.Name));
        var responses = new List<MessageBoxResponse>
        {
            new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteYesLabel)),
            new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteNoLabel))
        };

        var removeGameLaunchSettingsCheckbox =
            new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonRemoveGameLaunchSettings), false);

        var playniteApi = AmazonClientlessPlugin.PlayniteApi;
        
        var result = await playniteApi.Dialogs.ShowMessageAsync(
            LocalizationManager.Instance.GetString(LOC.CommonUninstallGameConfirm,
                new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)gamesCombined }),
            LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
            MessageBoxSeverity.Question, responses, [removeGameLaunchSettingsCheckbox]);
        if (result == responses[0])
        {
            var notUninstalledGames = new List<Game>();
            var uninstalledGames = new List<Game>();
            var globalProgressOptions =
                new GlobalProgressOptions($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstalling)}... ", false);
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async a =>
            {
                a.SetProgressMaxValue(games.Count);
                var counter = 0;
                var installedAppList = AmazonClientlessPlugin.Instance.InstalledAppList;
                foreach (var game in games)
                {
                    a.SetText($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstalling)} {game.Name}... ");
                    try
                    {
                        if (Directory.Exists(game.InstallDirectory))
                        {
                            Directory.Delete(game.InstallDirectory, true);
                        }

                        game.InstallState = InstallState.Uninstalled;
                        game.InstallDirectory = "";
                        game.InstallSize = 0;
                        if (removeGameLaunchSettingsCheckbox.IsSelected)
                        {
                            var gameSettingsFile = Path.Combine(Path.Combine(playniteApi.UserDataDir, "GamesSettings",
                                $"{game.LibraryGameId}.json"));
                            if (File.Exists(gameSettingsFile))
                            {
                                File.Delete(gameSettingsFile);
                            }
                        }

                        installedAppList.Remove(game.LibraryGameId!);
                        AmazonClientlessPlugin.Instance.InstalledAppListModified = true;
                        uninstalledGames.Add(game);
                        await playniteApi.Library.Games.UpdateAsync(game);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex.Message);
                        notUninstalledGames.Add(game);
                    }

                    counter += 1;
                    a.SetCurrentProgressValue(counter);
                }

                if (uninstalledGames.Count > 0)
                {
                    var uninstalledGamesList = uninstalledGames[0].Name;
                    if (uninstalledGames.Count > 1)
                    {
                        uninstalledGamesList = string.Join(", ", uninstalledGames.Select(item => item.Name));
                    }

                    await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonUninstallSuccess,
                        new Dictionary<string, IFluentType>
                            { ["appName"] = (FluentString)uninstalledGamesList, ["count"] = (FluentNumber)uninstalledGames.Count }));
                }

                if (notUninstalledGames.Count > 0)
                {
                    if (notUninstalledGames.Count == 1)
                    {
                        await playniteApi.Dialogs.ShowErrorMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameUninstallError,
                                new Dictionary<string, IFluentType>
                                    { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }),
                            notUninstalledGames[0].Name);
                    }
                    else
                    {
                        var notUninstalledGamesCombined = string.Join(", ", notUninstalledGames.Select(item => item.Name));
                        await playniteApi.Dialogs.ShowMessageAsync(
                            $"{LocalizationManager.Instance.GetString(LOC.CommonUninstallError, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)notUninstalledGamesCombined, ["count"] = (FluentNumber)notUninstalledGames.Count })} {LocalizationManager.Instance.GetString(LOC.CommonCheckLog)}");
                    }
                }
            });
        }
    }
}