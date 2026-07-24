using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Messaging.Client;
using Messaging.Client.Protocols;

namespace Messaging.UI.Services;

public class AppSession
{
    public MessageClient? Client { get; private set; }
    private CancellationTokenSource? _cts;

    public string? CurrentUsername { get; private set; }

    public async Task<bool> ConnectAsync(string ipAddress, int port, string username, bool useTls)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            return false;
        }

        CurrentUsername = username;
        Client = new MessageClient(address, port, new ClientProtocolFactory(), username, useTls);
        Client.DbHandler.OnMessageAdded += (wrapper) => 
        {
            WeakReferenceMessenger.Default.Send(new Messages.NewMessageReceivedMessage(wrapper));
        };
        _cts = new CancellationTokenSource();

        // Start connection in the background
        // RunAsync connects, sends intro, waits for ack. 
        // We will wrap RunAsync in a Task so it doesn't block the UI
        var connectTask = Task.Run(async () =>
        {
            try
            {
                await Client.RunAsync(_cts);
            }
            catch
            {
                // Connection dropped or failed
            }
        });

        // We need a way to know if connection succeeded.
        // Currently MessageClient.RunAsync prints to console and returns void on success, 
        // or returns early on failure.
        // For a proper UI, we might need to modify MessageClient to throw or return bool,
        // but for now, we just wait a bit to see if it throws or exits early.
        await Task.Delay(500); // Give it a moment to connect

        // If task completed already, it means it failed or exited
        if (connectTask.IsCompleted)
        {
            return false;
        }

        return true;
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        Client = null;
    }
}
