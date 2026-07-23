using Avalonia.Controls;
using System.Collections.Specialized;
using Messaging.UI.ViewModels;

namespace Messaging.UI.Views;

public partial class ChatAreaView : UserControl
{
    public ChatAreaView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ChatAreaViewModel vm)
        {
            vm.Messages.CollectionChanged += Messages_CollectionChanged;
        }
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
            if (scrollViewer != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    scrollViewer.ScrollToEnd();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }
    }
}
