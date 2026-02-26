using Godot;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Shared.Models.Enums;
using Shared.Network.Messages;

namespace Client.Scripts;

public partial class GameClient : Node
{
    [Export] public string ServerIp = "127.0.0.1";
    [Export] public int ServerPort = 7777;
    [Export] public int MaxPlayers = 2;
    [Export] public float PingInterval = 2.0f;

    private TcpClient _tcpClient;
    private NetworkStream _stream;
    private bool _isConnected;

    // UI Elements - Login
    [ExportGroup("UI - Login")]
    [Export] public Control LoginPanel;
    [Export] public LineEdit NameInput;
    [Export] public Button JoinButton;
    [Export] public Label StatusLabel;

    // UI Elements - Lobby
    [ExportGroup("UI - Lobby")]
    [Export] public Control LobbyPanel;
    [Export] public VBoxContainer PlayersContainer;
    [Export] public Button ReadyButton;

    // State
    private bool _isReady = false;
    public ClientStateManager StateManager { get; } = new();

    // Ping/Pong
    private float _pingTimer = 0f;
    private long _lastPingTime;
    private int _currentLatencyMs = -1;

    public override void _Ready()
    {
        if (JoinButton != null) JoinButton.Pressed += OnJoinButtonPressed;
        if (ReadyButton != null) ReadyButton.Pressed += OnReadyButtonPressed;

        if (StatusLabel != null) StatusLabel.Text = "Inserisci il nome e connettiti.";

        if (LoginPanel != null) LoginPanel.Show();
        if (LobbyPanel != null) LobbyPanel.Hide();

        InitializeEmptySlots();
    }

    private void InitializeEmptySlots()
    {
        if (PlayersContainer == null) return;

        // Rimuove eventuali nodi esistenti
        foreach (Node child in PlayersContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Crea MaxPlayers slot vuoti
        for (int i = 0; i < MaxPlayers; i++)
        {
            var label = new Label();
            label.Text = $"Slot {i + 1}: Vuoto";
            PlayersContainer.AddChild(label);
        }
    }

    private void OnReadyButtonPressed()
    {
        _isReady = !_isReady;
        if (ReadyButton != null) ReadyButton.Text = _isReady ? "Annulla Pronto" : "Pronto";

        var readyMsg = NetworkMessage.Create(MessageType.ReadyUp, new ReadyUpMessage { IsReady = _isReady });
        SendMessage(readyMsg);
    }

    /// <summary>
    /// Sends a PositionEcho message to the server requesting placement of the given
    /// echo instance at ally board column <paramref name="boardX"/>, row <paramref name="boardY"/>.
    /// No optimistic update — the authoritative confirmation arrives as a PlayerStateUpdate.
    /// </summary>
    public void SendPositionEcho(int instanceId, int boardX, int boardY)
    {
        var msg = NetworkMessage.Create(
            MessageType.PositionEcho,
            new PositionEchoMessage
            {
                EchoInstanceId = instanceId,
                BoardX = boardX,
                BoardY = boardY
            });
        SendMessage(msg);
    }

    public void SendBuyEcho(int shopSlot)
        => SendMessage(NetworkMessage.Create(MessageType.BuyEcho,
           new BuyEchoMessage { ShopSlot = shopSlot }));

    public void SendSellEcho(int echoInstanceId)
        => SendMessage(NetworkMessage.Create(MessageType.SellEcho,
           new SellEchoMessage { EchoInstanceId = echoInstanceId }));

    public void SendRefreshShop()
        => SendMessage(NetworkMessage.Create(MessageType.RefreshShop, new RefreshShopMessage()));

    public void SendBuyXP()
        => SendMessage(NetworkMessage.Create(MessageType.BuyXP, new BuyXPMessage()));

    public void SendRemoveFromBoard(int echoInstanceId)
        => SendMessage(NetworkMessage.Create(MessageType.RemoveFromBoard,
           new RemoveFromBoardMessage { EchoInstanceId = echoInstanceId }));

    public void SendUseIntervention(InterventionType type, int targetId = -1)
        => SendMessage(NetworkMessage.Create(MessageType.UseIntervention,
           new UseInterventionMessage { CardId = type.ToString(), TargetId = targetId }));

    public override void _Process(double delta)
    {
        if (!_isConnected) return;

        _pingTimer += (float)delta;
        if (_pingTimer >= PingInterval)
        {
            _pingTimer = 0f;
            SendPing();
        }
    }

    private void SendPing()
    {
        _lastPingTime = DateTime.UtcNow.Ticks;
        var pingMsg = NetworkMessage.Create(MessageType.Ping, new PingMessage { Timestamp = _lastPingTime });
        SendMessage(pingMsg);
    }

    private async void OnJoinButtonPressed()
    {
        string playerName = NameInput?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(playerName))
        {
            if (StatusLabel != null) StatusLabel.Text = "Il nome non può essere vuoto!";
            return;
        }

        if (NameInput != null) NameInput.Editable = false;
        if (JoinButton != null) JoinButton.Disabled = true;

        await ConnectToServerAsync(playerName);
    }

