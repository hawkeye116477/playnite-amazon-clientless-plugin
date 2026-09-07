using System.Windows;
using AmazonClientless.Models;
using CommonPlugin;
using CommonPlugin.Enums;
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