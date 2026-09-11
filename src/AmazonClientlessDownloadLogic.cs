using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using AmazonClientless.Models;
using AmazonClientless.Services;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using Playnite;
using PlayniteMod;
using PlayniteMod.Commands;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;
using HashAlgorithm = Sds.HashAlgorithm;

namespace AmazonClientless;

public class AmazonClientlessDownloadLogic : IUnifiedDownloadLogic
{
    private static readonly IPlayniteApi PlayniteApi = AmazonClientlessPlugin.PlayniteApi;
    private static readonly CommonHelpers CommonHelpers = AmazonClientlessPlugin.Instance.CommonHelpers;

    private static readonly IUnifiedDownloadManagerApi
        UnifiedDownloadManagerApi = AmazonClientlessPlugin.Instance.UnifiedDownloadManagerApi;

    private long ResumeInitialDiskBytes { get; set; }
    private static readonly ILogger Logger = LogManager.GetLogger<AmazonClientlessDownloadLogic>();
    private static readonly RetryHandler RetryHandler = new(new HttpClientHandler());
    private static readonly HttpClient Client = new(RetryHandler);
    private IProgress<ProgressData>? Progress { get; set; }
    private CancellationTokenSource? SpeedReporterCts { get; set; }
    private Stopwatch? SpeedStopwatch { get; set; }
    private long TotalSize { get; set; }
    private long totalDiskBytes;
    private string? BaseUrl { get; set; }
    public static int MaxMaxWorkers = 40;
    public static int DefaultMaxWorkers = 20;

