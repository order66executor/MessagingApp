using Messaging.UI.ViewModels;

namespace Messaging.UI.Messages;

public class NavigationMessage
{
    public ViewModelBase ViewModel { get; }

    public NavigationMessage(ViewModelBase viewModel)
    {
        ViewModel = viewModel;
    }
}
