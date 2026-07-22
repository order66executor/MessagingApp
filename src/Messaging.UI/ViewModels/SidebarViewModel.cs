using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using Messaging.UI.Messages;

namespace Messaging.UI.ViewModels;

// Simple message to notify that a conversation was selected
public class ConversationSelectedMessage
{
    public string PartnerUsername { get; }
    public ConversationSelectedMessage(string partnerUsername) => PartnerUsername = partnerUsername;
}

public partial class SidebarViewModel : ViewModelBase, IRecipient<NewMessageReceivedMessage>
{
    [ObservableProperty]
    private ObservableCollection<string> _conversations = new();

    [ObservableProperty]
    private string _newContactUsername = "";

    [ObservableProperty]
    private string? _selectedConversation;

    private readonly Messaging.UI.Services.AppSession? _appSession;

    public SidebarViewModel()
    {
    }

    public SidebarViewModel(Messaging.UI.Services.AppSession appSession)
    {
        _appSession = appSession;
        WeakReferenceMessenger.Default.Register(this);
        _ = LoadConversationsAsync();
    }

    public void Receive(NewMessageReceivedMessage message)
    {
        if (_appSession?.CurrentUsername == null) return;

        var partner = message.Wrapper.SenderUsername == _appSession.CurrentUsername 
            ? message.Wrapper.ReceiverUsername 
            : message.Wrapper.SenderUsername;

        if (!Conversations.Contains(partner))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Conversations.Add(partner));
        }
    }

    private async Task LoadConversationsAsync()
    {
        if (_appSession?.Client?.DbHandler == null || string.IsNullOrEmpty(_appSession.CurrentUsername))
            return;

        var keys = await _appSession.Client.DbHandler.GetConversationsAsync();
        
        // Extract partner names from ConversationKeys like "Alice::Bob"
        foreach (var key in keys)
        {
            var parts = key.Split("::");
            if (parts.Length == 2)
            {
                var partner = parts[0] == _appSession.CurrentUsername ? parts[1] : parts[0];
                if (!Conversations.Contains(partner))
                {
                    Conversations.Add(partner);
                }
            }
        }
    }

    [RelayCommand]
    private void StartNewChat()
    {
        if (string.IsNullOrWhiteSpace(NewContactUsername)) return;
        
        string partner = NewContactUsername.Trim();
        
        if (!Conversations.Contains(partner))
        {
            Conversations.Add(partner);
        }
        
        SelectedConversation = partner;
        NewContactUsername = "";
    }

    partial void OnSelectedConversationChanged(string? value)
    {
        if (value != null)
        {
            WeakReferenceMessenger.Default.Send(new ConversationSelectedMessage(value));
        }
    }
}
