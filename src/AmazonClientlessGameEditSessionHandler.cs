using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using CommonPlugin;
using Playnite;

namespace AmazonClientless;

public class AmazonClientlessGameEditSessionHandler(Game game) : GameEditSessionHandler
{
    private AmazonClientlessGameSettingsViewModel? gameSettingsViewModel;

    public override async Task<List<GameEditSessionSection>> GetEditSectionsAsync(GetEditSectionsAsyncArgs args)
    {
        gameSettingsViewModel = new AmazonClientlessGameSettingsViewModel(game);
        var gameSettingsView = new AmazonClientlessGameSettingsView
        {
            DataContext = gameSettingsViewModel
        };
        return
        [
            new GameEditSessionSection(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameLaunching), gameSettingsView),
        ];
    }

    public override async Task EndEditAsync(EndEditArgs args)
    {
        gameSettingsViewModel?.Save();
    }

    public override bool GetHasUnsavedChanges(GetHasUnsavedChangesArgs args)
    {
        if (gameSettingsViewModel == null)
        {
            return false;
        }

        var oldGameSettings = AmazonClientlessGameSettingsViewModel.LoadGameSettings(game.LibraryGameId!);
        var newGameSettings = gameSettingsViewModel.PrepareNewGameSettings();
        return Serialization.ToJson(newGameSettings) != Serialization.ToJson(oldGameSettings);
    }
}