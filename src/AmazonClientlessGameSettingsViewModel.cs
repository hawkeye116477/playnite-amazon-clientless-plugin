using System.IO;
using AmazonClientless.Enums;
using AmazonClientless.Models;
using CommonPlugin;
using CommunityToolkit.Mvvm.ComponentModel;
using Playnite;

namespace AmazonClientless;

public partial class AmazonClientlessGameSettingsViewModel : ObservableObject
{
    public Game Game { get; set; }
    private CommonHelpers commonHelpers = AmazonClientlessPlugin.Instance.CommonHelpers;

    [ObservableProperty]
    public partial GameSettings ChosenGameSettings { get; set; }

    [ObservableProperty]
    public partial string StartupArgumentsTxt { get; set; }

    public AmazonClientlessGameSettingsViewModel(Game game)
    {
        Game = game;
        ChosenGameSettings = LoadGameSettings(game.LibraryGameId!, true);
        if (ChosenGameSettings.StartupArguments is { Count: > 0 })
        {
            StartupArgumentsTxt = string.Join(" ",
                ChosenGameSettings.StartupArguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        }
        else
        {
            StartupArgumentsTxt = "";
        }
    }

    public static GameSettings LoadGameSettings(string gameId, bool init = false)
    {
        var playniteApi = AmazonClientlessPlugin.PlayniteApi;
        var gameSettings = new GameSettings();
        var gameSettingsFile = Path.Combine(playniteApi.UserDataDir, "GamesSettings", $"{gameId}.json");
        if (File.Exists(gameSettingsFile) &&
            Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(gameSettingsFile), out GameSettings? savedGameSettings))
        {
            if (savedGameSettings != null)
            {
                gameSettings = savedGameSettings;
            }
        }

        if (init)
        {
            var globalSettings = AmazonClientlessPlugin.GetSettings();
            if (gameSettings.DisableGameVersionCheck == null
                && globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
            {
                gameSettings.DisableGameVersionCheck = true;
            }
        }

        return gameSettings;
    }

    public GameSettings PrepareNewGameSettings()
    {
        var globalSettings = AmazonClientlessPlugin.GetSettings();
        var newGameSettings = new GameSettings();
        var globalDisableUpdates = globalSettings.GamesUpdatePolicy == UpdatePolicy.Never;

        if (ChosenGameSettings.DisableGameVersionCheck != globalDisableUpdates)
        {
            newGameSettings.DisableGameVersionCheck = ChosenGameSettings.DisableGameVersionCheck;
        }

        if (StartupArgumentsTxt != "")
        {
            newGameSettings.StartupArguments = CommonHelpers.SplitArguments(StartupArgumentsTxt).ToList();
        }

        newGameSettings.IsFullyInstalled = ChosenGameSettings.IsFullyInstalled;
        return newGameSettings;
    }

    public void Save()
    {
        var newGameSettings = PrepareNewGameSettings();
        if (newGameSettings.GetType().GetProperties().Any(p => p.GetValue(newGameSettings) != null))
        {
            commonHelpers.SaveJsonSettingsToFile(newGameSettings, "GamesSettings", Game.LibraryGameId!, true);
        }
    }
}