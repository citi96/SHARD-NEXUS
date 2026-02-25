#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using Shared.Data;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;

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

    [ExportGroup("Echo Colors - Class")]
    [Export] public Color ClassVanguard = new(0.25f, 0.45f, 0.90f, 1.00f);
    [Export] public Color ClassStriker = new(0.90f, 0.50f, 0.15f, 1.00f);
    [Export] public Color ClassRanger = new(0.20f, 0.75f, 0.35f, 1.00f);
    [Export] public Color ClassCaster = new(0.65f, 0.25f, 0.90f, 1.00f);
    [Export] public Color ClassSupport = new(0.90f, 0.80f, 0.15f, 1.00f);
    [Export] public Color ClassAssassin = new(0.70f, 0.10f, 0.20f, 1.00f);

    [ExportGroup("Echo Colors - Resonance")]
    [Export] public Color ResonanceFire = new(1.00f, 0.30f, 0.10f, 1.00f);
    [Export] public Color ResonanceFrost = new(0.30f, 0.80f, 1.00f, 1.00f);
    [Export] public Color ResonanceLightning = new(1.00f, 0.90f, 0.00f, 1.00f);
    [Export] public Color ResonanceEarth = new(0.60f, 0.40f, 0.10f, 1.00f);
    [Export] public Color ResonanceVoid = new(0.50f, 0.10f, 0.70f, 1.00f);
    [Export] public Color ResonanceLight = new(1.00f, 0.95f, 0.70f, 1.00f);
    [Export] public Color ResonanceShadow = new(0.25f, 0.20f, 0.35f, 1.00f);
    [Export] public Color ResonancePrism = new(0.80f, 0.80f, 0.80f, 1.00f);

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

    private readonly Dictionary<int, CombatUnitState> _combatUnits = [];
    private readonly Dictionary<int, float> _attackFlash = [];
    private readonly Dictionary<int, float> _damageFlash = [];

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
        _stateManager.OnCombatSnapshot += OnCombatSnapshot;
    }

    public override void _ExitTree()
    {
        if (_stateManager == null) return;
        _stateManager.OnOwnStateChanged -= OnOwnStateChanged;
        _stateManager.OnPhaseChanged -= OnPhaseChanged;
        _stateManager.OnCombatSnapshot -= OnCombatSnapshot;
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

    public override void _Process(double delta)
    {
        bool dirty = TickFlash(_attackFlash, (float)delta);
        dirty |= TickFlash(_damageFlash, (float)delta);
        if (dirty) QueueRedraw();
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
        bool isEnemy = col >= AllyCols;
        var align = isEnemy ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        var rect = CellRect(col, row);
        const int margin = 5;
        const int bodyHeight = 36;
        var innerRect = new Rect2(
            rect.Position + new Vector2(margin, margin),
            new Vector2(rect.Size.X - margin * 2, bodyHeight));

        // Class color body (placeholder sprite)
        DrawRect(innerRect, ClassColor(def?.Class));

        // Resonance border around body
        DrawRect(innerRect, ResonanceColor(def?.Resonance), false, 1.5f);

        // Name and stars
        float textX = isEnemy ? rect.End.X - 4 : rect.Position.X + 4;
        DrawString(font, new Vector2(textX, rect.Position.Y + margin + fontSize + 2),
            name, align, rect.Size.X - 8, fontSize, EchoNameColor);
        DrawString(font, new Vector2(textX, rect.Position.Y + 46),
            "\u2605", align, rect.Size.X - 8, fontSize, EchoNameColor);

        // HP / Mana fractions — prep: full HP, zero mana; combat: from snapshot
        float hpFraction = 1f;
        float mpFraction = 0f;

        if (_combatUnits.TryGetValue(instanceId, out var unit))
        {
            hpFraction = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 1f;
            mpFraction = unit.MaxMana > 0 ? (float)unit.Mana / unit.MaxMana : 0f;
        }

        DrawBars(rect, hpFraction, mpFraction);

        // Attack flash (white tint on attacker)
        if (_attackFlash.TryGetValue(instanceId, out float af) && af > 0)
            DrawRect(rect, new Color(1f, 1f, 1f, af / 0.4f * 0.5f));

        // Damage flash (red tint on target)
        if (_damageFlash.TryGetValue(instanceId, out float df) && df > 0)
            DrawRect(rect, new Color(1f, 0f, 0f, df / 0.4f * 0.6f));

        // Death overlay
        if (_combatUnits.TryGetValue(instanceId, out var dead) && !dead.Alive)
        {
            DrawRect(rect, new Color(0f, 0f, 0f, 0.6f));
            var grey = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            DrawLine(rect.Position, rect.End, grey, 1.5f);
            DrawLine(new Vector2(rect.End.X, rect.Position.Y),
                     new Vector2(rect.Position.X, rect.End.Y), grey, 1.5f);
        }
    }

    private void DrawBars(Rect2 cell, float hpFraction, float mpFraction)
    {
        const float hpY = 53f;
        const float mpY = 58f;
        const float hpH = 4f;
        const float mpH = 3f;
        const float margin = 3f;
        float barW = cell.Size.X - margin * 2;

        var hpBg = new Rect2(cell.Position.X + margin, cell.Position.Y + hpY, barW, hpH);
        DrawRect(hpBg, new Color(0f, 0.20f, 0f));
        DrawRect(new Rect2(hpBg.Position, new Vector2(barW * Mathf.Clamp(hpFraction, 0f, 1f), hpH)),
            HpColor(hpFraction));

        var mpBg = new Rect2(cell.Position.X + margin, cell.Position.Y + mpY, barW, mpH);
        DrawRect(mpBg, new Color(0f, 0f, 0.25f));
        if (mpFraction > 0f)
            DrawRect(new Rect2(mpBg.Position, new Vector2(barW * Mathf.Clamp(mpFraction, 0f, 1f), mpH)),
                new Color(0.10f, 0.40f, 0.90f));
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
        _dropZone = new Vector2I(-1, -1);
        QueueRedraw();
    }

    private void OnCombatSnapshot(CombatSnapshotPayload snapshot)
    {
        foreach (var unit in snapshot.Units)
            _combatUnits[unit.Id] = unit;

        foreach (var evt in snapshot.Events)
        {
            if (evt.Type != "attack") continue;
            _attackFlash[evt.Attacker] = 0.4f;
            _damageFlash[evt.Target] = 0.4f;
        }

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

        if (phase == GamePhase.Preparation)
        {
            _combatUnits.Clear();
            _attackFlash.Clear();
            _damageFlash.Clear();
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

    private static bool TickFlash(Dictionary<int, float> dict, float delta)
    {
        if (dict.Count == 0) return false;
        foreach (var key in dict.Keys.ToList())
        {
            dict[key] -= delta;
            if (dict[key] <= 0) dict.Remove(key);
        }
        return true;
    }

    private static Color HpColor(float fraction) =>
        fraction > 0.5f ? new Color(0.15f, 0.80f, 0.15f) :
        fraction > 0.25f ? new Color(0.90f, 0.75f, 0.10f) :
        new Color(0.90f, 0.15f, 0.10f);

    private Color ClassColor(EchoClass? cls) => cls switch
    {
        EchoClass.Vanguard => ClassVanguard,
        EchoClass.Striker => ClassStriker,
        EchoClass.Ranger => ClassRanger,
        EchoClass.Caster => ClassCaster,
        EchoClass.Support => ClassSupport,
        EchoClass.Assassin => ClassAssassin,
        _ => new Color(0.3f, 0.3f, 0.3f)
    };

    private Color ResonanceColor(Resonance? res) => res switch
    {
        Resonance.Fire => ResonanceFire,
        Resonance.Frost => ResonanceFrost,
        Resonance.Lightning => ResonanceLightning,
        Resonance.Earth => ResonanceEarth,
        Resonance.Void => ResonanceVoid,
        Resonance.Light => ResonanceLight,
        Resonance.Shadow => ResonanceShadow,
        Resonance.Prism => ResonancePrism,
        _ => new Color(0.5f, 0.5f, 0.5f)
    };
}
