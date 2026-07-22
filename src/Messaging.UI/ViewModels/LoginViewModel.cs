using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Messaging.UI.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _ipAddress = "127.0.0.1";

    [ObservableProperty]
    private string _port = "8080";

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isConnecting = false;

    public LoginViewModel() {}

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(IpAddress) || string.IsNullOrWhiteSpace(Port))
        {
            ErrorMessage = "Please fill in all fields.";
            return;
        }

        IsConnecting = true;
        ErrorMessage = "";

        // TODO: Actual connection logic using MessageClient
        // For now, simulate a delay and navigate
        await Task.Delay(1000);

        IsConnecting = false;
        
        // Navigate to MainChat
        var chatViewModel = App.Services?.GetService<MainChatViewModel>() ?? new MainChatViewModel();
        WeakReferenceMessenger.Default.Send(new NavigationMessage(chatViewModel));
    }
}
