using System.Diagnostics;
using System.IO;
using AmazonClientless.Models;
using AmazonClientless.Services;
using CommonPlugin;
using Playnite;
using PlayniteMod;
using SqlNado;

namespace AmazonClientless;

public class AmazonClientlessGames
{
    private static readonly SpecImportableProperty PcSpecProperty = new("pc_windows");
    public static string InstallationPath
    {
        get
        {
            var installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games");
            // var playniteApi = AmazonClientlessPlugin.PlayniteApi;
            // if (playniteAPI.ApplicationInfo.IsPortable)
            // {
            //     var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            //     installPath = Path.Combine(playniteDirectoryVariable, "Games");
            // }
            var savedSettings = AmazonClientlessPlugin.GetSettings();
            var savedGamesInstallationPath = savedSettings.GamesInstallationPath;
            if (savedGamesInstallationPath != "")
            {
                installPath = savedGamesInstallationPath;
            }

            return installPath;
        }
    }

    public static GameConfiguration? GetGameConfiguration(string gameDir)
    {
        var configFile = Path.Combine(gameDir, GameConfiguration.ConfigFileName);
        if (File.Exists(configFile))
        {
            var content = FileSystem.ReadFileAsStringSafe(configFile);
            if (!content.IsNullOrEmpty())
            {
                return Serialization.FromJson<GameConfiguration>(content);
            }
        }

        return null;
    }

    public static async Task CompleteGameInstallation(string gameId, string installDirectory)
    {
        var gameSettings = AmazonClientlessGameSettingsViewModel.LoadGameSettings(gameId);
        var gameConfig = GetGameConfiguration(installDirectory);
        if (gameConfig?.PostInstall.Count > 0)
        {
            foreach (var depend in gameConfig.PostInstall)
            {
                var dependExe = Path.GetFullPath(Path.Combine(installDirectory, depend.Command));
                if (File.Exists(dependExe))
                {
                    var cmd = new ProcessStartInfo();
                    cmd.ArgumentList.AddRangeIfNotNull(depend.Args);
                    cmd.UseShellExecute = true;
                    cmd.FileName = dependExe;
                    var process = ProcessStarter.StartProcess(cmd);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }
            }
        }

        gameSettings.IsFullyInstalled = true;
        var commonHelpers = AmazonClientlessPlugin.Instance.CommonHelpers;
        commonHelpers.SaveJsonSettingsToFile(gameSettings, "GamesSettings", gameId, true);
    }

    public static bool GetGameRequiresClient(GameConfiguration config)
    {
        return config.Main != null &&
               !config.Main.ClientId.IsNullOrEmpty() &&
               config.Main.AuthScopes.HasItems();
    }
    
    public static string NormalizeGameTitle(string gameTitle)
    {
        var newGameName = gameTitle.RemoveMarks().Replace("•", " ");
        if (newGameName.EndsWith(" - CE"))
        {
            newGameName = newGameName.TrimEndString("- CE") + "Collector's Edition";
        }
        else if (newGameName.EndsWith(" CE"))
        {
            newGameName = newGameName.TrimEndString("CE") + "Collector's Edition";
        }

        return newGameName;
    }
    
    public static Dictionary<string, InstalledGamesWrapper.Installed> GetInstalledGames()
    {
        var games = new Dictionary<string, InstalledGamesWrapper.Installed>();
        var nileAppList = AmazonClientlessLauncher.GetNileInstalledAppList();

        foreach (InstalledGamesWrapper.Installed installedGame in nileAppList)
        {
            installedGame.Name = NormalizeGameTitle(installedGame.Name);
            games.Add(installedGame.ID, installedGame);
        }

        // Add games installed using Amazon Games Launcher
        var amazonInstallSqlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite");
        if (File.Exists(amazonInstallSqlPath))
        {
            using var sql = new SQLiteDatabase(amazonInstallSqlPath, SQLiteOpenOptions.SQLITE_OPEN_READONLY);
            foreach (var program in sql.Load<GameConfiguration.AmazonLauncherInstallGameInfo>(@"SELECT * FROM DbSet WHERE Installed = 1;"))
            {
                if (!Directory.Exists(program.InstallDirectory))
                {
                    continue;
                }

                var installedMeta = new InstalledGamesWrapper.Installed
                {
                    ID = program.Id,
                    Name = NormalizeGameTitle(program.ProductTitle),
                    Path = program.InstallDirectory,
                    Version = program.InstallVersion
                };
                games.TryAdd(program.Id, installedMeta);
            }
        }

        return games;
    }

    public static Dictionary<string, ImportableGame> ConvertInstalledToImportableGames()
    {
        var installedGames = GetInstalledGames();
        var importableGames = new Dictionary<string, ImportableGame>();
        foreach (var installedGame in installedGames)
        {
            var game = new ImportableGame(installedGame.Value.Name, AmazonClientlessPlugin.Id, installedGame.Value.ID)
            {
                Source = new IdImportableProperty("Amazon"),
                InstallDirectory = installedGame.Value.Path,
                InstallState = InstallState.Installed,
                Platforms = [PcSpecProperty],
            };
            importableGames.Add(game.Id, game);
        }

        return importableGames;
    }
    
    public static async Task<List<ImportableGame>> GetLibraryGames()
    {
        var games = new List<ImportableGame>();
        var client = new AmazonAccountClient(AmazonClientlessPlugin.PlayniteApi);
        var entitlements = await client.GetAccountEntitlements();

        foreach (var item in entitlements)
        {
            if (item.Product.ProductLine == "Twitch:FuelEntitlement")
            {
                continue;
            }

            var gameName = NormalizeGameTitle(item.Product.Title);
            var game = new ImportableGame(gameName, AmazonClientlessPlugin.Id, item.Product.ID)
            {
                Source = new IdImportableProperty("Amazon"),
                InstallState = InstallState.Uninstalled,
                Platforms = [PcSpecProperty],
            };
            games.Add(game);
        }

        return games;
    }
    
    internal static void ClearCache()
    {
        var dataDir = AmazonClientlessPlugin.PlayniteApi.UserDataDir;
        var cacheDir = Path.Combine(dataDir, "cache");
        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, true);
        }
    }
}