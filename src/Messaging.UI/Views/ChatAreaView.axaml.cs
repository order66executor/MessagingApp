using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
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

        // Fix #2: Unsubscribe from old DataContext to prevent memory leak
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
            // Fix #1: Flowy smooth scroll instead of instant jump
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var scrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
                if (scrollViewer == null) return;

                double targetOffset = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
                if (targetOffset < 0) targetOffset = 0;

                // Animate the scroll offset for a smooth "flowy" effect
                var animation = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut(),
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(0.0),
                            Setters = { new Setter(ScrollViewer.OffsetProperty, scrollViewer.Offset) }
                        },
                        new KeyFrame
                        {
                            Cue = new Cue(1.0),
                            Setters = { new Setter(ScrollViewer.OffsetProperty, new Vector(0, targetOffset)) }
                        }
                    }
                };

                animation.RunAsync(scrollViewer);
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}
