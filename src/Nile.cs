using System.IO;
using AmazonClientless.Models;
using CommonPlugin;
using PlayniteMod;

namespace AmazonClientless;

public class Nile
{
    private static string ConfigPath
    {
        get
        {
            var nileConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nile");
            var heroicNileConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic",
                "nile_config", "nile");
            var originalNileInstallListPath = Path.Combine(nileConfigPath, "installed.json");
            var heroicNileInstallListPath = Path.Combine(heroicNileConfigPath, "installed.json");
            if (File.Exists(heroicNileInstallListPath))
            {
                if (File.Exists(originalNileInstallListPath))
                {
                    if (File.GetLastWriteTime(heroicNileInstallListPath) > File.GetLastWriteTime(originalNileInstallListPath))
                    {
                        nileConfigPath = heroicNileConfigPath;
                    }
                }
                else
                {
                    nileConfigPath = heroicNileConfigPath;
                }
            }

            var envNileConfigPath = Environment.GetEnvironmentVariable("NILE_CONFIG_PATH");
            if (!envNileConfigPath.IsNullOrWhiteSpace() && Directory.Exists(envNileConfigPath))
            {
                nileConfigPath = envNileConfigPath;
            }

            return nileConfigPath;
        }
    }

    public static List<InstalledGamesWrapper.Installed> GetInstalledAppList()
    {
        var installListPath = Path.Combine(ConfigPath, "installed.json");
        var list = new List<InstalledGamesWrapper.Installed>();
        if (File.Exists(installListPath))
        {
            var content = FileSystem.ReadFileAsStringSafe(installListPath);
            if (!content.IsNullOrWhiteSpace() &&
                Serialization.TryFromJson(content, out List<InstalledGamesWrapper.Installed>? nonEmptyList))
            {
                if (nonEmptyList != null)
                {
                    list = nonEmptyList;
                }
            }
        }

        foreach (var app in list)
        {
            var installLocation = app.Path;
            if (installLocation.IsNullOrEmpty())
            {
                continue;
            }

            installLocation = Paths.FixSeparators(installLocation);
            app.Path = installLocation;
            var gameName = new DirectoryInfo(installLocation).Name;
            var nileLibSyncJsonPath = Path.Combine(ConfigPath, "library.json");
            if (File.Exists(nileLibSyncJsonPath))
            {
                var nileLibyncJsonContent = FileSystem.ReadFileAsStringSafe(nileLibSyncJsonPath);
                if (!nileLibyncJsonContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(nileLibyncJsonContent,
                        out List<NileLibraryFile.NileGames>? newNileLibSyncJson))
                {
                    var wantedGame = newNileLibSyncJson?.FirstOrDefault(i => i.Product.ID == app.ID);
                    if (wantedGame != null)
                    {
                        gameName = wantedGame.Product.Title.RemoveMarks();
                    }
                }
            }

            app.Name = gameName;
        }

        return list;
    }
}