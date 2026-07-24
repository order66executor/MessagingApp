using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Messaging.UI.Services;
using Messaging.UI.Messages;
using MessagePack;

namespace Messaging.UI.ViewModels;

public partial class ChatAreaViewModel : ViewModelBase, IRecipient<ConversationSelectedMessage>, IRecipient<NewMessageReceivedMessage>
{
    private readonly AppSession? _appSession;

    [ObservableProperty]
    private string? _partnerUsername;

    [ObservableProperty]
    private ObservableCollection<ChatMessageViewModel> _messages = new();

    [ObservableProperty]
    private string _messageInput = "";

    public ChatAreaViewModel()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ChatAreaViewModel(AppSession appSession) : this()
    {
        _appSession = appSession;
    }

    public async void Receive(ConversationSelectedMessage message)
    {
        PartnerUsername = message.PartnerUsername;
        Messages.Clear();

        if (_appSession?.Client?.DbHandler == null || string.IsNullOrEmpty(_appSession.CurrentUsername))
            return;

        var userA = new Messaging.Shared.Models.StringIdentifier(_appSession.CurrentUsername);
        var userB = new Messaging.Shared.Models.StringIdentifier(PartnerUsername);
        var convKey = Messaging.Shared.Data.DbUtil.GetConversationKey(userA, userB);

        var history = await _appSession.Client.DbHandler.GetMessagesAsync(convKey);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var wrapper in history)
            {
                var vm = CreateChatMessageViewModel(wrapper);
                if (vm != null) Messages.Add(vm);
            }
        });
    }

    public void Receive(NewMessageReceivedMessage message)
    {
        if (string.IsNullOrEmpty(PartnerUsername) || _appSession?.CurrentUsername == null) return;

        bool isForCurrentPartner = (message.Wrapper.SenderUsername == PartnerUsername && message.Wrapper.ReceiverUsername == _appSession.CurrentUsername) ||
                                   (message.Wrapper.SenderUsername == _appSession.CurrentUsername && message.Wrapper.ReceiverUsername == PartnerUsername);

        if (isForCurrentPartner)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var vm = CreateChatMessageViewModel(message.Wrapper);
                if (vm != null) Messages.Add(vm);
            });
        }
    }

    private ChatMessageViewModel? CreateChatMessageViewModel(Messaging.Shared.Models.MessageWrapper wrapper)
    {
        var msgData = MessagePackSerializer.Deserialize<Messaging.Shared.Models.MessageData>(wrapper.SerializedMessageData);
        if (msgData == null || _appSession?.CurrentUsername == null) return null;

        bool isOwn = wrapper.SenderUsername == _appSession.CurrentUsername;
        var vm = new ChatMessageViewModel
        {
            IsOwnMessage = isOwn,
            Timestamp = msgData.SentAtUtc.ToLocalTime(),
            State = wrapper.State,
            DownloadAction = DownloadFile
        };

        switch (msgData.Type)
        {
            case Messaging.Shared.Models.MessageType.TextMessage:
                vm.Text = System.Text.Encoding.UTF8.GetString(msgData.Payload);
                break;

            case Messaging.Shared.Models.MessageType.FileNotification:
                var notifPayload = MessagePackSerializer.Deserialize<Messaging.Shared.Models.FileNotificationPayload>(msgData.Payload);
                if (notifPayload != null)
                {
                    vm.IsFileNotification = true;
                    vm.FileId = notifPayload.FileId;
                    vm.FileName = notifPayload.FileName;
                    vm.FileSizeDisplay = $"{notifPayload.FileSize / 1024} KB";
                    vm.Text = $"📎 {notifPayload.FileName}";
                }
                break;

            case Messaging.Shared.Models.MessageType.FileUpload:
                var uploadPayload = MessagePackSerializer.Deserialize<Messaging.Shared.Models.FileUploadPayload>(msgData.Payload);
                vm.Text = uploadPayload != null ? $"📤 {uploadPayload.FileName} (elküldve)" : "📤 Fájl elküldve";
                break;

            default:
                return null; // Don't display unknown/internal message types (e.g. FileRequest)
        }

        return vm;
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || string.IsNullOrWhiteSpace(PartnerUsername)) return;
        if (_appSession?.Client == null) return;

        string text = MessageInput.Trim();
        MessageInput = "";

        // TODO: Pass password to backend in auth phase
        // Send to network
        await _appSession.Client.SendTextMessageAsync(PartnerUsername, text);
    }

    private void DownloadFile(string fileId)
    {
        if (_appSession?.Client == null) return;
        _ = _appSession.Client.RequestFileAsync(fileId);
    }

    [RelayCommand]
    private async Task SendFileAsync()
    {
        if (string.IsNullOrWhiteSpace(PartnerUsername)) return;
        if (_appSession?.Client == null) return;

        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;

        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select a file to send",
                AllowMultiple = false
            });

        if (files.Count == 0) return;

        string? filePath = files[0].Path.LocalPath;
        if (filePath == null) return;

        await _appSession.Client.SendFileAsync(PartnerUsername, filePath);
    }
}
