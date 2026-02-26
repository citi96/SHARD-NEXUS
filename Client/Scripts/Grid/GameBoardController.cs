#nullable enable
using Client.Scripts.UI;
using Godot;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Client.Scripts.Grid;

/// <summary>
/// Parent Control node for the battle board UI layer (grid + bench).
/// Mediates between GridRenderer, BenchRenderer, InterventionPanel, and GameClient:
/// initializes renderers with the shared ClientStateManager, translates bench
/// selection + cell click into a PositionEcho network call, handles intervention
/// target selection (InterventionRequested → CombatCellClicked → SendUseIntervention),
/// and toggles board visibility based on the current game phase.
///
/// Expected scene hierarchy:
///   Main (GameClient)
///     ├── BattleBoard (GameBoardController)
///     │     ├── GridRenderer  (GridRenderer)
///     │     └── BenchArea     (Control)
///     │           └── BenchRenderer (BenchRenderer)
///     └── InterventionPanel (InterventionPanel)
/// </summary>
public partial class GameBoardController : Control
{
    [Export] public NodePath GridRendererPath = "GridRenderer";
    [Export] public NodePath BenchRendererPath = "BenchArea/BenchRenderer";

    private GameClient? _gameClient;
    private GridRenderer? _gridRenderer;
    private BenchRenderer? _benchRenderer;
    private InterventionPanel? _interventionPanel;
    private ClientStateManager? _sm;
    private int _selectedInstanceId = -1;
    private InterventionType? _pendingIntervention = null;
    private int _pendingButtonIndex = -1;

    public override void _Ready()
    {
        _gameClient = GetParent<GameClient>();
        _gridRenderer = GetNode<GridRenderer>(GridRendererPath);
        _benchRenderer = GetNode<BenchRenderer>(BenchRendererPath);

        // InterventionPanel is a sibling under GameClient
        _interventionPanel = GetParent().GetNode<InterventionPanel>("InterventionPanel");

        _sm = _gameClient.StateManager;
        _gridRenderer.Initialize(_sm);
        _benchRenderer.Initialize(_sm);

        _gridRenderer.CellClicked += OnGridCellClicked;
        _gridRenderer.RemoveFromBoardRequested += OnRemoveFromBoardRequested;
        _gridRenderer.CombatCellClicked += OnCombatCellClicked;
        _benchRenderer.EchoSelected += OnBenchEchoSelected;
        _benchRenderer.SellRequested += OnSellRequested;
        _interventionPanel.InterventionRequested += OnInterventionRequested;

        _sm.OnPhaseChanged += OnPhaseChanged;
        _sm.OnRoundStarted += OnRoundStarted;
        _sm.OnCombatStarted += OnCombatStarted;

        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_gridRenderer != null)
        {
            _gridRenderer.CellClicked -= OnGridCellClicked;
            _gridRenderer.RemoveFromBoardRequested -= OnRemoveFromBoardRequested;
            _gridRenderer.CombatCellClicked -= OnCombatCellClicked;
        }
        if (_benchRenderer != null)
        {
            _benchRenderer.EchoSelected -= OnBenchEchoSelected;
            _benchRenderer.SellRequested -= OnSellRequested;
        }
        if (_interventionPanel != null)
            _interventionPanel.InterventionRequested -= OnInterventionRequested;

        if (_sm == null) return;
        _sm.OnPhaseChanged -= OnPhaseChanged;
        _sm.OnRoundStarted -= OnRoundStarted;
        _sm.OnCombatStarted -= OnCombatStarted;
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

    private void OnRemoveFromBoardRequested(int instanceId)
    {
        _gameClient?.SendRemoveFromBoard(instanceId);
        ClearSelection();
    }

    private void OnSellRequested(int instanceId)
    {
        _gameClient?.SendSellEcho(instanceId);
        ClearSelection();
    }

    private void OnInterventionRequested(int typeIndex)
    {
        var type = InterventionPanel.ButtonTypes[typeIndex];

        // Accelerate has no target — send immediately
        if (type == InterventionType.Accelerate)
        {
            _gameClient?.SendUseIntervention(type, -1);
            _interventionPanel?.SetPendingMode(false, -1);
            return;
        }

        _pendingIntervention = type;
        _pendingButtonIndex = typeIndex;

        bool allyTarget = type != InterventionType.Focus;
        bool enemyTarget = type == InterventionType.Focus;
        _gridRenderer?.SetTargetSelectionMode(true, allyTarget, enemyTarget);
    }

    private void OnCombatCellClicked(int col, int row)
    {
        if (_pendingIntervention == null) return;

        int id = GetInstanceIdAtCell(col, row);
        if (id == -1) return; // empty cell or invalid

        _gameClient?.SendUseIntervention(_pendingIntervention.Value, id);
        CancelInterventionSelection();
    }

    /// <summary>Returns the unit instance ID at (col, row) on the combined combat board, or -1.</summary>
    private int GetInstanceIdAtCell(int col, int row)
    {
        const int ac = GridRenderer.AllyCols;

        if (col < ac)
        {
            var ids = _sm?.OwnState?.BoardEchoInstanceIds;
            int idx = row * ac + col;
            return (ids != null && idx < ids.Length) ? ids[idx] : -1;
        }
        else
        {
            var ids = _sm?.CombatOpponentState?.BoardEchoInstanceIds;
            int idx = row * ac + (col - ac);
            return (ids != null && idx < ids.Length) ? ids[idx] : -1;
        }
    }

    private void CancelInterventionSelection()
    {
        _pendingIntervention = null;
        _pendingButtonIndex = -1;
        _gridRenderer?.SetTargetSelectionMode(false, false, false);
        _interventionPanel?.SetPendingMode(false, -1);
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

        CancelInterventionSelection();
    }

    private void ClearSelection()
    {
        _selectedInstanceId = -1;
        _benchRenderer?.SetSelectedSlot(-1);
        _gridRenderer?.ClearDropZone();
    }
}
