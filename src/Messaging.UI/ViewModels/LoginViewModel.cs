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
    private string _ipAddress = "::1";

    [ObservableProperty]
    private string _port = "8080";

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _confirmPassword = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isConnecting = false;

    [ObservableProperty]
    private bool _isRegisterMode = false;

    // Computed UI labels
    public string ActionButtonText => IsRegisterMode ? "Register" : "Connect";
    public string ToggleModeText => IsRegisterMode ? "Already have an account? Login" : "Don't have an account? Register";
    public string HeaderText => IsRegisterMode ? "Create Account" : "Welcome Back";

    partial void OnIsRegisterModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(ToggleModeText));
        OnPropertyChanged(nameof(HeaderText));
        ErrorMessage = "";
        ConfirmPassword = "";
    }

    private readonly Messaging.UI.Services.AppSession? _appSession;

    public LoginViewModel(Messaging.UI.Services.AppSession appSession)
    {
        _appSession = appSession;
    }

    // Required for design-time instantiation or fallback
    public LoginViewModel() {}

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegisterMode = !IsRegisterMode;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(IpAddress) || string.IsNullOrWhiteSpace(Port))
        {
            ErrorMessage = "Please fill in all fields.";
            return;
        }

        if (IsRegisterMode)
        {
            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Please confirm your password.";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }

            if (Password.Length < 4)
            {
                ErrorMessage = "Password must be at least 4 characters.";
                return;
            }
        }

        if (!int.TryParse(Port, out int portNum))
        {
            ErrorMessage = "Port must be a number.";
            return;
        }

        IsConnecting = true;
        ErrorMessage = "";

        // TODO: Pass Password and IsRegisterMode to ConnectAsync when backend auth is implemented
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
