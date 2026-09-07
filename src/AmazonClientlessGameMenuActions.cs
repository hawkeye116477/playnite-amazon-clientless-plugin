using System.Windows;
using AmazonClientless.Models;
using CommonPlugin;
using CommonPlugin.Enums;
using Playnite;

namespace AmazonClientless;

public static class AmazonClientlessGameMenuActions
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(AmazonClientlessGameMenuActions));
    private static IPlayniteApi PlayniteApi { get; set; } = AmazonClientlessPlugin.PlayniteApi;

    public static async Task OpenCheckForGamesUpdatesWindow()
    {
    }

    public static async Task OpenMoveGameWindow()
    {
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
            var installProperties = new DownloadProperties { DownloadAction = DownloadAction.Repair, InstallPath = CommonHelpers.NormalizePath(game.InstallDirectory!) };
            installData.Add(new DownloadManagerData.Download
            {
                GameId = game.LibraryGameId!,
                Name = game.Name,
                DownloadProperties = installProperties
            });
        }
        AmazonClientlessInstallController.LaunchInstaller(installData);
    }
}