using System.IO;
using CommonPlugin.Enums;
using Playnite;

namespace AmazonClientless;

public class AmazonClientlessCache
{
    private static readonly ILogger Logger = LogManager.GetLogger<AmazonClientlessCache>();
    public static long GetNextClearingTime(ClearCacheTime frequency)
    {
        DateTimeOffset? clearingTime = null;
        DateTimeOffset now = DateTime.UtcNow;
        clearingTime = frequency switch
        {
            ClearCacheTime.Day => now.AddDays(1),
            ClearCacheTime.Week => now.AddDays(7),
            ClearCacheTime.Month => now.AddMonths(1),
            ClearCacheTime.ThreeMonths => now.AddMonths(3),
            ClearCacheTime.SixMonths => now.AddMonths(6),
            _ => clearingTime
        };

        return clearingTime?.ToUnixTimeSeconds() ?? 0;
    }
    
    public static string GetCachePath(string dirName)
    {
        return Path.Combine(AmazonClientlessPlugin.PlayniteApi.UserDataDir, "cache", dirName);
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

    public static void ClearSpecificGamesCache(List<string> gameIds)
    {
        var cacheDirs = new List<string>
        {
            GetCachePath("manifest"),
            GetCachePath("update"),
        };

        foreach (var cacheDir in cacheDirs)
        {
            if (Directory.Exists(cacheDir))
            {
                foreach (var file in Directory.EnumerateFiles(cacheDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (gameIds.Any(gameId => file.Contains(gameId)))
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                       Logger.Error(ex, $"An error occured during removing {file} file");
                    }
                }
            }
        }
    }
}