using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommonPlugin.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AmazonClientless.Models;

public partial class DownloadManagerData : ObservableObject
{
    public ObservableCollection<Download> Downloads { get; set; } = [];

    public partial class Download : ObservableObject
    {
        public string GameId { get; set; } = "";
        public string Name { get; set; } = "";
        public string FullInstallPath { get; set; } = "";

        [ObservableProperty]
        [field: JsonIgnore]
        public partial double DownloadSizeNumber { get; set; }

        public DownloadProperties DownloadProperties { get; set; } = new();
    }
}

public class DownloadProperties : ObservableObject
{
    public string InstallPath { get; set; } = "";
    public DownloadAction DownloadAction { get; set; }
    public int MaxWorkers { get; set; }
}