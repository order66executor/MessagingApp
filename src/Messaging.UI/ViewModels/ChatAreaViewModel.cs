using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Messaging.UI.Services;
using Messaging.UI.Messages;

namespace Messaging.UI.ViewModels;

public partial class ChatAreaViewModel : ViewModelBase, IRecipient<ConversationSelectedMessage>, IRecipient<NewMessageReceivedMessage>
{
    private readonly AppSession? _appSession;

    [ObservableProperty]
    private string? _partnerUsername;

    [ObservableProperty]
    private string _newMessageText = "";

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

        // UI Updates MUST happen on UI thread, but GetMessagesAsync is asynchronous
        // CommunityToolkit handles property changes, but ObservableCollection updates
        // from async might need Dispatcher. Let's do it safely:
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var wrapper in history)
            {
                var msgData = System.Text.Json.JsonSerializer.Deserialize<Messaging.Shared.Models.MessageData>(wrapper.SerializedMessageData);
                if (msgData != null)
                {
                    string text = System.Text.Encoding.UTF8.GetString(msgData.Payload);
                    bool isOwn = wrapper.SenderUsername == _appSession.CurrentUsername;
                    
                    Messages.Add(new ChatMessageViewModel
                    {
                        Text = text,
                        IsOwnMessage = isOwn,
                        Timestamp = msgData.SentAtUtc.ToLocalTime(),
                        State = wrapper.State
                    });
                }
            }
        });
    }

    public void Receive(NewMessageReceivedMessage message)
    {
        if (string.IsNullOrEmpty(PartnerUsername) || _appSession?.CurrentUsername == null) return;

        // Check if message belongs to the current conversation
        bool isForCurrentPartner = (message.Wrapper.SenderUsername == PartnerUsername && message.Wrapper.ReceiverUsername == _appSession.CurrentUsername) ||
                                   (message.Wrapper.SenderUsername == _appSession.CurrentUsername && message.Wrapper.ReceiverUsername == PartnerUsername);

        if (isForCurrentPartner)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var msgData = System.Text.Json.JsonSerializer.Deserialize<Messaging.Shared.Models.MessageData>(message.Wrapper.SerializedMessageData);
                if (msgData != null)
                {
                    string text = System.Text.Encoding.UTF8.GetString(msgData.Payload);
                    bool isOwn = message.Wrapper.SenderUsername == _appSession.CurrentUsername;
                    
                    Messages.Add(new ChatMessageViewModel
                    {
                        Text = text,
                        IsOwnMessage = isOwn,
                        Timestamp = msgData.SentAtUtc.ToLocalTime(),
                        State = message.Wrapper.State
                    });
                }
            });
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || string.IsNullOrWhiteSpace(PartnerUsername)) return;
        if (_appSession?.Client == null) return;

        string text = MessageInput.Trim();
        MessageInput = "";

        // Send to network
        await _appSession.Client.SendTextMessageAsync(PartnerUsername, text);
    }
}
