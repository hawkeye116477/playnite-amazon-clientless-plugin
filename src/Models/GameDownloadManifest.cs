using System.Text.Json.Serialization;

namespace AmazonClientless.Models;

public class GameDownloadManifest
{
    [field: JsonIgnore]
    public string? DownloadUrl { get; set; }
    public string? VersionId { get; set; }
}