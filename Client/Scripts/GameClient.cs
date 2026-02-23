using Godot;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Shared.Network.Messages;

namespace Client.Scripts;

public partial class GameClient : Node
{
    [Export] public int MaxPlayers = 2;
    private TcpClient _tcpClient;
    private NetworkStream _stream;
    private bool _isConnected;
    
    private Label _statusLabel;
    private LineEdit _nameInput;
    private Button _joinButton;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("UI/Panel/StatusLabel");
        _nameInput = GetNode<LineEdit>("UI/Panel/NameInput");
        _joinButton = GetNode<Button>("UI/Panel/JoinButton");

        _joinButton.Pressed += OnJoinButtonPressed;
        _statusLabel.Text = "Inserisci il nome e connettiti.";
    }

    private void OnJoinButtonPressed()
    {
        string playerName = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            _statusLabel.Text = "Il nome non può essere vuoto!";
            return;
        }

        _nameInput.Editable = false;
        _joinButton.Disabled = true;
        
        ConnectToServer(playerName);
    }

    private void ConnectToServer(string playerName)
    {
        _statusLabel.Text = "Connessione al server...";
        
        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect("127.0.0.1", 7777);
            _stream = _tcpClient.GetStream();
            _isConnected = true;

            // Avvia la lettura asincrona
            _ = Task.Run(ReceiveMessagesAsync);

            // Invia il messaggio di JoinLobby
            var joinMsg = NetworkMessage.Create(MessageType.JoinLobby, new JoinLobbyMessage { PlayerName = playerName });
            SendMessage(joinMsg);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Impossibile connettersi al server.";
            GD.PrintErr("Errore di connessione: ", ex.Message);
            _nameInput.Editable = true;
            _joinButton.Disabled = false;
        }
    }

    private void SendMessage(NetworkMessage message)
    {
        if (!_isConnected || _stream == null) return;

        try
        {
            string json = message.ToJson();
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

            _stream.Write(lengthPrefix, 0, lengthPrefix.Length);
            _stream.Write(payload, 0, payload.Length);
        }
        catch (Exception ex)
        {
            GD.PrintErr("Errore invio messaggio: ", ex.Message);
            Disconnect();
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        byte[] lengthBuffer = new byte[4];

        try
        {
            while (_isConnected && _tcpClient.Connected)
            {
                int lengthBytesRead = await _stream.ReadAsync(lengthBuffer, 0, 4);
                if (lengthBytesRead == 0) break;

                int jsonLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (jsonLength <= 0 || jsonLength > 1048576) break;

                byte[] jsonBuffer = new byte[jsonLength];
                int totalRead = 0;
                while (totalRead < jsonLength)
                {
                    int read = await _stream.ReadAsync(jsonBuffer, totalRead, jsonLength - totalRead);
                    if (read == 0) throw new Exception("Connessione chiusa durante la lettura.");
                    totalRead += read;
                }

                string json = Encoding.UTF8.GetString(jsonBuffer);
                var message = NetworkMessage.FromJson(json);

                if (message != null)
                {
                    // Chiamare in modo sicuro la UI dal thread principale di Godot
                    CallDeferred(nameof(HandleNetworkMessage), message.ToJson());
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr("Errore o disconnessione: ", ex.Message);
        }
        finally
        {
            CallDeferred(nameof(Disconnect));
        }
    }

    private void HandleNetworkMessage(string jsonMessage)
    {
        var message = NetworkMessage.FromJson(jsonMessage);
        if (message == null) return;

        switch (message.Type)
        {
            case MessageType.JoinLobbyResponse:
                var joinResp = message.DeserializePayload<JoinLobbyMessage>();
                _statusLabel.Text = $"In attesa di giocatori... ({joinResp?.PlayerName})";
                _nameInput.Hide();
                _joinButton.Hide();
                break;

            case MessageType.StartRound:
                var startMsg = message.DeserializePayload<StartRoundMessage>();
                _statusLabel.Text = $"MATCH INIZIATO! Round {startMsg?.RoundNumber}";
                GD.Print("Match Avviato dal Server!");
                break;
        }
    }

    private void Disconnect()
    {
        _isConnected = false;
        _stream?.Close();
        _tcpClient?.Close();
        _statusLabel.Text = "Disconnesso dal server.";
        _nameInput.Editable = true;
        _joinButton.Disabled = false;
        _nameInput.Show();
        _joinButton.Show();
    }
    
    public override void _ExitTree()
    {
        Disconnect();
    }
}
