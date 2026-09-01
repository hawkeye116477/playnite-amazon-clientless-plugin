using System.Diagnostics;
using System.IO;
using AmazonClientless.Services;
using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite;
using Playnite.Common;

namespace AmazonClientless;

public class AmazonClientlessPlayController(Game game) : PlayController(game.LibraryGameId!,
    LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonStartUsingClient,
        new Dictionary<string, IFluentType> { ["var0"] = (FluentString)"Amazon Clientless" }))
{
    private CancellationTokenSource? watcherToken;
    private readonly IPlayniteApi playniteApi = AmazonClientlessPlugin.PlayniteApi;
    private AmazonClientlessLauncher launcher = new AmazonClientlessLauncher();
    private readonly ILogger logger = LogManager.GetLogger();

    public override async ValueTask DisposeAsync()
    {
        if (watcherToken != null)
        {
            await watcherToken.CancelAsync();
            watcherToken?.Dispose();
            watcherToken = null;
        }
    }

    private async Task BeforeGameStarting()
    {
        var gameSettings = AmazonClientlessGameSettingsViewModel.LoadGameSettings(game.LibraryGameId!);
        if (!gameSettings.IsFullyInstalled)
        {
            var installProgressOptions =
                new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation), false);
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(installProgressOptions,
                async a => { await AmazonClientlessLauncher.CompleteGameInstallation(game.LibraryGameId!, game.InstallDirectory!); });
        }
    }

    public override async Task PlayAsync(PlayActionArgs args)
    {
        if (Directory.Exists(game.InstallDirectory))
        {
            await BeforeGameStarting();
            await LaunchGame();
        }
        else
        {
            await GameStoppedAsync(null!);
        }
    }

    private async Task LaunchGame(bool noLauncher = true)
    {
        await DisposeAsync();
        var playArgs = new List<string>();
        var globalSettings = AmazonClientlessPlugin.GetSettings();
        var gameSettings = AmazonClientlessGameSettingsViewModel.LoadGameSettings(game.LibraryGameId!);

        var workingDirectory = Path.Combine(game.InstallDirectory!);
        var providedArgs = new List<string>();
        bool canLaunchOffline = false;
        var mainBinaryPath = "";
        if (noLauncher)
        {
            var gameConfig = AmazonClientlessLauncher.GetGameConfiguration(game.InstallDirectory!);
            if (gameConfig != null && !AmazonClientlessLauncher.GetGameRequiresClient(gameConfig))
            {
                canLaunchOffline = true;
                if (gameConfig.Main?.Command != null)
                {
                    mainBinaryPath = Path.Combine(game.InstallDirectory!, gameConfig.Main.Command);
                }

                if (gameConfig.Main != null && gameConfig.Main.Args.HasNonEmptyItems())
                {
                    providedArgs.AddRange(gameConfig.Main.Args);
                }

                if (gameConfig.Main != null && !gameConfig.Main.WorkingSubdirOverride.IsNullOrEmpty())
                {
                    workingDirectory = Path.Combine(game.InstallDirectory!, gameConfig.Main.WorkingSubdirOverride);
                }
            }
        }

        if (gameSettings.StartupArguments is { Count: > 0 })
        {
            foreach (var userArg in gameSettings.StartupArguments)
            {
                if (userArg.Equals("{Args}", StringComparison.OrdinalIgnoreCase))
                {
                    if (providedArgs.Count > 0)
                    {
                        playArgs.AddRange(providedArgs);
                    }
                }
                else if (userArg.Contains('{'))
                {
                    playArgs.Add(playniteApi.ExpandVariables(game, userArg, false));
                }
                else
                {
                    playArgs.Add(userArg);
                }
            }
        }
        else if (providedArgs.Count > 0)
        {
            playArgs.AddRange(providedArgs);
        }

        var shouldStop = false;
        if (File.Exists(mainBinaryPath))
        {
            var cmd = new ProcessStartInfo();
            cmd.ArgumentList.AddRangeIfNotNull(playArgs);
            cmd.UseShellExecute = true;
            cmd.FileName = mainBinaryPath;
            cmd.WorkingDirectory = workingDirectory;

            if (!canLaunchOffline)
            {
                var clientApi = new AmazonAccountClient(AmazonClientlessPlugin.PlayniteApi);
                var userLoggedIn = await clientApi.GetIsUserLoggedIn();
                if (!userLoggedIn)
                {
                    await playniteApi.Dialogs.ShowMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameStartError,
                            new Dictionary<string, IFluentType>
                                { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired) }
                        ));
                    await GameStoppedAsync(new GameStoppedArgs(0));
                    return;
                }
            }

            var mainProcess = ProcessStarter.StartProcess(cmd);
            if (mainProcess != null)
            {
                await GameStartedAsync(new GameStartedArgs(mainProcess.Id));
                var monitor = new MonitorProcessTree(mainProcess.Id);
                StartTracking(monitor.IsProcessTreeRunning);
            }
            else
            {
                shouldStop = true;
            }
        }
        else
        {
            shouldStop = true;
        }

        if (shouldStop)
        {
            await GameStoppedAsync(new GameStoppedArgs(0));
        }
    }

    private void StartTracking(
        Func<bool> trackingAction,
        Func<int>? startupCheck = null,
        int trackingFrequency = 2000,
        int trackingStartDelay = 0)
    {
        if (watcherToken != null)
        {
            throw new Exception("Game is already being tracked.");
        }

        watcherToken = new CancellationTokenSource();
        Task.Run(async () =>
        {
            ulong playTimeMs = 0;
            var trackingWatch = new Stopwatch();
            const int maxFailCount = 5;
            var failCount = 0;

            if (trackingStartDelay > 0)
            {
                await Task.Delay(trackingStartDelay, watcherToken.Token).ContinueWith(task => { });
            }

            if (startupCheck != null)
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (failCount >= maxFailCount)
                    {
                        await GameStoppedAsync(new GameStoppedArgs(0));
                        return;
                    }

                    try
                    {
                        var id = startupCheck();
                        if (id > 0)
                        {
                            await GameStartedAsync(new GameStartedArgs(id));
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        failCount++;
                        logger.Error(e, "Game startup tracking iteration failed.");
                    }

                    await Task.Delay(trackingFrequency, watcherToken.Token).ContinueWith(task => { });
                }
            }

            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                if (failCount >= maxFailCount)
                {
                    var playTimeS = playTimeMs / 1000;
                    await GameStoppedAsync(new GameStoppedArgs((uint)playTimeS));
                    return;
                }

                try
                {
                    trackingWatch.Restart();
                    if (!trackingAction())
                    {
                        var playTimeS = playTimeMs / 1000;
                        await GameStoppedAsync(new GameStoppedArgs((uint)playTimeS));
                        return;
                    }
                }
                catch (Exception e)
                {
                    failCount++;
                    logger.Error(e, "Game tracking iteration failed.");
                }

                await Task.Delay(trackingFrequency, watcherToken.Token).ContinueWith(task => { });
                trackingWatch.Stop();
                if (trackingWatch.ElapsedMilliseconds > trackingFrequency + 30_000)
                {
                    // This is for cases where system is put into sleep or hibernation.
                    // Realistically speaking, one tracking interation should never take 30+ seconds,
                    // but lets use that as safe value in case this runs super slowly on some weird PCs.
                    continue;
                }

                playTimeMs += (ulong)trackingWatch.ElapsedMilliseconds;
            }
        });
    }
}