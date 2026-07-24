using System;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Messaging.Shared.Models;

namespace Messaging.UI.ViewModels;

public partial class ChatMessageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private bool _isOwnMessage;

    [ObservableProperty]
    private DateTime _timestamp;

    [ObservableProperty]
    private MessageState _state;

    // Computed properties returning proper Avalonia types for XAML binding
    public HorizontalAlignment Alignment => IsOwnMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public IBrush Background => IsOwnMessage ? new SolidColorBrush(Color.Parse("#6610f2")) : new SolidColorBrush(Color.Parse("#2A2D3E"));
    public CornerRadius BubbleCornerRadius => IsOwnMessage ? new CornerRadius(16, 16, 0, 16) : new CornerRadius(16, 16, 16, 0);

    partial void OnIsOwnMessageChanged(bool value)
    {
        OnPropertyChanged(nameof(Alignment));
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(BubbleCornerRadius));
    }

    [ObservableProperty]
    private bool _isFileNotification;

    [ObservableProperty]
    private string? _fileId;

    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    private string? _fileSizeDisplay;

    public Action<string>? DownloadAction { get; set; }

    [RelayCommand]
    private void Download()
    {
        if (FileId != null && DownloadAction != null)
        {
            DownloadAction.Invoke(FileId);
        }
    }
}
