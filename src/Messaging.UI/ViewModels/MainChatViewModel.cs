using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.UI.ViewModels;

public partial class MainChatViewModel : ViewModelBase
{
    public SidebarViewModel Sidebar { get; }
    public ChatAreaViewModel ChatArea { get; }

    public MainChatViewModel()
    {
        Sidebar = App.Services?.GetService<SidebarViewModel>() ?? new SidebarViewModel();
        ChatArea = App.Services?.GetService<ChatAreaViewModel>() ?? new ChatAreaViewModel();
    }
}
