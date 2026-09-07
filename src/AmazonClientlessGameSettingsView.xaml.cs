using System.Windows;
using System.Windows.Controls;
using CommonPlugin;
using Playnite;

namespace AmazonClientless;

public partial class AmazonClientlessGameSettingsView : UserControl
{
    private AmazonClientlessGameSettingsViewModel Vm => (DataContext as AmazonClientlessGameSettingsViewModel)!;
    private CommonHelpers commonHelpers = AmazonClientlessPlugin.Instance.CommonHelpers;
    private Game Game => Vm.Game;

    public AmazonClientlessGameSettingsView()
    {
        InitializeComponent();
    }

    private void AmazonClientlessGameSettingsView_OnLoaded(object sender, RoutedEventArgs e)
    {
        commonHelpers.SetControlBackground(this);
        var appList = AmazonClientlessGames.GetAllInstalledGames();
        if (appList.TryGetValue(Game.LibraryGameId!, out var installed))
        {
            GameVersionTxt.Text = installed.Version;
        }
        else
        {
            VersionSP.Visibility = Visibility.Collapsed;
        }
    }
}