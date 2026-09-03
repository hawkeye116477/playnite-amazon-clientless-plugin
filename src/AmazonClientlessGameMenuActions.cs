using Playnite;

namespace AmazonClientless;

public class AmazonClientlessGameMenuActions(IReadOnlyList<Game> games)
{
    private static readonly ILogger Logger = LogManager.GetLogger();
    private IPlayniteApi PlayniteApi { get; set; } = AmazonClientlessPlugin.PlayniteApi;
    private Game Game { get; set; } = games.First();
    private IReadOnlyList<Game> Games { get; set; } = games;

    public async Task OpenCheckForGamesUpdatesWindow()
    {
    }

    public async Task OpenMoveGameWindow()
    {
    }
}