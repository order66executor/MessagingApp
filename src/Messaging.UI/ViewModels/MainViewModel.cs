using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Messaging.UI.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.UI.ViewModels;

public partial class MainViewModel : ViewModelBase, IRecipient<NavigationMessage>
{
    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    public MainViewModel()
    {
        WeakReferenceMessenger.Default.Register(this);

        // Instead of resolving from DI which might cause a loop if LoginViewModel takes MainViewModel,
        // we can just resolve it safely now because LoginViewModel won't depend on MainViewModel anymore.
        _currentViewModel = App.Services?.GetService<LoginViewModel>() ?? new LoginViewModel();
    }

    public void Receive(NavigationMessage message)
    {
        CurrentViewModel = message.ViewModel;
    }
}
