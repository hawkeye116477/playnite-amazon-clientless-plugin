namespace AmazonClientless;

public static class AmazonClientlessTroubleshootingInformation
{
    public static string PlayniteVersion => AmazonClientlessPlugin.PlayniteApi.AppInfo.ApplicationVersion.ToString();

    public static string? PluginVersion
    {
        get
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.ProductVersion;
        }
    }

    public static string GamesInstallationPath => AmazonClientlessLauncher.GamesInstallationPath;
}