    private async Task ConnectToServerAsync(string playerName)
    {
        if (StatusLabel != null) StatusLabel.Text = "Connessione al server...";

        try
        {
            _tcpClient = new TcpClient();
            // ConnectAsync prevents freezing the main thread
            await _tcpClient.ConnectAsync(ServerIp, ServerPort);

            _stream = _tcpClient.GetStream();
            _isConnected = true;

            // Avvia la lettura asincrona
            _ = Task.Run(ReceiveMessagesAsync);

            // Invia il messaggio di JoinLobby
            var joinMsg = NetworkMessage.Create(MessageType.JoinLobby, new JoinLobbyMessage { PlayerName = playerName });
            SendMessage(joinMsg);

            GD.Print($"[Network] Connesso al server {ServerIp}:{ServerPort}");
        }
        catch (Exception ex)
        {
            if (StatusLabel != null) StatusLabel.Text = "Errore: " + ex.Message;
            GD.PrintErr("Errore di connessione: ", ex.Message);
            if (NameInput != null) NameInput.Editable = true;
            if (JoinButton != null) JoinButton.Disabled = false;
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

        StateManager.HandleMessage(message);

        string pingSuffix = _currentLatencyMs >= 0 ? $" ({_currentLatencyMs}ms)" : "";

        switch (message.Type)
        {
            case MessageType.Pong:
                var pongResp = message.DeserializePayload<PongMessage>();
                if (pongResp != null)
                {
                    long now = DateTime.UtcNow.Ticks;
                    _currentLatencyMs = (int)((now - pongResp.OriginalTimestamp) / TimeSpan.TicksPerMillisecond);
                    if (StatusLabel != null && (StatusLabel.Text.Contains("attesa") || StatusLabel.Text.Contains("giocatori") || StatusLabel.Text.Contains("Lobby")))
                    {
                        UpdateStatusWithPing();
                    }
                }
                break;

            case MessageType.JoinLobbyResponse:
                var joinResp = message.DeserializePayload<JoinLobbyMessage>();
                if (StatusLabel != null) StatusLabel.Text = $"Connesso come {joinResp?.PlayerName}. Attendere...{pingSuffix}";
                if (LoginPanel != null) LoginPanel.Hide();
                if (LobbyPanel != null) LobbyPanel.Show();
                break;

            case MessageType.LobbyState:
                var lobbyState = message.DeserializePayload<LobbyStateMessage>();
                if (lobbyState != null)
                {
                    UpdateLobbyUI(lobbyState);
                }
                break;

            case MessageType.StartRound:
                var startMsg = message.DeserializePayload<StartRoundMessage>();
                if (StatusLabel != null) StatusLabel.Text = $"MATCH INIZIATO! Round {startMsg?.RoundNumber}{pingSuffix}";
                if (LobbyPanel != null) LobbyPanel.Hide();
                GD.Print("Match Avviato dal Server!");
                break;
        }
    }

    private void UpdateLobbyUI(LobbyStateMessage state)
    {
        if (PlayersContainer == null) return;

        // Clear existing slots first
        foreach (Node child in PlayersContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Fill slots with connected players
        int i = 0;
        foreach (var player in state.Players)
        {
            var label = new Label();
            string readyText = player.IsReady ? "[Pronto]" : "[In Attesa]";
            label.Text = $"Slot {i + 1}: {player.PlayerName} {readyText}";
            PlayersContainer.AddChild(label);
            i++;
        }

        // Fill remaining empty slots
        for (; i < MaxPlayers; i++)
        {
            var label = new Label();
            label.Text = $"Slot {i + 1}: Vuoto";
            PlayersContainer.AddChild(label);
        }

        // Update overall status
        if (StatusLabel != null)
        {
            if (state.CountdownRemaining >= 0)
            {
                StatusLabel.Text = $"Il match inizia in: {MathF.Ceiling(state.CountdownRemaining)}... ({_currentLatencyMs}ms)";
            }
            else if (state.AllReady)
            {
                StatusLabel.Text = $"Lobby Pronta. In attesa del server... ({_currentLatencyMs}ms)";
            }
            else
            {
                StatusLabel.Text = $"Lobby ({state.Players.Count}/{MaxPlayers}). In attesa di tutti i giocatori... ({_currentLatencyMs}ms)";
            }
        }
    }

    private void UpdateStatusWithPing()
    {
        if (StatusLabel == null) return;
        string currentText = StatusLabel.Text;
        int lastParen = currentText.LastIndexOf(" (");
        if (lastParen != -1 && currentText.EndsWith("ms)"))
        {
            currentText = currentText.Substring(0, lastParen);
        }
        StatusLabel.Text = $"{currentText} ({_currentLatencyMs}ms)";
    }

    private void Disconnect()
    {
        _isConnected = false;
        _currentLatencyMs = -1;
        _isReady = false;
        _stream?.Close();
        _tcpClient?.Close();

        if (StatusLabel != null) StatusLabel.Text = "Disconnesso dal server.";
        if (NameInput != null) NameInput.Editable = true;
        if (JoinButton != null) JoinButton.Disabled = false;
        if (ReadyButton != null) ReadyButton.Text = "Pronto";

        if (LoginPanel != null) LoginPanel.Show();
        if (LobbyPanel != null) LobbyPanel.Hide();
    }

    public override void _ExitTree()
    {
        Disconnect();
    }
}
