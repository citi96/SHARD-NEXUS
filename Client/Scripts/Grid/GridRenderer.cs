#nullable enable
using Godot;
using Shared.Data;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Client.Scripts.Grid;

/// <summary>
/// Renders the 8-column (4 ally + 4 enemy) battle grid using Godot's _Draw() API.
/// Ally columns are on the left (cols 0-3), enemy on the right (cols 4-7).
///
/// Board index contract (ally side, matches server PositionEcho handler):
///   boardIndex = row * AllyCols + col
///
/// Initialization: call Initialize(ClientStateManager) from GameBoardController._Ready()
/// after both nodes are in the scene tree.
/// </summary>
public partial class GridRenderer : Control
{
    public const int TotalCols = 8;
    public const int AllyCols = 4;
    public const int Rows = 4;
    public const int CellSize = 64;

    [ExportGroup("Colors")]
    [Export] public Color AllyBase = new(0.20f, 0.40f, 0.80f, 0.25f);
    [Export] public Color EnemyBase = new(0.80f, 0.20f, 0.20f, 0.25f);
    [Export] public Color AllyOccupied = new(0.30f, 0.60f, 1.00f, 0.60f);
    [Export] public Color EnemyOccupied = new(1.00f, 0.40f, 0.40f, 0.60f);
    [Export] public Color HoverTint = new(1.00f, 1.00f, 1.00f, 0.18f);
    [Export] public Color DropZoneBorder = new(0.00f, 1.00f, 0.40f, 0.90f);
    [Export] public Color GridLine = new(1.00f, 1.00f, 1.00f, 0.10f);
    [Export] public Color Divider = new(1.00f, 1.00f, 1.00f, 0.35f);
    [Export] public Color EchoNameColor = new(1.00f, 1.00f, 1.00f, 1.00f);

    /// <summary>
    /// Emitted when the player left-clicks an ally cell during the Preparation phase.
    /// </summary>
    [Signal]
    public delegate void CellClickedEventHandler(int col, int row);

