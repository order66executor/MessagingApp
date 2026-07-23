using System;
using CommunityToolkit.Mvvm.ComponentModel;
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

    // Computed properties for UI layout
    public string Alignment => IsOwnMessage ? "Right" : "Left";
    public string BackgroundColor => IsOwnMessage ? "#6610f2" : "#2A2D3E";
    public string CornerRadius => IsOwnMessage ? "16,16,0,16" : "16,16,16,0";

    // Fix #7: Notify UI when IsOwnMessage changes so computed properties update
    partial void OnIsOwnMessageChanged(bool value)
    {
        OnPropertyChanged(nameof(Alignment));
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(CornerRadius));
    }

    // For later: Attachment info
    [ObservableProperty]
    private bool _hasAttachment;
}
