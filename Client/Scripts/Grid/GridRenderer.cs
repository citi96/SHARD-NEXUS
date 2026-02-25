#nullable enable
using Godot;
using Shared.Models.Structs;

namespace Client.Scripts.Grid;

public partial class GridRenderer : Control
{
    public const int Cols     = 8;
    public const int Rows     = 4;
    public const int CellSize = 64;

    private const int ServerCols = 7;

    [ExportGroup("Colors")]
    [Export] public Color AllyBase     = new(0.20f, 0.40f, 0.80f, 0.25f);
    [Export] public Color EnemyBase    = new(0.80f, 0.20f, 0.20f, 0.25f);
    [Export] public Color AllyOccupied = new(0.30f, 0.60f, 1.00f, 0.60f);
    [Export] public Color HoverTint    = new(1.00f, 1.00f, 1.00f, 0.18f);
    [Export] public Color GridLine     = new(1.00f, 1.00f, 1.00f, 0.10f);
    [Export] public Color Divider      = new(1.00f, 1.00f, 1.00f, 0.35f);

    private ClientStateManager? _stateManager;
    private Vector2I             _hovered  = new(-1, -1);
    private PlayerState?         _ownState;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Cols * CellSize, Rows * CellSize);
        MouseFilter       = MouseFilterEnum.Stop;
        MouseExited      += OnMouseExited;

        _stateManager = GetParent<GameClient>().StateManager;
        _stateManager.OnOwnStateChanged += OnOwnStateChanged;
    }

    public override void _ExitTree()
    {
        if (_stateManager != null)
            _stateManager.OnOwnStateChanged -= OnOwnStateChanged;
    }

    public override void _Draw()
    {
        for (int col = 0; col < Cols; col++)
        {
            for (int row = 0; row < Rows; row++)
            {
                var rect = new Rect2(col * CellSize, row * CellSize, CellSize, CellSize);

                DrawRect(rect, col < 4 ? AllyBase : EnemyBase);

                if (col < 4 && IsOccupied(col, row))
                    DrawRect(rect, AllyOccupied);

                if (_hovered.X == col && _hovered.Y == row)
                    DrawRect(rect, HoverTint);
            }
        }

        for (int col = 0; col <= Cols; col++)
            DrawLine(new Vector2(col * CellSize, 0), new Vector2(col * CellSize, Rows * CellSize), GridLine);

        for (int row = 0; row <= Rows; row++)
            DrawLine(new Vector2(0, row * CellSize), new Vector2(Cols * CellSize, row * CellSize), GridLine);

        DrawLine(new Vector2(4 * CellSize, 0), new Vector2(4 * CellSize, Rows * CellSize), Divider, 2f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            int col  = Mathf.Clamp((int)(motion.Position.X / CellSize), 0, Cols - 1);
            int row  = Mathf.Clamp((int)(motion.Position.Y / CellSize), 0, Rows - 1);
            var next = new Vector2I(col, row);
            if (_hovered != next)
            {
                _hovered = next;
                QueueRedraw();
            }
        }
    }

    private void OnMouseExited()
    {
        _hovered = new Vector2I(-1, -1);
        QueueRedraw();
    }

    private void OnOwnStateChanged(PlayerState state)
    {
        _ownState = state;
        QueueRedraw();
    }

    private bool IsOccupied(int col, int row)
    {
        if (!_ownState.HasValue) return false;
        int boardIdx = row * ServerCols + col;
        var ids      = _ownState.Value.BoardEchoInstanceIds;
        return boardIdx < ids.Length && ids[boardIdx] != -1;
    }
}
