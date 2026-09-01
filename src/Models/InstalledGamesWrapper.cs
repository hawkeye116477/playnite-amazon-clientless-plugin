namespace AmazonClientless.Models;

public class InstalledGamesWrapper
{
    public List<Installed> InstalledGames { get; set; } = [];

    public class Installed
    {
        public string ID { get; set; } = "";
        public string Version { get; set; } = "";
        public string Path { get; set; } = "";
        public double Size { get; set; }
        public string Name { get; set; } = "";
    }
}