    private ClientStateManager? _stateManager;
    private PlayerState? _ownState;
    private GamePhase _currentPhase = GamePhase.WaitingForPlayers;
    private Vector2I _hovered = new(-1, -1);
    private Vector2I _dropZone = new(-1, -1);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(TotalCols * CellSize, Rows * CellSize);
        MouseFilter = MouseFilterEnum.Stop;
        MouseExited += OnMouseExited;
    }

    /// <summary>
    /// Wires this renderer to the shared state manager.
    /// Must be called by GameBoardController after both nodes are in the scene tree.
    /// </summary>
    public void Initialize(ClientStateManager stateManager)
    {
        _stateManager = stateManager;
        _stateManager.OnOwnStateChanged += OnOwnStateChanged;
        _stateManager.OnPhaseChanged += OnPhaseChanged;
    }

    public override void _ExitTree()
    {
        if (_stateManager == null) return;
        _stateManager.OnOwnStateChanged -= OnOwnStateChanged;
        _stateManager.OnPhaseChanged -= OnPhaseChanged;
    }

    /// <summary>
    /// Activates a green border on the target cell as placement feedback.
    /// Cleared automatically when the server's PlayerStateUpdate arrives.
    /// </summary>
    public void SetDropZoneActive(int col, int row)
    {
        _dropZone = new Vector2I(col, row);
        QueueRedraw();
    }

    public void ClearDropZone()
    {
        _dropZone = new Vector2I(-1, -1);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawBackgrounds();
        DrawEchoData();
        DrawHoverTint();
        DrawDropZoneBorders();
        DrawGridLines();
        DrawDivider();
    }

    private void DrawBackgrounds()
    {
        for (int col = 0; col < TotalCols; col++)
            for (int row = 0; row < Rows; row++)
            {
                var rect = CellRect(col, row);
                bool isAlly = col < AllyCols;

                DrawRect(rect, isAlly ? AllyBase : EnemyBase);

                if (isAlly && IsAllyOccupied(col, row))
                    DrawRect(rect, AllyOccupied);
                else if (!isAlly && IsEnemyOccupied(col, row))
                    DrawRect(rect, EnemyOccupied);
            }
    }

    private void DrawEchoData()
    {
        var font = ThemeDB.FallbackFont;
        const int fs = 11;

        if (_ownState.HasValue)
        {
            var ids = _ownState.Value.BoardEchoInstanceIds;
            for (int col = 0; col < AllyCols; col++)
                for (int row = 0; row < Rows; row++)
                {
                    int idx = row * AllyCols + col;
                    if (idx >= ids.Length || ids[idx] == -1) continue;
                    DrawEchoInCell(col, row, ids[idx], font, fs);
                }
        }

        // Enemy board — populated during Combat via CombatOpponentState
        var opp = _stateManager?.CombatOpponentState;
        if (opp.HasValue)
        {
            var ids = opp.Value.BoardEchoInstanceIds;
            for (int localCol = 0; localCol < AllyCols; localCol++)
                for (int row = 0; row < Rows; row++)
                {
                    int idx = row * AllyCols + localCol;
                    int displayCol = localCol + AllyCols;
                    if (idx >= ids.Length || ids[idx] == -1) continue;
                    DrawEchoInCell(displayCol, row, ids[idx], font, fs);
                }
        }
    }

    private void DrawEchoInCell(int col, int row, int instanceId, Font font, int fontSize)
    {
        var def = EchoCatalog.GetByInstanceId(instanceId);
        string name = def?.Name ?? $"#{instanceId / 1000}";
        string stars = "\u2605"; // ★ — hardcoded 1-star until star-up system is added

        var rect = CellRect(col, row);
        var namePos = rect.Position + new Vector2(4, fontSize + 4);
        var starsPos = rect.Position + new Vector2(4, fontSize * 2 + 6);

        DrawString(font, namePos, name, HorizontalAlignment.Left, rect.Size.X - 4, fontSize, EchoNameColor);
        DrawString(font, starsPos, stars, HorizontalAlignment.Left, rect.Size.X - 4, fontSize, EchoNameColor);
    }

    private void DrawHoverTint()
    {
        if (_hovered.X < 0) return;
        if (_currentPhase != GamePhase.Preparation) return;
        if (_hovered.X >= AllyCols) return;

        DrawRect(CellRect(_hovered.X, _hovered.Y), HoverTint);
    }

    private void DrawDropZoneBorders()
    {
        if (_dropZone.X < 0) return;
        DrawRect(CellRect(_dropZone.X, _dropZone.Y), DropZoneBorder, false, 2f);
    }

    private void DrawGridLines()
    {
        for (int col = 0; col <= TotalCols; col++)
            DrawLine(new Vector2(col * CellSize, 0),
                     new Vector2(col * CellSize, Rows * CellSize),
                     GridLine);

        for (int row = 0; row <= Rows; row++)
            DrawLine(new Vector2(0, row * CellSize),
                     new Vector2(TotalCols * CellSize, row * CellSize),
                     GridLine);
    }

    private void DrawDivider()
    {
        DrawLine(new Vector2(AllyCols * CellSize, 0),
                 new Vector2(AllyCols * CellSize, Rows * CellSize),
                 Divider, 2f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                UpdateHover(motion.Position);
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
                HandleClick(click.Position);
                break;
        }
    }

    private void UpdateHover(Vector2 position)
    {
        int col = Mathf.Clamp((int)(position.X / CellSize), 0, TotalCols - 1);
        int row = Mathf.Clamp((int)(position.Y / CellSize), 0, Rows - 1);
        var next = new Vector2I(col, row);
        if (_hovered == next) return;
        _hovered = next;
        QueueRedraw();
    }

    private void HandleClick(Vector2 position)
    {
        if (_currentPhase != GamePhase.Preparation) return;

        int col = Mathf.Clamp((int)(position.X / CellSize), 0, TotalCols - 1);
        int row = Mathf.Clamp((int)(position.Y / CellSize), 0, Rows - 1);

        if (col >= AllyCols) return;

        EmitSignal(SignalName.CellClicked, col, row);
    }

    private void OnMouseExited()
    {
        _hovered = new Vector2I(-1, -1);
        QueueRedraw();
    }

    private void OnOwnStateChanged(PlayerState state)
    {
        _ownState = state;
        _dropZone = new Vector2I(-1, -1); // server confirmed: clear pending hint
        QueueRedraw();
    }

    /// <summary>
    /// Sets the active game phase and refreshes the grid accordingly.
    /// Called by GameBoardController when the server drives a phase transition
    /// (OnRoundStarted → Preparation, OnCombatStarted → Combat) in addition to
    /// the regular OnPhaseChanged event for when the server sends explicit PhaseChanged messages.
    /// </summary>
    public void SetGamePhase(GamePhase phase)
    {
        _currentPhase = phase;

        if (phase == GamePhase.Combat)
        {
            _hovered = new Vector2I(-1, -1);
            _dropZone = new Vector2I(-1, -1);
        }

        QueueRedraw();
    }

    private void OnPhaseChanged(GamePhase phase, float _duration) => SetGamePhase(phase);

    private static Rect2 CellRect(int col, int row) =>
        new(col * CellSize, row * CellSize, CellSize, CellSize);

    private bool IsAllyOccupied(int col, int row)
    {
        if (!_ownState.HasValue) return false;
        int idx = row * AllyCols + col;
        var ids = _ownState.Value.BoardEchoInstanceIds;
        return idx < ids.Length && ids[idx] != -1;
    }

    private bool IsEnemyOccupied(int col, int row)
    {
        var opp = _stateManager?.CombatOpponentState;
        if (!opp.HasValue) return false;
        int localCol = col - AllyCols;
        int idx = row * AllyCols + localCol;
        var ids = opp.Value.BoardEchoInstanceIds;
        return idx < ids.Length && ids[idx] != -1;
    }
}
