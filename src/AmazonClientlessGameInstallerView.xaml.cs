using System.IO;
using System.Windows;
using System.Windows.Controls;
using AmazonClientless.Models;
using AmazonClientless.Services;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using Playnite;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace AmazonClientless;

public partial class AmazonClientlessGameInstallerView : UserControl
{
    private static readonly ILogger Logger = LogManager.GetLogger<AmazonClientlessGameInstallerView>();
    private readonly CommonHelpers commonHelpers = AmazonClientlessPlugin.Instance.CommonHelpers;
    private readonly IPlayniteApi playniteApi = AmazonClientlessPlugin.PlayniteApi;
    private long availableFreeSpace;
    private double downloadSizeNumber;

    public List<DownloadManagerData.Download> MultiInstallData
    {
        get => (List<DownloadManagerData.Download>)DataContext;
        set { }
    }

    private Window InstallerWindow => Window.GetWindow(this)!;

    public AmazonClientlessGameInstallerView()
    {
        InitializeComponent();
    }

    private async void AmazonClientlessGameInstallerView_OnLoaded(object sender, RoutedEventArgs e)
    {
        var isUdmInstalled = await AmazonClientlessDownloadLogic.CheckIfUdmInstalled();
        if (!isUdmInstalled)
        {
            Window.GetWindow(this)?.Close();
        }

        commonHelpers.SetControlBackground(this);
        if (MultiInstallData.First().DownloadProperties.DownloadAction == DownloadAction.Repair)
        {
            FolderDP.Visibility = Visibility.Collapsed;
            InstallBtn.Visibility = Visibility.Collapsed;
            RepairBtn.Visibility = Visibility.Visible;
            AfterInstallingSP.Visibility = Visibility.Collapsed;
        }

        var settings = AmazonClientlessPlugin.GetSettings();
        var installPath = AmazonClientlessGames.InstallationPath;
        var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory;
        if (installPath.Contains(playniteDirectoryVariable))
        {
            installPath = installPath.Replace(playniteDirectoryVariable, playniteApi.AppInfo.ApplicationDirectory);
        }

        SelectedGamePathTxt.Text = installPath;
        MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
        MaxWorkersNI.Value = settings.MaxWorkers.ToString();

        await RefreshAll();

        if (settings.UnattendedInstall &&
            MultiInstallData.First().DownloadProperties.DownloadAction == DownloadAction.Install)
        {
            await StartTask(DownloadAction.Install, true);
        }
    }

