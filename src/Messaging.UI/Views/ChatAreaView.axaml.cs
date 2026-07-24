using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Specialized;
using Messaging.UI.ViewModels;

namespace Messaging.UI.Views;

public partial class ChatAreaView : UserControl
{
    private ChatAreaViewModel? _previousVm;

    public ChatAreaView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_previousVm != null)
        {
            _previousVm.Messages.CollectionChanged -= Messages_CollectionChanged;
        }

        if (DataContext is ChatAreaViewModel vm)
        {
            vm.Messages.CollectionChanged += Messages_CollectionChanged;
            _previousVm = vm;
        }
        else
        {
            _previousVm = null;
        }
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Post with low priority so layout is calculated before we scroll
            Dispatcher.UIThread.Post(() =>
            {
                var scrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
                if (scrollViewer == null) return;

                SmoothScrollToEnd(scrollViewer);
            }, DispatcherPriority.Background);
        }
    }

    private void SmoothScrollToEnd(ScrollViewer scrollViewer)
    {
        double startY = scrollViewer.Offset.Y;
        double targetY = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;

        if (targetY < 0) targetY = 0;

        // If already at the bottom or very close, just snap
        if (Math.Abs(targetY - startY) < 1)
        {
            scrollViewer.Offset = new Vector(0, targetY);
            return;
        }

        const int totalMs = 300;
        const int intervalMs = 16; // ~60fps
        int elapsed = 0;

        var timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };

        timer.Tick += (_, _) =>
        {
            elapsed += intervalMs;
            double progress = Math.Min(1.0, (double)elapsed / totalMs);

            // Cubic ease-out: 1 - (1 - t)^3
            double eased = 1.0 - Math.Pow(1.0 - progress, 3);

            double currentY = startY + (targetY - startY) * eased;
            scrollViewer.Offset = new Vector(0, currentY);

            if (progress >= 1.0)
            {
                timer.Stop();
            }
        };

        timer.Start();
    }
}
