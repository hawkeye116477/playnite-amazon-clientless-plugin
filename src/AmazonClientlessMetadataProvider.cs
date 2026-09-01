using Playnite;

namespace AmazonClientless;

public class AmazonClientlessMetadataProviderProviderGameSession(Game game) : MetadataProviderGameSession(game)
{
    public override async Task<object?> GetDataAsync(GetDataArgs dataArgs)
    {
        return dataArgs.DataId switch
        {
            BuiltInGameDataId.Links => new List<ImportableWebLink> { new("pcgamingwiki", "PCGamingWiki", $"http://pcgamingwiki.com/w/index.php?search={Uri.EscapeDataString(Game.Name)}") },
            _ => null
        };
    }
}

public class AmazonClientlessMetadataProvider : MetadataProvider
{
    public override async Task<MetadataProviderGameSession?> CreateGameSessionAsync(CreateGameMetadataSessionArgs args)
    {
        return args.Game.LibraryId != AmazonClientlessPlugin.Id ? null :
            new AmazonClientlessMetadataProviderProviderGameSession(args.Game);
    }
}