    public async Task StartDownload(UnifiedDownload downloadTask)
    {
        var matchingPluginTask =
            AmazonClientlessPlugin.Instance.PluginDownloadData.Downloads.FirstOrDefault(t => t.GameId == downloadTask.GameId);
        var userCancelCts = downloadTask.GracefulCts;
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            downloadTask.ForcefulCts!.Token,
            userCancelCts!.Token
        );
        var sw = Stopwatch.StartNew();
        downloadTask.Activity = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel)}";
        downloadTask.Status = UnifiedDownloadStatus.Running;
        var tempReporterCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
        using var tempReporter = Task.Run(async () =>
        {
            while (!tempReporterCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(500, tempReporterCts.Token);
                    if (!tempReporterCts.Token.IsCancellationRequested)
                    {
                        _ = Application.Current.Dispatcher?.BeginInvoke((Action)(() => { downloadTask.Elapsed = sw.Elapsed; }));
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, tempReporterCts.Token);
        var downloadProperties = matchingPluginTask!.DownloadProperties;
        await tempReporterCts.CancelAsync();
        try
        {
            await tempReporter;
        }
        catch (OperationCanceledException)
        {
        }

        downloadTask.Activity = "";


        // Stop continuing if no links or files
        bool stopContinue = false;
        var clientApi = new AmazonAccountClient(AmazonClientlessPlugin.PlayniteApi);

        var manifest = await clientApi.GetGameManifest(downloadTask.GameId, downloadTask.Name);
        var originalManifestJson = Serialization.ToJson(manifest);

        var gameDownloadManifest = await clientApi.GetGameDownload(downloadTask.GameId, downloadTask.Name);
        BaseUrl = gameDownloadManifest.DownloadUrl;
        if (BaseUrl == null || manifest.AllFiles.Count == 0)
        {
            stopContinue = true;
        }

        if (stopContinue)
        {
            Logger.Error("No files to download.");
            downloadTask.Status = UnifiedDownloadStatus.Error;
            return;
        }

        var repairSkipPath = Path.Combine(matchingPluginTask.FullInstallPath, ".ACS_Temp");
        Directory.CreateDirectory(repairSkipPath);
        string repairSkipFile = Path.Combine(repairSkipPath, "repair-skip");

        // Verify and repair files
        if (Directory.Exists(matchingPluginTask.FullInstallPath) &&
            matchingPluginTask.DownloadProperties.DownloadAction != DownloadAction.Install && !File.Exists(repairSkipFile))
        {
            var invalidGameFiles = new ConcurrentBag<FullGameManifest.GameFile>();
            var reporterCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
            var allFiles = Directory.EnumerateFiles(matchingPluginTask.FullInstallPath, "*.*", SearchOption.AllDirectories).ToList();
            int countFiles = allFiles.Count;
            Logger.Debug(countFiles);
            if (countFiles > 0)
            {
                var itemsMap = manifest.AllFiles.Where(i => !string.IsNullOrEmpty(i.Path) && i.Size is > 0)
                                       .ToDictionary(i => new Uri(Path.Combine(downloadTask.FullInstallPath, i.Path!)).LocalPath, i => i);
                downloadTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonVerifying);
                long verifiedFiles = 0;
                long totalBytesRead = 0;
                if (manifest.AllFiles.Count > 0)
                {
                    var swDelta = new Stopwatch();
                    _ = Task.Run(async () =>
                    {
                        long lastUiUpdate = 0;
                        long previousBytes = 0;
                        swDelta = Stopwatch.StartNew();

                        try
                        {
                            while (!reporterCts.Token.IsCancellationRequested)
                            {
                                await Task.Delay(500, reporterCts.Token);

                                long currentBytes = Interlocked.Read(ref totalBytesRead);
                                long deltaBytes = currentBytes - previousBytes;
                                previousBytes = currentBytes;

                                double elapsedSec = swDelta.Elapsed.TotalSeconds;
                                swDelta.Restart();

                                long currentVerified = Interlocked.Read(ref verifiedFiles);
                                long now = Stopwatch.GetTimestamp();

                                if (now - lastUiUpdate >= TimeSpan.FromMilliseconds(500).Ticks)
                                {
                                    lastUiUpdate = now;
                                    _ = Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                                    {
                                        downloadTask.Activity =
                                            $"{LocalizationManager.Instance.GetString(LOC.CommonVerifying)} ({verifiedFiles}/{countFiles})";
                                        downloadTask.Elapsed = sw.Elapsed;
                                        downloadTask.DiskWriteSpeedBytes = deltaBytes / elapsedSec;
                                        double filesPerSecond = currentVerified / sw.Elapsed.TotalSeconds;
                                        double remainingFiles = countFiles - currentVerified;
                                        downloadTask.Eta = TimeSpan.FromSeconds(remainingFiles / filesPerSecond);
                                    }));
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }, reporterCts.Token);

                    try
                    {
                        ParallelOptions parallelOptions = new()
                        {
                            MaxDegreeOfParallelism = Math.Min(CommonHelpers.CpuThreadsNumber, 4),
                            CancellationToken = downloadTask.GracefulCts!.Token
                        };
                        await Task.Run(() =>
                        {
                            Parallel.ForEach(allFiles, parallelOptions, (file, _) =>
                            {
                                var perFileProgress = new Progress<int>(bytes => { Interlocked.Add(ref totalBytesRead, bytes); });
                                if (itemsMap.TryGetValue(file, out var searchedItem))
                                {
                                    string correctChecksum = "";
                                    var checksumType = HashAlgorithm.Sha256;
                                    if (searchedItem.Hash != null)
                                    {
                                        if (searchedItem.Hash.Algorithm != null)
                                        {
                                            checksumType = (HashAlgorithm)searchedItem.Hash.Algorithm;
                                            if (checksumType != HashAlgorithm.Sha256)
                                            {
                                                Logger.Debug(
                                                    $"This is {checksumType} checksum algorithm, which isn't yet supported. Please report that.");
                                            }
                                        }

                                        if (searchedItem.Hash.Value != null)
                                        {
                                            correctChecksum = searchedItem.Hash.Value;
                                        }

                                        if (!string.IsNullOrEmpty(correctChecksum))
                                        {
                                            try
                                            {
                                                string? calculatedChecksum = checksumType switch
                                                {
                                                    HashAlgorithm.Shake128 => null,
                                                    HashAlgorithm.Sha256 => Helpers.GetSHA256(file, perFileProgress, linkedCts.Token),
                                                    _ => null
                                                };

                                                if (calculatedChecksum != null &&
                                                    !string.Equals(calculatedChecksum, correctChecksum, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    try
                                                    {
                                                        File.Delete(file);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Logger.Debug(ex);
                                                    }
                                                }
                                                else
                                                {
                                                    invalidGameFiles.Add(searchedItem);
                                                }
                                            }
                                            catch (Exception hashEx)
                                            {
                                                Logger.Warn(hashEx, "");
                                            }
                                        }
                                    }
                                }

                                Interlocked.Increment(ref verifiedFiles);
                            });
                        }, linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    finally
                    {
                        await reporterCts.CancelAsync();
                        swDelta?.Stop();
                        foreach (var invalidGameFile in invalidGameFiles)
                        {
                            manifest.AllFiles.Remove(invalidGameFile);
                        }

                        Interlocked.Exchange(ref verifiedFiles, countFiles);
                        Application.Current.Dispatcher?.Invoke(() =>
                        {
                            downloadTask.Activity =
                                $"{LocalizationManager.Instance.GetString(LOC.CommonVerifying)} ({verifiedFiles}/{countFiles})";
                            downloadTask.Elapsed = sw.Elapsed;
                            if (verifiedFiles == countFiles)
                            {
                                downloadTask.Progress = 100.0;
                            }

                            using (File.Create(repairSkipFile))
                            {
                            }
                        });
                    }
                }
            }
        }

        downloadTask.Activity = "";
        Progress = new Progress<ProgressData>(p =>
        {
            downloadTask.DownloadSizeBytes = p.TotalBytes;
            downloadTask.InstallSizeBytes = p.TotalBytes;
            downloadTask.DownloadSpeedBytes = p.DownloadSpeed;
            downloadTask.DiskWriteSpeedBytes = p.DiskSpeed;
            downloadTask.Eta = TimeSpan.FromSeconds(p.Eta);
            downloadTask.Elapsed = sw.Elapsed;
            var currentPercentProgress = p.TotalBytes > 0 ? (double)p.DiskBytes / p.TotalBytes * 100 : 0;
            downloadTask.Progress = currentPercentProgress;
            if (p.TotalBytes == p.NetworkBytes)
            {
                switch (downloadProperties.DownloadAction)
                {
                    case DownloadAction.Install:
                        downloadTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation);
                        break;
                    case DownloadAction.Update:
                        downloadTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingUpdate);
                        break;
                    case DownloadAction.Repair:
                        downloadTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingRepair);
                        break;
                }
            }

            downloadTask.DownloadedBytes = p.NetworkBytes;
        });
        try
        {
            var maxWorkers = downloadProperties.MaxWorkers;
            if (downloadProperties.MaxWorkers == 0)
            {
                maxWorkers = DefaultMaxWorkers;
            }

            if (downloadProperties.DownloadAction != DownloadAction.Update)
            {
                downloadTask.Activity = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDownloadingLabel);
            }
            else
            {
                downloadTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonDownloadingUpdate);
            }

            Logger.Debug(
                $"Downloading {downloadTask.Name} ({downloadTask.GameId}) to {matchingPluginTask.DownloadProperties.InstallPath} ...");
            SpeedReporterCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
            if (manifest.AllFiles.Count > 0)
            {
                await DownloadGame(manifest, downloadTask.FullInstallPath, maxWorkers, linkedCts.Token);
            }
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                Logger.Error(ex, $"An error occured during downloading {downloadTask.Name} game");
                downloadTask.Status = UnifiedDownloadStatus.Error;
                downloadTask.Activity = "";
            }
        }
        finally
        {
            SpeedStopwatch?.Stop();
            if (SpeedReporterCts != null)
            {
                await SpeedReporterCts.CancelAsync();
            }

            SpeedReporterCts?.Dispose();
            sw.Stop();

            var finalDiskBytes = Interlocked.Read(ref totalDiskBytes);
            downloadTask.DownloadSizeBytes = TotalSize;
            downloadTask.InstallSizeBytes = TotalSize;
            downloadTask.DownloadSpeedBytes = 0;
            downloadTask.DiskWriteSpeedBytes = 0;
            downloadTask.Eta = TimeSpan.FromSeconds(0);
            downloadTask.Elapsed = sw.Elapsed;
            downloadTask.DownloadedBytes = finalDiskBytes;
            double currentPercentProgress = 0.0;
            if (TotalSize > 0)
            {
                currentPercentProgress = (double)finalDiskBytes / TotalSize * 100;
            }
            else if (downloadTask.Status != UnifiedDownloadStatus.Error)
            {
                currentPercentProgress = 100;
            }

            downloadTask.Progress = currentPercentProgress;
            downloadTask.Activity = "";

            if (downloadTask.Progress >= 100)
            {
                if (Directory.Exists(repairSkipPath))
                {
                    Directory.Delete(repairSkipPath, true);
                }

                var installedManifestPath = Path.Combine(downloadTask.FullInstallPath, ".manifest_ac");
                var installedManifestFile = Path.Combine(installedManifestPath, "manifest.json");
                if (File.Exists(installedManifestFile))
                {
                    File.Delete(installedManifestFile);
                }

                var installedManifestDirectoryInfo = Directory.CreateDirectory(installedManifestPath);
                installedManifestDirectoryInfo.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

                await File.WriteAllTextAsync(installedManifestFile, originalManifestJson, linkedCts.Token);

                if (downloadTask.GameId != AmazonClientlessGames.AmazonGamesSdkId)
                {
                    var allRealFiles = Directory.GetFileSystemEntries(downloadTask.FullInstallPath, "*", SearchOption.AllDirectories);
                    double realSize = 0;
                    foreach (var file in allRealFiles)
                    {
                        if (File.Exists(file))
                        {
                            var fileInfo = new FileInfo(file);
                            realSize += fileInfo.Length;
                        }
                    }

                    var installedAppList = AmazonClientlessPlugin.Instance.InstalledAppList;
                    var installedGameInfo = new InstalledGamesWrapper.Installed
                    {
                        Version = manifest.Version ?? "0",
                        Path = downloadTask.FullInstallPath,
                        Name = downloadTask.Name,
                        ID = downloadTask.GameId,
                        Size = realSize
                    };
                    installedAppList.Remove(downloadTask.GameId);

                    var game = new Game();
                    var existingGame = AmazonClientlessPlugin.PlayniteApi.Library.Games.FirstOrDefault(item =>
                        item.LibraryId == AmazonClientlessPlugin.Id && item.LibraryGameId == downloadTask.GameId);
                    if (existingGame != null)
                    {
                        game = existingGame;
                    }

                    game.InstallDirectory = installedGameInfo.Path;
                    game.InstallSize = (ulong)installedGameInfo.Size;
                    game.InstallState = InstallState.Installed;
                    await AmazonClientlessPlugin.PlayniteApi.Library.Games.UpdateAsync(game);
                    installedAppList.Add(downloadTask.GameId, installedGameInfo);
                    AmazonClientlessPlugin.Instance.InstalledAppListModified = true;
                }

                DateTimeOffset now = DateTime.UtcNow;
                downloadTask.Status = UnifiedDownloadStatus.Completed;
                downloadTask.CompletedTime = now.ToUnixTimeSeconds();
                linkedCts.Dispose();
            }
        }
    }

    private async Task DownloadGame(
        FullGameManifest manifest,
        string fullInstallPath,
        int maxParallel,
        CancellationToken token)
    {
        int bufferSize = 512 * 1024;
        Client.DefaultRequestHeaders.Clear();
        Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AmazonAccountClient.LauncherUserAgent);
        TotalSize = 0;
        long initialDiskBytesLocal = 0;

        var allFiles = new List<FullGameManifest.GameFile>();
        foreach (var file in manifest.AllFiles)
        {
            if (file.Path != null)
            {
                var filePath = Path.Combine(fullInstallPath, file.Path.TrimStart('/', '\\'));
                if (File.Exists(filePath))
                {
                    var fileSize = new FileInfo(filePath).Length;
                    initialDiskBytesLocal += fileSize;
                    if (fileSize != file.Size)
                    {
                        allFiles.Add(file);
                    }
                }
                else
                {
                    allFiles.Add(file);
                }

                if (file.Size != null)
                {
                    TotalSize += (long)file.Size;
                }
            }
        }

        ResumeInitialDiskBytes = initialDiskBytesLocal;
        totalDiskBytes = initialDiskBytesLocal;
        ReportProgress();

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = maxParallel,
            CancellationToken = token
        };
        await Parallel.ForEachAsync(allFiles, parallelOptions, async (file, ct) =>
        {
            if (!ct.IsCancellationRequested)
            {
                if (file.Path != null)
                {
                    var filePath = Path.Combine(fullInstallPath, file.Path.TrimStart('/', '\\'));
                    var targetDirectory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        if (targetDirectory != null)
                        {
                            Directory.CreateDirectory(targetDirectory);
                        }
                    }

                    if (file.Size == 0)
                    {
                        if (!File.Exists(filePath) || new FileInfo(filePath).Length != 0)
                        {
                            await File.WriteAllBytesAsync(filePath, [], ct).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        if (file.Hash != null && BaseUrl != null)
                        {
                            var gameUri = new Uri(BaseUrl);
                            var gameUriBuilder = new UriBuilder(gameUri)
                            {
                                Path = $"{gameUri.LocalPath}/files/{file.Hash.Value}",
                                Query = gameUri.Query,
                                Host = gameUri.Host
                            };
                            var finalGameUrl = gameUriBuilder.Uri.ToString();
                            using var request = new HttpRequestMessage(HttpMethod.Get, finalGameUrl);
                            long resumeStartByte = 0;
                            if (File.Exists(filePath))
                            {
                                resumeStartByte = new FileInfo(filePath).Length;
                                if (resumeStartByte < file.Size)
                                {
                                    request.Headers.Range = new RangeHeaderValue(resumeStartByte, file.Size - 1);
                                }
                            }

                            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                                                             .ConfigureAwait(false);
                            response.EnsureSuccessStatusCode();
                            await using var networkStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                            var fileMode = resumeStartByte > 0 && resumeStartByte < file.Size ? FileMode.Append : FileMode.Create;
                            await RentAndUsePool(bufferSize, async buffer =>
                                {
                                    await using var finalFileFs = new FileStream(filePath, fileMode, FileAccess.Write,
                                        FileShare.None, bufferSize,
                                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                                    int bytesRead;
                                    while ((bytesRead = await networkStream.ReadAsync(buffer, ct)
                                                                           .ConfigureAwait(false)) >
                                           0)
                                    {
                                        Interlocked.Add(ref totalDiskBytes, bytesRead);
                                        await finalFileFs.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                                    }

                                    if (file.Hidden == true)
                                    {
                                        File.SetAttributes(filePath, FileAttributes.Hidden);
                                    }
                                })
                               .ConfigureAwait(false);
                        }
                    }
                }
            }
        });
    }

    private async Task RentAndUsePool(int size, Func<byte[], Task> action)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            await action(buffer).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            catch
            {
                // ignored
            }
        }
    }

    private void ReportProgress()
    {
        _ = Task.Run(async () =>
        {
            TimeSpan lastStopwatchElapsed = TimeSpan.Zero;
            SpeedStopwatch = Stopwatch.StartNew();

            int maxSamples = 15;
            long lastDiskBytes = Interlocked.Read(ref totalDiskBytes);

            Queue<double> netSpeedSamples = new();

            try
            {
                while (SpeedReporterCts is { Token.IsCancellationRequested: false })
                {
                    await Task.Delay(900, SpeedReporterCts.Token);
                    var elapsed = SpeedStopwatch.Elapsed;
                    double dt = (elapsed - lastStopwatchElapsed).TotalSeconds;
                    if (dt <= 0)
                    {
                        continue;
                    }

                    var diskBytes = Interlocked.Read(ref totalDiskBytes);

                    double initialDisk = ResumeInitialDiskBytes;

                    double rawNetSpeed = Math.Max(0, (diskBytes - lastDiskBytes) / dt);

                    UpdateSmoothQueue(netSpeedSamples, rawNetSpeed, maxSamples);

                    double smoothNetSpeed = netSpeedSamples.Average();

                    if (rawNetSpeed <= 0)
                    {
                        smoothNetSpeed = 0;
                    }

                    double avgNetSpeed = (diskBytes - initialDisk) / elapsed.TotalSeconds;

                    double speedForEta = Math.Max(1, avgNetSpeed);
                    double remaining = TotalSize - diskBytes;

                    double eta = remaining / speedForEta;

                    lastStopwatchElapsed = elapsed;
                    lastDiskBytes = diskBytes;

                    Progress?.Report(new ProgressData
                    {
                        TotalBytes = TotalSize,
                        NetworkBytes = diskBytes,
                        DiskBytes = diskBytes,
                        Eta = eta,
                        DownloadSpeed = smoothNetSpeed,
                        DiskSpeed = smoothNetSpeed,
                    });
                }
            }
            catch
            {
                // ignored
            }
        }, SpeedReporterCts!.Token);
    }

    private void UpdateSmoothQueue(Queue<double> queue, double rawValue, int max)
    {
        queue.Enqueue(rawValue);
        if (queue.Count > max)
        {
            queue.Dequeue();
        }
    }

    public async Task OnCancelDownload(UnifiedDownload downloadTask)
    {
        downloadTask.DownloadedBytes = 0;
        downloadTask.Progress = 0;
        var gameId = downloadTask.GameId;

        const int maxRetries = 5;
        int delayMs = 500;
        var matchingPluginTask = AmazonClientlessPlugin.Instance.PluginDownloadData.Downloads.FirstOrDefault(t => t.GameId == gameId);
        await Task.Run(async () =>
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (matchingPluginTask is { DownloadProperties.DownloadAction: DownloadAction.Install })
                    {
                        if (Directory.Exists(downloadTask.FullInstallPath))
                        {
                            Directory.Delete(downloadTask.FullInstallPath, true);
                        }
                    }

                    break;
                }
                catch (Exception rex)
                {
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                    }
                    else
                    {
                        var itemToRemove = downloadTask.FullInstallPath;
                        Logger.Warn(rex, $"Can't remove {itemToRemove}. Please try removing manually.");
                        break;
                    }
                }
            }
        });
    }

    public async Task OnRemoveDownloadEntry(UnifiedDownload downloadTask)
    {
        var matchingPluginTask =
            AmazonClientlessPlugin.Instance.PluginDownloadData.Downloads.FirstOrDefault(t => t.GameId == downloadTask.GameId);
        if (matchingPluginTask != null)
        {
            AmazonClientlessPlugin.Instance.PluginDownloadData.Downloads.Remove(matchingPluginTask);
            SaveDownloadData();
        }
    }

    public void OpenDownloadPropertiesWindow(UnifiedDownload selectedEntry)
    {
        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        var matchingPluginTask =
            AmazonClientlessPlugin.Instance.PluginDownloadData.Downloads.FirstOrDefault(t =>
                t.GameId == selectedEntry.GameId);
        if (matchingPluginTask != null)
        {
            window.Title =
                $"{selectedEntry.Name} — {LocalizationManager.Instance.GetString(LOC.CommonDownloadProperties)}";
            window.DataContext = matchingPluginTask;
            window.Content = new AmazonClientlessDownloadProperties();
            window.Owner = PlayniteApi.GetLastActiveWindow();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }
    }

    public static async Task<bool> CheckIfUdmInstalled()
    {
        bool installed = PlayniteApi.Addons.Plugins.Any(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
        if (!installed)
        {
            var options = new List<MessageBoxResponse>
            {
                new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame)),
                new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel))
            };
            var result = await PlayniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled,
                    new Dictionary<string, IFluentType>
                        { ["launcherName"] = (FluentString)"Unified Download Manager" }),
                $"{AmazonClientlessPlugin.ShortPluginName} library integration", MessageBoxSeverity.Error, options, []);
            if (result == options[0])
            {
                Commands.OpenUrl(
                    "https://github.com/hawkeye116477/playnite-unifiedDownloadManager-plugin/releases");
            }
        }

        return installed;
    }

    public static DownloadManagerData LoadSavedDownloadData()
    {
        var downloadData = new DownloadManagerData();

        var dataDir = PlayniteApi.UserDataDir;
        var dataFile = Path.Combine(dataDir, "downloads.json");
        var correctJson = false;
        if (File.Exists(dataFile))
        {
            var content = FileSystem.ReadFileAsStringSafe(dataFile);
            if (!content.IsNullOrWhiteSpace() &&
                Serialization.TryFromJson(content, out DownloadManagerData? newPluginDownloadData))
            {
                if (newPluginDownloadData is { Downloads: not null })
                {
                    correctJson = true;
                    downloadData = newPluginDownloadData;
                }
            }
        }

        if (!correctJson)
        {
            downloadData = new DownloadManagerData
            {
                Downloads = []
            };
        }

        return downloadData;
    }

    public static void SaveDownloadData()
    {
        CommonHelpers.SaveJsonSettingsToFile(AmazonClientlessPlugin.Instance.PluginDownloadData, "", "downloads", true);
    }

    public async Task AddTasks(List<DownloadManagerData.Download> downloadTasks, bool silently = false)
    {
        var unifiedTasks = new List<UnifiedDownload>();
        var downloadItemsAlreadyAdded = new List<string>();
        var unifiedDownloadManagerApi = AmazonClientlessPlugin.Instance.UnifiedDownloadManagerApi;
        foreach (var downloadTask in downloadTasks)
        {
            var completedDownload = true;
            var wantedUnifiedItem =
                unifiedDownloadManagerApi.GetTask(downloadTask.GameId, AmazonClientlessPlugin.Id);
            if (wantedUnifiedItem != null)
            {
                if (wantedUnifiedItem.Status != UnifiedDownloadStatus.Completed)
                {
                    completedDownload = false;
                }
            }

            if (completedDownload)
            {
                var wantedPluginItem =
                    AmazonClientlessPlugin.Instance?.PluginDownloadData?.Downloads?.FirstOrDefault(item =>
                        item.GameId == downloadTask.GameId);
                if (wantedPluginItem != null)
                {
                    AmazonClientlessPlugin.Instance?.PluginDownloadData?.Downloads?.Remove(wantedPluginItem);
                }

                if (wantedUnifiedItem != null)
                {
                    unifiedDownloadManagerApi.RemoveTask(wantedUnifiedItem);
                    wantedUnifiedItem =
                        unifiedDownloadManagerApi.GetTask(downloadTask.GameId, AmazonClientlessPlugin.Id);
                }
            }

            if (wantedUnifiedItem != null)
            {
                downloadItemsAlreadyAdded.Add(wantedUnifiedItem.Name);
                continue;
            }

            AmazonClientlessPlugin.Instance?.PluginDownloadData?.Downloads?.Add(downloadTask);
            var unifiedTask = new UnifiedDownload
            {
                GameId = downloadTask.GameId,
                Name = downloadTask.Name,
                PluginId = AmazonClientlessPlugin.Id,
                SourceName = "Amazon Games",
                DownloadSizeBytes = downloadTask.DownloadSizeNumber,
                InstallSizeBytes = downloadTask.DownloadSizeNumber,
                FullInstallPath = downloadTask.FullInstallPath
            };
            unifiedTasks.Add(unifiedTask);
        }

        await unifiedDownloadManagerApi.AddTasks(unifiedTasks);
        SaveDownloadData();

        if (!silently && unifiedTasks.Count == 0)
        {
            if (downloadItemsAlreadyAdded.Count > 0)
            {
                var downloadItemsAlreadyAddedCombined = downloadItemsAlreadyAdded[0];
                if (downloadItemsAlreadyAdded.Count > 1)
                {
                    downloadItemsAlreadyAddedCombined = string.Join(", ",
                        downloadItemsAlreadyAdded.Select(item => item.ToString()));
                }

                await PlayniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonDownloadAlreadyExists,
                        new Dictionary<string, IFluentType>
                        {
                            ["appName"] = (FluentString)downloadItemsAlreadyAddedCombined,
                            ["count"] = (FluentNumber)downloadItemsAlreadyAdded.Count,
                            ["pluginShortName"] = (FluentString)"Unified Download Manager"
                        }), "", MessageBoxButtons.OK, MessageBoxSeverity.Error);
            }
        }
    }
}