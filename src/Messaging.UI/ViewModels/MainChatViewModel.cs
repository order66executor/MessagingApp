using CommunityToolkit.Mvvm.ComponentModel;

namespace Messaging.UI.ViewModels;

public partial class MainChatViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusText = "Connected.";
    
    // We will add SidebarViewModel and ChatAreaViewModel here later
}
