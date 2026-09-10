using System.Windows;
using System.Windows.Controls;
using AmazonClientless.Models;
using CommonPlugin;
using Playnite;

namespace AmazonClientless;

public partial class AmazonClientlessUpdaterView : UserControl
{
    private Dictionary<string, UpdateInfo> updatesList = [];
    private readonly IPlayniteApi playniteApi = AmazonClientlessPlugin.PlayniteApi;
    private readonly CommonHelpers commonHelpers = AmazonClientlessPlugin.Instance.CommonHelpers;

    public AmazonClientlessUpdaterView()
    {
        InitializeComponent();
    }

    private async void AmazonClientlessUpdaterView_OnLoaded(object sender, RoutedEventArgs e)
    {
        var isUdmInstalled = await AmazonClientlessDownloadLogic.CheckIfUdmInstalled();
        if (!isUdmInstalled)
        {
            Window.GetWindow(this)?.Close();
            return;
        }
        updatesList = (Dictionary<string, UpdateInfo>)DataContext;
        commonHelpers.SetControlBackground(this);
        RefreshWindow();
        var settings = AmazonClientlessPlugin.GetSettings();
        MaxWorkersNI.MaxValue = AmazonClientlessDownloadLogic.MaxMaxWorkers;
        MaxWorkersNI.Value = settings.MaxWorkers.ToString();
                var successUpdates = updatesList.Where(i => i.Value.Status == UpdateStatus.Available).ToDictionary(i => i.Key, i => i.Value);

        var checkedGames = updatesList.Where(i => i.Value.Status != UpdateStatus.Available)
                                      .ToDictionary(i => i.Key, i => i.Value);
        var failedGames = checkedGames.Any(i => i.Value.Status == UpdateStatus.Error);
        if (checkedGames.Count > 0 && successUpdates.Count == 0)
        {
            Window.GetWindow(this)?.Visibility = Visibility.Collapsed;
            var noUpdatesMessage = LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable);
            var noUpdatesSeverity = MessageBoxSeverity.Information;
            if (failedGames)
            {
                noUpdatesMessage = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage);
                noUpdatesSeverity = MessageBoxSeverity.Error;
            }

            var options = new List<MessageBoxResponse>
            {
                new(LocalizationManager.Instance.GetString(LOC.CommonReload)),
                new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel), true, true)
            };
            var result = await playniteApi.Dialogs.ShowMessageAsync(
                noUpdatesMessage, AmazonClientlessPlugin.ShortPluginName,
                noUpdatesSeverity, options, []);
            if (result == options[0])
            {
                var checkedGamesIds = checkedGames.Select(g => g.Key).ToList();
                var updateCheckProgressOptions =
                    new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false)
                        { IsIndeterminate = true };
                await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions, async a =>
                {
                    AmazonClientlessCache.ClearSpecificGamesCache(checkedGamesIds);
                    var legendaryUpdateController = new AmazonClientlessUpdateController();
                    if (checkedGamesIds.Count > 1)
                    {
                        updatesList = await legendaryUpdateController.CheckAllGamesUpdates(true);
                    }
                    else
                    {
                        updatesList = await legendaryUpdateController.CheckGameUpdates(checkedGames.First().Value.Title,
                            checkedGames.First().Key);
                    }
                });
                if (updatesList.All(i => i.Value.Status != UpdateStatus.Available))
                {
                    await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable),
                        AmazonClientlessPlugin.ShortPluginName);
                    Window.GetWindow(this)?.Close();
                    return;
                }

                Window.GetWindow(this)?.Visibility = Visibility.Visible;
                RefreshWindow();
            }
            else
            {
                Window.GetWindow(this)?.Close();
            }
        }
    }
    
    private void RefreshWindow()
    {
        UpdateBtn.IsEnabled = false;
        DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
        InstallSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);

        var successUpdates = updatesList.Where(i => i.Value.Status == UpdateStatus.Available).ToDictionary(i => i.Key, i => i.Value);
        UpdatesLB.ItemsSource = successUpdates;
        UpdatesLB.SelectAll();
        if (updatesList.Count > 0)
        {
            UpdateBtn.IsEnabled = true;
        }
    }

    private void UpdatesLB_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateBtn.IsEnabled = UpdatesLB.SelectedIndex != -1;
        double initialDownloadSizeNumber = 0;
        foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, UpdateInfo>>().ToList())
        {
            initialDownloadSizeNumber += selectedOption.Value.Download_size;
        }

        var downloadSize = CommonHelpers.FormatSize(initialDownloadSizeNumber);
        DownloadSizeTB.Text = downloadSize;
        InstallSizeTB.Text = downloadSize;
    }
}