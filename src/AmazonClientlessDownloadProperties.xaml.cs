using System.IO;
using System.Windows;
using System.Windows.Controls;
using AmazonClientless.Models;
using CommonPlugin;
using CommonPlugin.Enums;
using Playnite;
using UnifiedDownloadManagerApiNS.Models;

namespace AmazonClientless;

public partial class AmazonClientlessDownloadProperties : UserControl
{
    private readonly CommonHelpers commonHelpers = AmazonClientlessPlugin.Instance.CommonHelpers;
    private DownloadManagerData.Download SelectedDownload => (DownloadManagerData.Download)DataContext;
    private readonly IPlayniteApi playniteApi = AmazonClientlessPlugin.PlayniteApi;
    private long availableFreeSpace;

    public AmazonClientlessDownloadProperties()
    {
        InitializeComponent();
    }

    private void AmazonClientlessDownloadProperties_OnLoaded(object sender, RoutedEventArgs e)
    {
        commonHelpers.SetControlBackground(this);
        MaxWorkersNI.MaxValue = AmazonClientlessDownloadLogic.MaxMaxWorkers;
        SelectedGamePathTxt.Text = SelectedDownload.DownloadProperties.InstallPath;
        MaxWorkersNI.Value = SelectedDownload.DownloadProperties.MaxWorkers.ToString();
        TaskCBo.SelectedValue = SelectedDownload.DownloadProperties.DownloadAction;
        var downloadActionOptions = new Dictionary<DownloadAction, string>
        {
            { DownloadAction.Install, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame) },
            { DownloadAction.Repair, LocalizationManager.Instance.GetString(LOC.CommonRepair) },
            { DownloadAction.Update, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterInstallUpdate) }
        };
        TaskCBo.ItemsSource = downloadActionOptions;
        UpdateSpaceInfo(SelectedDownload.DownloadProperties.InstallPath);
        if (SelectedDownload.GameId == AmazonClientlessGames.AmazonGamesSdkId)
        {
            InstallPathDP.IsEnabled = false;
        }

        var unifiedDownloadManagerApi = AmazonClientlessPlugin.Instance.UnifiedDownloadManagerApi;
        var wantedItem = unifiedDownloadManagerApi.GetTask(SelectedDownload.GameId, AmazonClientlessPlugin.Id);
        if (wantedItem?.Status is UnifiedDownloadStatus.Completed or UnifiedDownloadStatus.Running)
        {
            SaveBtn.IsEnabled = false;
        }
        if (wantedItem?.Status != UnifiedDownloadStatus.Completed)
        {
            SizeGrd.Visibility = Visibility.Visible;
        }
    }

    private async void ChooseGamePathBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var folders = await playniteApi.Dialogs.SelectFolderAsync();
        if (folders is { Count: > 0 } && folders[0] != "")
        {
            SelectedGamePathTxt.Text = folders[0];
            UpdateSpaceInfo(SelectedDownload.DownloadProperties.InstallPath);
        }
    }

    private async void SaveBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var wantedItem =
            AmazonClientlessPlugin.Instance.PluginDownloadData.Downloads.First(item =>
                item.GameId == SelectedDownload.GameId);
        var installPath = SelectedGamePathTxt.Text;
        var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory;
        if (installPath.Contains(playniteDirectoryVariable))
        {
            installPath = installPath.Replace(playniteDirectoryVariable, playniteApi.AppInfo.ApplicationDirectory);
        }

        if (!await commonHelpers.IsDirectoryWritable(installPath, LOC.CommonPermissionError))
        {
            return;
        }

        wantedItem.DownloadProperties.InstallPath = installPath;
        wantedItem.DownloadProperties.DownloadAction = (DownloadAction)TaskCBo.SelectedValue;
        wantedItem.DownloadProperties.MaxWorkers = int.Parse(MaxWorkersNI.Value);
        AmazonClientlessDownloadLogic.SaveDownloadData();
        Window.GetWindow(this)?.Close();
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
        double afterInstallSizeNumber = availableFreeSpace - SelectedDownload.DownloadSizeNumber;
        if (afterInstallSizeNumber < 0)
        {
            afterInstallSizeNumber = 0;
        }

        AfterInstallingTB.Text = CommonHelpers.FormatSize(afterInstallSizeNumber);
    }
}