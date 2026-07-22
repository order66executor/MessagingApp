using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    public MainViewModel()
    {
        // By default, start with the Login view
        // We use the DI container if it's available (it might be null during design time)
        _currentViewModel = App.Services?.GetService<LoginViewModel>() ?? new LoginViewModel(this);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
    }
}
