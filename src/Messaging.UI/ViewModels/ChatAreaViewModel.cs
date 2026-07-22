using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Messaging.UI.Services;

namespace Messaging.UI.ViewModels;

public partial class ChatAreaViewModel : ViewModelBase, IRecipient<ConversationSelectedMessage>
{
    private readonly AppSession? _appSession;

    [ObservableProperty]
    private string? _partnerUsername;

    [ObservableProperty]
    private ObservableCollection<string> _messages = new();

    [ObservableProperty]
    private string _messageInput = "";

    public ChatAreaViewModel()
    {
        WeakReferenceMessenger.Default.Register(this);
    }

    public ChatAreaViewModel(AppSession appSession) : this()
    {
        _appSession = appSession;
    }

    public void Receive(ConversationSelectedMessage message)
    {
        PartnerUsername = message.PartnerUsername;
        Messages.Clear();
        // TODO: Load chat history from DB
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

        // Optimistically add to UI
        Messages.Add($"Me: {text}");
    }
}
