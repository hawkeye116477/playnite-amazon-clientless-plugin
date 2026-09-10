namespace AmazonClientless.Models;

public class UpdateInfo
{
    public string Title { get; set; } = "";
    public string Old_version { get; set; } = "";
    public string Version { get; set; } = "";
    public double Download_size { get; set; } = 0;
    public string Install_path { get; set; } = "";
    public UpdateStatus Status { get; set; } = UpdateStatus.Available;
}

public enum UpdateStatus
{
    Available,
    NotAvailable,
    Error,
}