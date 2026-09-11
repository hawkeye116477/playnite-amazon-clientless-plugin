using System.IO;
using System.Windows;
using System.Windows.Controls;
using AmazonClientless.Enums;
using AmazonClientless.Services;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using Playnite;
using PlayniteMod;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace AmazonClientless
{
    /// <summary>
    /// Interaction logic for AmazonClientlessSettingsView.xaml
    /// </summary>
    public partial class AmazonClientlessSettingsView : UserControl
    {
        private readonly ILogger logger = LogManager.GetLogger<AmazonClientlessSettingsView>();

        public AmazonClientlessSettingsView()
        {
            InitializeComponent();
        }

        private async Task UpdateAuthStatus(bool accountConnected = false)
        {
            if (AmazonClientlessPlugin.GetSettings().ConnectAccount || accountConnected)
            {
                LoginBtn.IsEnabled = false;
                AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonLoginChecking);
                var clientApi = new AmazonAccountClient();
                var userLoggedIn = await clientApi.GetIsUserLoggedIn();
                if (userLoggedIn)
                {
                    AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSignedInAs,
                        new Dictionary<string, IFluentType> { ["userName"] = (FluentString)clientApi.GetUsername() });
                    LoginBtn.Content = LocalizationManager.Instance.GetString(LOC.CommonSignOut);
                    LoginBtn.IsChecked = true;
                }
                else
                {
                    AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonNotLoggedIn);
                    LoginBtn.Content = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonAuthenticateLabel);
                    LoginBtn.IsChecked = false;
                }

                LoginBtn.IsEnabled = true;
            }
            else
            {
                AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonNotLoggedIn);
                LoginBtn.IsEnabled = true;
            }
        }

        private async void AmazonConnectAccountChk_Checked(object sender, RoutedEventArgs e)
        {
            await UpdateAuthStatus();
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var userLoggedIn = LoginBtn.IsChecked;
            var clientApi = new AmazonAccountClient();
            if (!userLoggedIn == false)
            {
                try
                {
                    await clientApi.Login();
                }
                catch (Exception ex)
                {
                    await AmazonClientlessPlugin.PlayniteApi.Dialogs.ShowErrorMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonNotLoggedInError), "");
                    logger.Error(ex, "Failed to authenticate user.");
                }

                await UpdateAuthStatus(true);
            }
            else
            {
                var answer = await AmazonClientlessPlugin.PlayniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonSignOutConfirm),
                    LocalizationManager.Instance.GetString(LOC.CommonSignOut), MessageBoxButtons.YesNo);
                if (answer == MessageBoxResult.Yes)
                {
                    await clientApi.LogOut();
                    await UpdateAuthStatus();
                }
                else
                {
                    LoginBtn.IsChecked = true;
                }
            }
        }

        private void GamesUpdatesCBo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedValue = (KeyValuePair<UpdatePolicy, string>)GamesUpdatesCBo.SelectedItem;
            if (selectedValue.Key == UpdatePolicy.Never)
            {
                AutoUpdateGamesChk.IsEnabled = false;
            }
            else
            {
                AutoUpdateGamesChk.IsEnabled = true;
            }
        }

        private async void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await AmazonClientlessPlugin.PlayniteApi.Dialogs.SelectFolderAsync();
            if (result is { Count: > 0 })
            {
                SelectedGamePathTxt.Text = result[0];
            }
        }


        private async void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await AmazonClientlessPlugin.PlayniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonClearCacheConfirm),
                LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsClearCacheTitle),
                MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
            if (result == MessageBoxResult.Yes)
            {
                AmazonClientlessCache.ClearCache();
            }
        }

        private async void OpenGamesInstallationPathBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(AmazonClientlessTroubleshootingInformation.GamesInstallationPath))
            {
                ProcessStarter.StartProcess(AmazonClientlessTroubleshootingInformation.GamesInstallationPath);
            }
            else
            {
                await AmazonClientlessPlugin.PlayniteApi.Dialogs.ShowErrorMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonPathNotExistsError));
            }
        }

        private void CopyRawDataBtn_Click(object sender, RoutedEventArgs e)
        {
            var troubleshootingInformationTxt = new
            {
                AmazonClientlessTroubleshootingInformation.PlayniteVersion,
                AmazonClientlessTroubleshootingInformation.PluginVersion,
                AmazonClientlessTroubleshootingInformation.GamesInstallationPath,
            };
            var troubleshootingJson = Serialization.ToJson(troubleshootingInformationTxt, true);
            Clipboard.SetText(troubleshootingJson);
        }


        private async void AmazonClientlessSettingsView_OnInitialized(object? sender, EventArgs e)
        {
            await UpdateAuthStatus();
            var updatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                {
                    UpdatePolicy.PlayniteLaunch,
                    LocalizationManager.Instance.GetString(LOC.CommonCheckUpdatesEveryPlayniteStartup)
                },
                { UpdatePolicy.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { UpdatePolicy.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { UpdatePolicy.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { UpdatePolicy.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { UpdatePolicy.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnlyManually) }
            };
            GamesUpdatesCBo.ItemsSource = updatePolicyOptions;

            var autoClearOptions = new Dictionary<ClearCacheTime, string>
            {
                { ClearCacheTime.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { ClearCacheTime.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { ClearCacheTime.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { ClearCacheTime.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { ClearCacheTime.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                {
                    ClearCacheTime.Never,
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsPlaytimeImportModeNever)
                }
            };
            AutoClearCacheCBo.ItemsSource = autoClearOptions;

            PlayniteVersionTxt.Text = AmazonClientlessTroubleshootingInformation.PlayniteVersion;
            PluginVersionTxt.Text = AmazonClientlessTroubleshootingInformation.PluginVersion ?? "";
            GamesInstallationPathTxt.Text = AmazonClientlessTroubleshootingInformation.GamesInstallationPath;
            ReportBugHyp.NavigateUri = new Uri(
                $"https://github.com/hawkeye116477/playnite-amazon-clientless-plugin/issues/new?assignees=&labels=bug&projects=&template=bugs.yml&pluginV={AmazonClientlessTroubleshootingInformation.PluginVersion}&playniteV={AmazonClientlessTroubleshootingInformation.PlayniteVersion}");
        }
    }
}