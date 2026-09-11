using System.IO;
using AmazonClientless.Services;
using Playnite;

namespace AmazonClientless;

public class AmazonClientlessMetadataProviderProviderGameSession(Game game) : MetadataProviderGameSession(game)
{
    private ImportableFile? GetIconImage()
    {
        // Load icon from exe
        if (Game.InstallState == InstallState.Installed)
        {
            var installedAppList = AmazonClientlessGames.GetAllInstalledGames();
            if (Game.LibraryGameId != null && installedAppList.TryGetValue(Game.LibraryGameId, out var value))
            {
                var gameConfig = AmazonClientlessGames.GetGameConfiguration(game.InstallDirectory!);
                var exePath = Path.Combine(value.Path, gameConfig.Main.Command);
                if (File.Exists(exePath))
                {
                    return new ImportableFile(BuiltInGameDataId.DesktopIcon, exePath);
                }
            }
        }
        return null;
    }
    
    public override async Task<object?> GetDataAsync(GetDataArgs dataArgs)
    {
        var clientApi = new AmazonAccountClient(AmazonClientlessPlugin.PlayniteApi);
        var entitlement = await clientApi.GetEntitlement(Game.LibraryGameId);
        var details = entitlement?.Product.ProductDetail;
        
        return dataArgs.DataId switch
        {
            BuiltInGameDataId.Links => new List<ImportableWebLink>
                { new("pcgamingwiki", "PCGamingWiki", $"http://pcgamingwiki.com/w/index.php?search={Uri.EscapeDataString(Game.Name)}") },
            BuiltInGameDataId.DesktopIcon => GetIconImage(),
            BuiltInGameDataId.ReleaseDate => details?.Details.ReleaseDate,
            BuiltInGameDataId.Genres => details?.Details.Genres,
            _ => null
        };
    }
}

public class AmazonClientlessMetadataProvider : MetadataProvider
{
    public override async Task<MetadataProviderGameSession?> CreateGameSessionAsync(CreateGameMetadataSessionArgs args)
    {
        return args.Game.LibraryId != AmazonClientlessPlugin.Id ?
            null :
            new AmazonClientlessMetadataProviderProviderGameSession(args.Game);
    }
}