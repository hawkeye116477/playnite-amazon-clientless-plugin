using Sds;

namespace AmazonClientless.Models;

public class FullGameManifest
{
    public List<GameFile> AllFiles { get; set; } = [];
    public ManifestHeader ManifestHeader { get; set; } = new();
    public bool ErrorDisplayed { get; set; } = false;

    public class GameFile
    {
        public string? Path { get; set; }
        public uint? Mode { get; set; }
        public long? Size { get; set; }
        public bool? Hidden { get; set; } = false;
        public GameFileHash? Hash { get; set; }
    }

    public class GameFileHash
    {
        public HashAlgorithm? Algorithm { get; set; }
        public string? Value { get; set; }
    }
}