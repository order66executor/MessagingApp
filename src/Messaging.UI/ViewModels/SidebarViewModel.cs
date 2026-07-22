using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Messaging.UI.ViewModels;

// Simple message to notify that a conversation was selected
public class ConversationSelectedMessage
{
    public string PartnerUsername { get; }
    public ConversationSelectedMessage(string partnerUsername) => PartnerUsername = partnerUsername;
}

public partial class SidebarViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> _conversations = new();

    [ObservableProperty]
    private string _newContactUsername = "";

    [ObservableProperty]
    private string? _selectedConversation;

    public SidebarViewModel()
    {
        // TODO: Load history from DB
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
