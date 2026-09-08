using System.IO;
using System.Windows;
using AmazonClientless.Models;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using Playnite;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace AmazonClientless;

public static class AmazonClientlessGameMenuActions
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(AmazonClientlessGameMenuActions));
    private static IPlayniteApi PlayniteApi { get; set; } = AmazonClientlessPlugin.PlayniteApi;

    public static async Task OpenCheckForGamesUpdatesWindow()
    {
    }

    public static async Task OpenMoveGameWindow(Game game)
    {
        var folders = await PlayniteApi.Dialogs.SelectFolderAsync();
        var newPath = folders?.FirstOrDefault();
        if (!newPath.IsNullOrEmpty())
        {
            var oldPath = game.InstallDirectory;
            if (Directory.Exists(oldPath) && Directory.Exists(newPath))
            {
                string sepChar = Path.DirectorySeparatorChar.ToString();
                string altChar = Path.AltDirectorySeparatorChar.ToString();
                if (!oldPath.EndsWith(sepChar) && !oldPath.EndsWith(altChar))
                {
                    oldPath += sepChar;
                }

                var folderName = Path.GetFileName(Path.GetDirectoryName(oldPath));
                newPath = Path.Combine(newPath, folderName!);
                var moveFluentArgs = new Dictionary<string, IFluentType>
                {
                    ["appName"] = (FluentString)game.Name,
                    ["path"] = (FluentString)newPath
                };
                var moveConfirm = await PlayniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonMoveConfirm,
                        moveFluentArgs), LocalizationManager.Instance.GetString(LOC.CommonMove),
                    MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                if (moveConfirm == MessageBoxResult.Yes)
                {
                    var globalProgressOptions =
                        new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMovingGame, moveFluentArgs), false);
                    await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async a =>
                    {
                        a.SetProgressMaxValue(3);
                        a.SetCurrentProgressValue(0);
                        _ = (Application.Current.Dispatcher?.BeginInvoke((Action)async delegate
                        {
                            try
                            {
                                Directory.Move(oldPath, newPath);
                                a.SetCurrentProgressValue(1);
                                var installedAppList = AmazonClientlessPlugin.Instance.InstalledAppList;
                                if (installedAppList.TryGetValue(game.LibraryGameId!, out var installedApp))
                                {
                                    installedApp.Path = newPath;
                                    AmazonClientlessPlugin.Instance.InstalledAppListModified = true;
                                }

                                a.SetCurrentProgressValue(2);
                                game.InstallDirectory = newPath;
                                await PlayniteApi.Library.Games.UpdateAsync(game);
                                a.SetCurrentProgressValue(3);
                                await PlayniteApi.Dialogs.ShowMessageAsync(
                                    LocalizationManager.Instance.GetString(
                                        LOC.CommonMoveGameSuccess, moveFluentArgs));
                            }
                            catch (Exception e)
                            {
                                a.SetCurrentProgressValue(3);
                                await PlayniteApi.Dialogs.ShowErrorMessageAsync(
                                    LocalizationManager.Instance.GetString(
                                        LOC.CommonMoveGameError, moveFluentArgs));
                                Logger.Error(e.Message);
                            }
                        }));
                    });
                }
            }
        }
    }

    public static void OpenInstallerWindow(List<Game> games)
    {
        var installData = new List<DownloadManagerData.Download>();
        foreach (var notInstalledLegendaryGame in games)
        {
            var installProperties = new DownloadProperties { DownloadAction = DownloadAction.Install };
            installData.Add(new DownloadManagerData.Download
            {
                GameId = notInstalledLegendaryGame.LibraryGameId ?? "",
                Name = notInstalledLegendaryGame.Name,
                DownloadProperties = installProperties
            });
        }

        AmazonClientlessInstallController.LaunchInstaller(installData);
    }

    public static void OpenRepairWindow(List<Game> games)
    {
        var installData = new List<DownloadManagerData.Download>();
        foreach (var game in games)
        {
            var installProperties = new DownloadProperties
                { DownloadAction = DownloadAction.Repair, InstallPath = CommonHelpers.NormalizePath(game.InstallDirectory!) };
            installData.Add(new DownloadManagerData.Download
            {
                GameId = game.LibraryGameId!,
                Name = game.Name,
                DownloadProperties = installProperties,
                FullInstallPath = installProperties.InstallPath,
            });
        }

        AmazonClientlessInstallController.LaunchInstaller(installData);
    }
}