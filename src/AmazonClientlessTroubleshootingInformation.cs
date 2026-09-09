using System.Diagnostics;
using System.Reflection;

namespace AmazonClientless;

public static class AmazonClientlessTroubleshootingInformation
{
    public static string PlayniteVersion => AmazonClientlessPlugin.PlayniteApi.AppInfo.ApplicationVersion.ToString();

    public static string? PluginVersion
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.ProductVersion;
        }
    }

    public static string GamesInstallationPath => AmazonClientlessGames.InstallationPath;
}