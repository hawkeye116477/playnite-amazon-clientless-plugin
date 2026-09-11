using System.IO;
using AmazonClientless.Models;
using AmazonClientless.Services;
using CommonPlugin;
using CommonPlugin.Enums;
using Playnite;

namespace AmazonClientless;

public class AmazonClientlessUpdateController
{
    private readonly ILogger logger = LogManager.GetLogger<AmazonClientlessPlayController>();
    private readonly IPlayniteApi playniteApi = AmazonClientlessPlugin.PlayniteApi;

    public async Task<Dictionary<string, UpdateInfo>> CheckGameUpdates(string gameId, string gameTitle)
    {
        var gameToUpdate = new Dictionary<string, UpdateInfo>();
        var clientApi = new AmazonAccountClient();
        var gameManifest = await clientApi.GetGameManifest(gameId, gameTitle);
        var allInstalledGames = AmazonClientlessGames.GetAllInstalledGames();
        allInstalledGames.TryGetValue(gameId, out var installedInfo);
        if (installedInfo != null)
        {
            var oldVersion = installedInfo.Version;
            var newVersion = gameManifest.Version;
            
            if (oldVersion != gameManifest.Version)
            {
                var updateInfo = new UpdateInfo
                {
                    Install_path = installedInfo.Path,
                    Title = gameTitle,
                };
                if (newVersion != null)
                {
                    updateInfo.Old_version = oldVersion;
                    updateInfo.Version = newVersion;
                    updateInfo.Download_size = await AmazonClientlessGames.CalculateGameSize(gameId, gameTitle, true);
                }
                else
                {
                    updateInfo.Status = UpdateStatus.Error;
                    logger.Error($"An error occured during checking {gameTitle} updates.");
                }

                gameToUpdate.Add(gameId, updateInfo);
            }
        }

        if (!gameToUpdate.ContainsKey(gameId))
        {
            gameToUpdate.Add(gameId, new UpdateInfo()
            {
                Status = UpdateStatus.NotAvailable,
                Title = gameTitle
            });
        }

        return gameToUpdate;
    }

    public async Task<Dictionary<string, UpdateInfo>> CheckAllGamesUpdates(bool forceRefreshCache = false)
    {
        var gamesToUpdate = new Dictionary<string, UpdateInfo>();
        var allInstalledGames = AmazonClientlessGames.GetAllInstalledGames();
        var gameIdsToCheck = allInstalledGames.Keys.ToList();
        var clientApi = new AmazonAccountClient();
        var newVersionIdsResponse = await clientApi.GetLiveVersionIds(gameIdsToCheck, forceRefreshCache);

        foreach (var (gameId, installedInfo) in allInstalledGames)
        {
            var gameSettings = AmazonClientlessGameSettingsViewModel.LoadGameSettings(gameId);
            newVersionIdsResponse.AdgProductIdToVersionIdMap.TryGetValue(gameId, out var newVersion);
            var gameTitle = installedInfo.Name;
            if (gameSettings.DisableGameVersionCheck != true)
            {
                var updateInfo = new UpdateInfo
                {
                    Install_path = installedInfo.Path,
                    Title = gameTitle,
                };
                var oldVersion = installedInfo.Version;
                if (oldVersion != newVersion)
                {
                    if (newVersion != null)
                    {
                        updateInfo.Version = newVersion;
                        updateInfo.Old_version = oldVersion;
                        updateInfo.Download_size = await AmazonClientlessGames.CalculateGameSize(gameId, gameTitle, true);
                    }
                    else
                    {
                        updateInfo.Status = UpdateStatus.Error;
                        logger.Error($"An error occured during checking {gameTitle} updates.");
                    }
                }
                else
                {
                    updateInfo.Status = UpdateStatus.NotAvailable;
                }

                gamesToUpdate.Add(gameId, updateInfo);
            }
        }

        var installedSdkManifestFile = Path.Combine(AmazonClientlessGames.AmazonGamesSdkInstallationPath, ".manifest_ac", "manifest.json");
        if (File.Exists(installedSdkManifestFile))
        {
            var content = await File.ReadAllTextAsync(installedSdkManifestFile);
            if (!content.IsNullOrEmpty() && Serialization.TryFromJson(content, out FullGameManifest? sdkManifest))
            {
                var oldVersion = sdkManifest?.Version;

                if (oldVersion != null)
                {
                    var sdkTitle = "Amazon Games SDK";
                    var updateInfo = new UpdateInfo
                    {
                        Install_path = AmazonClientlessGames.AmazonGamesSdkInstallationPath,
                        Title = sdkTitle,
                    };
                    var newSdkManifest =
                        await clientApi.GetGameManifest(AmazonClientlessGames.AmazonGamesSdkId, sdkTitle, forceRefreshCache);
                    var newVersion = newSdkManifest.Version;
                    if (oldVersion != newVersion)
                    {
                        if (newVersion != null)
                        {
                            updateInfo.Version = newVersion;
                            updateInfo.Download_size =
                                await AmazonClientlessGames.CalculateGameSize(AmazonClientlessGames.AmazonGamesSdkId, sdkTitle);
                        }
                        else
                        {
                            updateInfo.Status = UpdateStatus.Error;
                            logger.Error($"An error occured during checking {sdkTitle} updates.");
                        }
                    }
                    else
                    {
                        updateInfo.Status = UpdateStatus.NotAvailable;
                    }

                    gamesToUpdate.Add(AmazonClientlessGames.AmazonGamesSdkId, updateInfo);
                }
            }
        }

        return gamesToUpdate;
    }

    public async Task UpdateGame(
        Dictionary<string, UpdateInfo> gamesToUpdate, string gameTitle = "", bool silently = false,
        DownloadProperties? downloadProperties = null)
    {
        var updateTasks = new List<DownloadManagerData.Download>();
        if (gamesToUpdate.Count > 0)
        {
            if (silently)
            {
                playniteApi.Notifications.Add(new NotificationMessage("AmazonClientlessGamesUpdates",
                    LocalizationManager.Instance.GetString(LOC.CommonGamesUpdatesUnderway), NotificationSeverity.Info));
            }

            foreach (var gameToUpdate in gamesToUpdate)
            {
                var settings = AmazonClientlessPlugin.GetSettings();
                var newDownloadProperties = new DownloadProperties()
                {
                    DownloadAction = DownloadAction.Update,
                    MaxWorkers = settings.MaxWorkers,
                };
                if (downloadProperties != null)
                {
                    newDownloadProperties = downloadProperties.GetClone();
                }

                var updateTask = new DownloadManagerData.Download
                {
                    GameId = gameToUpdate.Key,
                    Name = gameToUpdate.Value.Title,
                    DownloadSizeNumber = gameToUpdate.Value.Download_size,
                    DownloadProperties = newDownloadProperties,
                };
                if (gameToUpdate.Value.Install_path.IsNullOrEmpty())
                {
                    logger.Warn($"No install path for {gameToUpdate.Value.Title}, skipping...");
                    continue;
                }

                updateTask.DownloadProperties.InstallPath = Directory.GetParent(gameToUpdate.Value.Install_path)?.FullName!;
                updateTask.FullInstallPath = gameToUpdate.Value.Install_path;
                updateTasks.Add(updateTask);
            }

            if (updateTasks.Count > 0)
            {
                var downloadLogic = AmazonClientlessPlugin.Instance.UnifiedDownloadLogic;
                await downloadLogic.AddTasks(updateTasks, silently);
            }
        }
        else if (!silently)
        {
            await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), gameTitle);
        }
    }
}