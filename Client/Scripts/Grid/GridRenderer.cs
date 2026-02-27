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
/// Also handles:
///   - Target highlighting during intervention target selection (SetTargetSelectionMode).
///   - VFX overlays for active interventions (OnInterventionActivated via state manager).
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

    [ExportGroup("Star Colors")]
    [Export] public Color Star1Color = new(0.70f, 0.70f, 0.70f, 1.00f);
    [Export] public Color Star2Color = new(0.30f, 0.80f, 1.00f, 1.00f);
    [Export] public Color Star3Color = new(1.00f, 0.85f, 0.10f, 1.00f);

    [ExportGroup("VFX / Target Colors")]
    [Export] public Color TargetHighlight = new(1.00f, 1.00f, 0.20f, 0.30f);
    [Export] public Color VfxBarrier = new(1.00f, 0.85f, 0.10f, 0.70f);
    [Export] public Color VfxFocus = new(1.00f, 0.10f, 0.10f, 0.55f);
    [Export] public Color VfxAccelerate = new(0.10f, 1.00f, 0.30f, 0.40f);
    [Export] public Color VfxReposition = new(0.10f, 0.60f, 1.00f, 0.70f);
    [Export] public Color VfxRetreat = new(0.60f, 0.60f, 0.60f, 0.50f);
    [Export] public Color VfxFusion = new(1.00f, 1.00f, 1.00f, 0.90f);

    /// <summary>
    /// Emitted when the player left-clicks an ally cell during the Preparation phase.
    /// </summary>
    [Signal]
    public delegate void CellClickedEventHandler(int col, int row);

    /// <summary>
    /// Emitted when the player right-clicks an occupied ally cell during Preparation.
    /// Used to request moving the echo back to the bench.
    /// </summary>
    [Signal]
    public delegate void RemoveFromBoardRequestedEventHandler(int instanceId);

    /// <summary>
    /// Emitted when a combat cell is clicked during intervention target selection.
    /// Only fires when SetTargetSelectionMode is active and the clicked column matches
    /// the required target side (ally or enemy).
    /// </summary>
    [Signal]
    public delegate void CombatCellClickedEventHandler(int col, int row);

    private ClientStateManager? _stateManager;
    private PlayerState? _ownState;
    private GamePhase _currentPhase = GamePhase.WaitingForPlayers;
    private Vector2I _hovered = new(-1, -1);
    private Vector2I _dropZone = new(-1, -1);

    private bool _targetSelectionMode = false;
    private bool _targetAlly = false;
    private bool _targetEnemy = false;

    private readonly Dictionary<int, CombatUnitState> _combatUnits = [];
    private readonly Dictionary<int, float> _attackFlash = [];
    private readonly Dictionary<int, float> _damageFlash = [];
    // VFX: key = unitInstanceId (unit-specific) or -playerId (whole-team, e.g. Accelerate)
    private readonly Dictionary<int, (string Type, int PlayerId, float TimeLeft)> _vfx = [];
    private readonly Dictionary<int, float> _fusionFlash = []; // key = slotIndex (board), value = time left

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
        _stateManager.OnInterventionActivated += OnInterventionActivated;
        _stateManager.OnEchoFused += OnEchoFused;
    }

    public override void _ExitTree()
    {
        if (_stateManager == null) return;
        _stateManager.OnOwnStateChanged -= OnOwnStateChanged;
        _stateManager.OnPhaseChanged -= OnPhaseChanged;
        _stateManager.OnCombatSnapshot -= OnCombatSnapshot;
        _stateManager.OnInterventionActivated -= OnInterventionActivated;
        _stateManager.OnEchoFused -= OnEchoFused;
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

    /// <summary>
    /// Activates target-selection highlight mode over ally and/or enemy columns.
    /// When active, left-clicks emit CombatCellClicked instead of CellClicked.
    /// </summary>
    public void SetTargetSelectionMode(bool active, bool allyTarget, bool enemyTarget)
    {
        _targetSelectionMode = active;
        _targetAlly = allyTarget;
        _targetEnemy = enemyTarget;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        bool dirty = TickFlash(_attackFlash, (float)delta);
        dirty |= TickFlash(_damageFlash, (float)delta);
        dirty |= TickFlash(_fusionFlash, (float)delta);
        dirty |= TickVfx((float)delta);
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
        DrawTargetHighlights();
        DrawVfxEffects();
        DrawFusionFlash();
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
            var stars = _ownState.Value.BoardEchoStarLevels;
            for (int col = 0; col < AllyCols; col++)
                for (int row = 0; row < Rows; row++)
                {
                    int idx = row * AllyCols + col;
                    if (idx >= ids.Length || ids[idx] == -1) continue;
                    byte star = (idx < stars.Length) ? stars[idx] : (byte)1;
                    DrawEchoInCell(col, row, ids[idx], star, font, fs);
                }
        }

        // Enemy board — populated during Combat via CombatOpponentState
        var opp = _stateManager?.CombatOpponentState;
        if (opp.HasValue)
        {
            var ids = opp.Value.BoardEchoInstanceIds;
            var stars = opp.Value.BoardEchoStarLevels;
            for (int localCol = 0; localCol < AllyCols; localCol++)
                for (int row = 0; row < Rows; row++)
                {
                    int idx = row * AllyCols + localCol;
                    int displayCol = localCol + AllyCols;
                    if (idx >= ids.Length || ids[idx] == -1) continue;
                    byte star = (idx < stars.Length) ? stars[idx] : (byte)1;
                    DrawEchoInCell(displayCol, row, ids[idx], star, font, fs);
                }
        }
    }

    private void DrawEchoInCell(int col, int row, int instanceId, byte starLevel, Font font, int fontSize)
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

        int stars = Mathf.Clamp(starLevel, 1, 3);
        string starText = new string('\u2605', stars);
        Color starColor = stars switch { 3 => Star3Color, 2 => Star2Color, _ => Star1Color };
        DrawString(font, new Vector2(textX, rect.Position.Y + 46),
            starText, align, rect.Size.X - 8, fontSize, starColor);

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

    private void DrawTargetHighlights()
    {
        if (!_targetSelectionMode) return;
        for (int col = 0; col < TotalCols; col++)
            for (int row = 0; row < Rows; row++)
            {
                bool ally = col < AllyCols;
                if ((_targetAlly && ally) || (_targetEnemy && !ally))
                    DrawRect(CellRect(col, row), TargetHighlight);
            }
    }

    private void DrawVfxEffects()
    {
        if (_vfx.Count == 0) return;

        foreach (var (key, (type, _, _)) in _vfx)
        {
            Color overlay = VfxColor(type);

            if (key < 0)
            {
                // Whole-team (Accelerate): key = -playerId
                int pid = -key;
                bool isOwnTeam = pid == (_stateManager?.OwnState?.PlayerId ?? -1);
                for (int col = 0; col < TotalCols; col++)
                {
                    bool allyCol = col < AllyCols;
                    if ((isOwnTeam && allyCol) || (!isOwnTeam && !allyCol))
                        for (int row = 0; row < Rows; row++)
                            DrawRect(CellRect(col, row), overlay);
                }
            }
            else
            {
                // Unit-specific: Barrier, Reposition, Retreat, Focus target
                var cell = FindCellForUnit(key);
                if (cell.HasValue)
                    DrawRect(CellRect(cell.Value.Col, cell.Value.Row), overlay);
            }
        }
    }

    private void DrawFusionFlash()
    {
        foreach (var (slotIndex, timeLeft) in _fusionFlash)
        {
            int col = slotIndex % AllyCols;
            int row = slotIndex / AllyCols;
            float alpha = Mathf.Clamp(timeLeft / 1.5f, 0f, 1f);
            DrawRect(CellRect(col, row), new Color(VfxFusion, alpha));
        }
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

            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } rclick:
                HandleRightClick(rclick.Position);
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
        int col = Mathf.Clamp((int)(position.X / CellSize), 0, TotalCols - 1);
        int row = Mathf.Clamp((int)(position.Y / CellSize), 0, Rows - 1);

        // Intervention target selection takes priority during combat
        if (_currentPhase == GamePhase.Combat && _targetSelectionMode)
        {
            bool ally = col < AllyCols;
            if ((_targetAlly && ally) || (_targetEnemy && !ally))
                EmitSignal(SignalName.CombatCellClicked, col, row);
            return;
        }

        if (_currentPhase != GamePhase.Preparation) return;
        if (col >= AllyCols) return;

        EmitSignal(SignalName.CellClicked, col, row);
    }

    private void HandleRightClick(Vector2 position)
    {
        if (_currentPhase != GamePhase.Preparation) return;
        if (!_ownState.HasValue) return;

        int col = Mathf.Clamp((int)(position.X / CellSize), 0, TotalCols - 1);
        int row = Mathf.Clamp((int)(position.Y / CellSize), 0, Rows - 1);
        if (col >= AllyCols) return;

        int idx = row * AllyCols + col;
        var ids = _ownState.Value.BoardEchoInstanceIds;
        if (idx >= ids.Length || ids[idx] == -1) return;

        EmitSignal(SignalName.RemoveFromBoardRequested, ids[idx]);
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

    private void OnInterventionActivated(InterventionActivatedMessage msg)
    {
        float dur = msg.InterventionType switch
        {
            "Reposition" => 0.5f,
            "Focus" => 3.0f,
            "Barrier" => 2.0f,
            "Accelerate" => 4.0f,
            "TacticalRetreat" => 2.0f,
            _ => 1.0f,
        };

        // Whole-team effects (Accelerate) keyed as -playerId; unit-specific keyed as unitId
        int key = msg.InterventionType == "Accelerate" ? -msg.PlayerId : msg.TargetUnitId;
        _vfx[key] = (msg.InterventionType, msg.PlayerId, dur);
        QueueRedraw();
    }

    private void OnEchoFused(EchoFusedMessage msg)
    {
        if (!msg.IsOnBoard) return;
        _fusionFlash[msg.SlotIndex] = 1.5f;
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
            _vfx.Clear();
            _targetSelectionMode = false;
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

    private bool TickVfx(float delta)
    {
        if (_vfx.Count == 0) return false;
        foreach (var key in _vfx.Keys.ToList())
        {
            var (type, pid, left) = _vfx[key];
            left -= delta;
            if (left <= 0) _vfx.Remove(key);
            else _vfx[key] = (type, pid, left);
        }
        return true;
    }

    /// <summary>Returns the (Col, Row) of a unit on the combined board, or null if not found.</summary>
    private (int Col, int Row)? FindCellForUnit(int instanceId)
    {
        if (_ownState.HasValue)
        {
            var ids = _ownState.Value.BoardEchoInstanceIds;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == instanceId)
                    return (i % AllyCols, i / AllyCols);
        }

        var oIds = _stateManager?.CombatOpponentState?.BoardEchoInstanceIds;
        if (oIds != null)
            for (int i = 0; i < oIds.Length; i++)
                if (oIds[i] == instanceId)
                    return (i % AllyCols + AllyCols, i / AllyCols);

        return null;
    }

    private Color VfxColor(string type) => type switch
    {
        "Barrier" => VfxBarrier,
        "Focus" => VfxFocus,
        "Accelerate" => VfxAccelerate,
        "Reposition" => VfxReposition,
        "TacticalRetreat" => VfxRetreat,
        _ => new Color(1f, 1f, 1f, 0.3f),
    };

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
        _ => new Color(0.3f, 0.3f, 0.3f),
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
        _ => new Color(0.5f, 0.5f, 0.5f),
    };
}
