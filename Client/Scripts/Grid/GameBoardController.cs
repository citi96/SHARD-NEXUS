#nullable enable
using Godot;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Client.Scripts.Grid;

/// <summary>
/// Parent Control node for the battle board UI layer (grid + bench).
/// Mediates between GridRenderer, BenchRenderer, and GameClient:
/// initializes renderers with the shared ClientStateManager, translates bench
/// selection + cell click into a PositionEcho network call, and toggles board
/// visibility based on the current game phase.
///
/// Expected scene hierarchy:
///   Main (GameClient)
///     └── BattleBoard (GameBoardController)
///           ├── GridRenderer  (GridRenderer)
///           └── BenchArea     (Control)
///                 └── BenchRenderer (BenchRenderer)
/// </summary>
public partial class GameBoardController : Control
{
    [Export] public NodePath GridRendererPath = "GridRenderer";
    [Export] public NodePath BenchRendererPath = "BenchArea/BenchRenderer";

    private GameClient? _gameClient;
    private GridRenderer? _gridRenderer;
    private BenchRenderer? _benchRenderer;
    private int _selectedInstanceId = -1;

    public override void _Ready()
    {
        _gameClient = GetParent<GameClient>();
        _gridRenderer = GetNode<GridRenderer>(GridRendererPath);
        _benchRenderer = GetNode<BenchRenderer>(BenchRendererPath);

        var sm = _gameClient.StateManager;
        _gridRenderer.Initialize(sm);
        _benchRenderer.Initialize(sm);

        _gridRenderer.CellClicked += OnGridCellClicked;
        _benchRenderer.EchoSelected += OnBenchEchoSelected;

        sm.OnPhaseChanged += OnPhaseChanged;
        // OnRoundStarted fires when the server starts a match — treated as the start
        // of the Preparation phase until the server sends explicit PhaseChanged messages.
        sm.OnRoundStarted += OnRoundStarted;
        sm.OnCombatStarted += OnCombatStarted;

        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_gameClient == null) return;
        var sm = _gameClient.StateManager;
        sm.OnPhaseChanged -= OnPhaseChanged;
        sm.OnRoundStarted -= OnRoundStarted;
        sm.OnCombatStarted -= OnCombatStarted;
    }

    private void OnBenchEchoSelected(int instanceId, int slotIndex)
    {
        if (_selectedInstanceId == instanceId)
        {
            ClearSelection();
            return;
        }

        _selectedInstanceId = instanceId;
        _benchRenderer?.SetSelectedSlot(slotIndex);
        _gridRenderer?.ClearDropZone();
    }

    private void OnGridCellClicked(int col, int row)
    {
        if (_selectedInstanceId == -1 || _gameClient == null) return;

        _gameClient.SendPositionEcho(_selectedInstanceId, col, row);
        _gridRenderer?.SetDropZoneActive(col, row);
        ClearSelection();
    }

    private void OnRoundStarted(int _roundNumber)
    {
        Visible = true;
        _gridRenderer?.SetGamePhase(GamePhase.Preparation);
        _benchRenderer?.SetGamePhase(GamePhase.Preparation);
    }

    private void OnCombatStarted(int _opponentId, PlayerState _opponentState)
    {
        _gridRenderer?.SetGamePhase(GamePhase.Combat);
        _benchRenderer?.SetGamePhase(GamePhase.Combat);
        ClearSelection();
    }

    private void OnPhaseChanged(GamePhase phase, float _duration)
    {
        Visible = phase is GamePhase.Preparation or GamePhase.Combat or GamePhase.Reward;

        if (phase != GamePhase.Preparation)
            ClearSelection();
    }

    private void ClearSelection()
    {
        _selectedInstanceId = -1;
        _benchRenderer?.SetSelectedSlot(-1);
        _gridRenderer?.ClearDropZone();
    }
}