    private async Task RefreshAll()
    {
        InstallBtn.IsEnabled = false;
        ReloadBtn.IsEnabled = false;
        UpdateSpaceInfo(SelectedGamePathTxt.Text);
        downloadSizeNumber = 0;
        bool gamesListShouldBeDisplayed = false;

        var installedSdkManifestFile = Path.Combine(AmazonClientlessGames.AmazonGamesSdkInstallationPath, ".manifest_ac", "manifest.json");
        if (!File.Exists(installedSdkManifestFile))
        {
            var sdkInstallTask = new DownloadManagerData.Download
            {
                FullInstallPath = AmazonClientlessGames.AmazonGamesSdkInstallationPath,
                GameId = AmazonClientlessGames.AmazonGamesSdkId,
                Name = "Amazon Games SDK",
                DownloadProperties =
                {
                    InstallPath = AmazonClientlessGames.AmazonGamesSdkBaseInstallationPath
                }
            };
            MultiInstallData.Add(sdkInstallTask);
        }

        var clientApi = new AmazonAccountClient(AmazonClientlessPlugin.PlayniteApi);
        foreach (var installData in MultiInstallData.ToList())
        {
            var manifest = await clientApi.GetGameManifest(installData.GameId, installData.Name);
            if (manifest.ErrorDisplayed)
            {
                gamesListShouldBeDisplayed = true;
                MultiInstallData.Remove(installData);
                continue;
            }

            foreach (var file in manifest.AllFiles)
            {
                if (file.Size != null)
                {
                    installData.DownloadSizeNumber += (double)file.Size;
                }
            }

            downloadSizeNumber += installData.DownloadSizeNumber;
        }

        var games = MultiInstallData.Where(i => i.GameId != AmazonClientlessGames.AmazonGamesSdkId).ToList();
        GamesLB.ItemsSource = games;
        if (games.Count > 1 || gamesListShouldBeDisplayed)
        {
            GamesBrd.Visibility = Visibility.Visible;
        }

        if (downloadSizeNumber != 0)
        {
            InstallBtn.IsEnabled = true;
        }

        ReloadBtn.IsEnabled = true;
        UpdateAfterInstallingSize();
        DownloadSizeTB.Text = CommonHelpers.FormatSize(downloadSizeNumber);
        InstallSizeTB.Text = CommonHelpers.FormatSize(downloadSizeNumber);
        if (games.Count == 0 || gamesListShouldBeDisplayed)
        {
            await playniteApi.Dialogs.ShowErrorMessageAsync(
                LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMetadataDownloadError,
                    new Dictionary<string, IFluentType>
                    {
                        ["var0"] =
                            (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog)
                    }), "");
        }
    }

    private void UpdateSpaceInfo(string path)
    {
        DriveInfo dDrive = new DriveInfo(path);
        if (dDrive.IsReady)
        {
            availableFreeSpace = dDrive.AvailableFreeSpace;
            SpaceTB.Text = CommonHelpers.FormatSize(availableFreeSpace);
        }

        UpdateAfterInstallingSize();
    }

    private void UpdateAfterInstallingSize()
    {
        double afterInstallSizeNumber = availableFreeSpace - downloadSizeNumber;
        if (afterInstallSizeNumber < 0)
        {
            afterInstallSizeNumber = 0;
        }

        AfterInstallingTB.Text = CommonHelpers.FormatSize(afterInstallSizeNumber);
    }

    private async void ChooseGamePathBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var result = await playniteApi.Dialogs.SelectFolderAsync();
        if (result is { Count: > 0 } && result[0] != "")
        {
            SelectedGamePathTxt.Text = result[0];
            UpdateSpaceInfo(result[0]);
        }
    }

    private async Task StartTask(DownloadAction downloadAction, bool silently = false)
    {
        var clientApi = new AmazonAccountClient(AmazonClientlessPlugin.PlayniteApi);
        var userLoggedIn = await clientApi.GetIsUserLoggedIn();
        if (!userLoggedIn)
        {
            var loginErrorMessage = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError,
                new Dictionary<string, IFluentType>
                {
                    ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired)
                });
            await playniteApi.Dialogs.ShowErrorMessageAsync("", loginErrorMessage);
            InstallerWindow.Close();
            return;
        }

        var installPath = SelectedGamePathTxt.Text;
        if (installPath == "")
        {
            installPath = AmazonClientlessGames.InstallationPath;
        }

        var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory;
        if (installPath.Contains(playniteDirectoryVariable))
        {
            installPath = installPath.Replace(playniteDirectoryVariable, playniteApi.AppInfo.ApplicationDirectory);
        }

        InstallerWindow.Close();
        var downloadTasks = new List<DownloadManagerData.Download>();

        foreach (var installData in MultiInstallData)
        {
            if (!await commonHelpers.IsDirectoryWritable(installPath, LOC.CommonPermissionError))
            {
                continue;
            }

            if (installData.DownloadProperties.InstallPath.IsNullOrEmpty())
            {
                var folderName = installData.Name;
                string[] inappropriateDirChars = [":", "/", "*", "?", "<", ">", "\\", "|", "™", "\"", "®"];
                foreach (var inappropriateDirChar in inappropriateDirChars)
                {
                    folderName = folderName.Replace(inappropriateDirChar, "");
                }

                installData.FullInstallPath = Path.Combine(installPath, folderName);
                installData.DownloadProperties.InstallPath = installPath;
            }

            var downloadProperties = GetDownloadProperties(installData, downloadAction);
            installData.DownloadProperties = downloadProperties;
            downloadTasks.Add(installData);
        }

        if (downloadTasks.Count > 0)
        {
            var pluginDownloadLogic = new AmazonClientlessDownloadLogic();
            await pluginDownloadLogic.AddTasks(downloadTasks, silently);
        }
    }

    private DownloadProperties GetDownloadProperties(DownloadManagerData.Download installData, DownloadAction downloadAction)
    {
        var settings = AmazonClientlessPlugin.GetSettings();
        int maxWorkers = settings.MaxWorkers;
        if (MaxWorkersNI.Value != "")
        {
            maxWorkers = int.Parse(MaxWorkersNI.Value);
        }

        var newDownloadProperties = installData.DownloadProperties.GetClone();
        newDownloadProperties.DownloadAction = downloadAction;
        newDownloadProperties.MaxWorkers = maxWorkers;
        return newDownloadProperties;
    }

    private async void InstallBtn_OnClick(object sender, RoutedEventArgs e)
    {
        await StartTask(DownloadAction.Install);
    }

    private async void ReloadBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var result = await playniteApi.Dialogs.ShowMessageAsync(
            LocalizationManager.Instance.GetString(LOC.CommonReloadConfirm),
            LocalizationManager.Instance.GetString(LOC.CommonReload), MessageBoxButtons.YesNo,
            MessageBoxSeverity.Question);
        if (result == MessageBoxResult.Yes)
        {
            InstallBtn.IsEnabled = false;
            DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
            InstallSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
            AfterInstallingTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);

            var gameIds = MultiInstallData.Select(g => g.GameId).ToList();
            AmazonClientlessCache.ClearSpecificGamesCache(gameIds);

            await RefreshAll();
        }
    }

    private async void RepairBtn_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var installData in MultiInstallData)
        {
            installData.DownloadSizeNumber = 0;
        }

        await StartTask(DownloadAction.Repair);
    }
}