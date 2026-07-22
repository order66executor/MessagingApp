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

    private readonly Messaging.UI.Services.AppSession? _appSession;

    public LoginViewModel(Messaging.UI.Services.AppSession appSession)
    {
        _appSession = appSession;
    }

    // Required for design-time instantiation or fallback
    public LoginViewModel() {}

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(IpAddress) || string.IsNullOrWhiteSpace(Port))
        {
            ErrorMessage = "Please fill in all fields.";
            return;
        }

        if (!int.TryParse(Port, out int portNum))
        {
            ErrorMessage = "Port must be a number.";
            return;
        }

        IsConnecting = true;
        ErrorMessage = "";

        bool success = _appSession != null && await _appSession.ConnectAsync(IpAddress, portNum, Username);

        IsConnecting = false;
        
        if (!success)
        {
            ErrorMessage = "Failed to connect to the server.";
            return;
        }

        // Navigate to MainChat
        var chatViewModel = App.Services?.GetService<MainChatViewModel>() ?? new MainChatViewModel();
        WeakReferenceMessenger.Default.Send(new NavigationMessage(chatViewModel));
    }
}
