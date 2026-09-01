using CommunityToolkit.Mvvm.ComponentModel;

namespace AmazonClientless.Models;

public partial class GameSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool? DisableGameVersionCheck { get; set; }

    [ObservableProperty]
    public partial List<string>? StartupArguments { get; set; }
    
    [ObservableProperty]
    public partial bool IsFullyInstalled { get; set; } = false;
}