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
/// Renders the 9-slot bench as a horizontal strip.
/// Each slot shows a class-colored body, resonance border, Echo name, stars, and HP/Mana bars.
/// The selected slot is outlined in white.
///
/// Emits EchoSelected(instanceId, slotIndex) when the player clicks an occupied slot
/// during the Preparation phase.
///
/// Initialization: call Initialize(ClientStateManager) from GameBoardController._Ready().
/// </summary>
public partial class BenchRenderer : Control
{
    public const int BenchSlots = 9;
    public const int SlotSize = 64;
    public const int SlotGap = 4;

    [ExportGroup("Colors")]
    [Export] public Color EmptySlot = new(0.15f, 0.15f, 0.15f, 0.60f);
    [Export] public Color SelectedBorder = new(1.00f, 1.00f, 1.00f, 1.00f);
    [Export] public Color SlotNameColor = new(1.00f, 1.00f, 1.00f, 1.00f);
    [Export] public Color CommonColor = new(0.55f, 0.55f, 0.55f, 0.70f);
    [Export] public Color UncommonColor = new(0.20f, 0.80f, 0.20f, 0.70f);
    [Export] public Color RareColor = new(0.10f, 0.40f, 0.90f, 0.70f);
    [Export] public Color EpicColor = new(0.60f, 0.10f, 0.90f, 0.70f);
    [Export] public Color LegendaryColor = new(1.00f, 0.70f, 0.00f, 0.70f);

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
    /// Emitted when the player clicks an occupied bench slot during Preparation.
    /// </summary>
    [Signal]
    public delegate void EchoSelectedEventHandler(int instanceId, int slotIndex);

    /// <summary>
    /// Emitted when the player right-clicks an occupied bench slot during Preparation.
    /// </summary>
    [Signal]
    public delegate void SellRequestedEventHandler(int instanceId);

    [ExportGroup("Star Colors")]
    [Export] public Color Star1Color = new(0.70f, 0.70f, 0.70f, 1.00f);
    [Export] public Color Star2Color = new(0.30f, 0.80f, 1.00f, 1.00f);
    [Export] public Color Star3Color = new(1.00f, 0.85f, 0.10f, 1.00f);
    [Export] public Color VfxFusion = new(1.00f, 1.00f, 1.00f, 0.90f);

    private ClientStateManager? _stateManager;
    private PlayerState? _ownState;
    private GamePhase _currentPhase = GamePhase.WaitingForPlayers;
    private int _selectedSlot = -1;
    private readonly Dictionary<int, float> _fusionFlash = [];

    public override void _Ready()
    {
        int totalWidth = BenchSlots * SlotSize + (BenchSlots - 1) * SlotGap;
        CustomMinimumSize = new Vector2(totalWidth, SlotSize);
        MouseFilter = MouseFilterEnum.Stop;
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
        _stateManager.OnEchoFused += OnEchoFused;
    }

    public override void _ExitTree()
    {
        if (_stateManager == null) return;
        _stateManager.OnOwnStateChanged -= OnOwnStateChanged;
        _stateManager.OnEchoFused -= OnEchoFused;
        _stateManager.OnPhaseChanged -= OnPhaseChanged;
    }

    /// <summary>Highlights the given slot with a white border. Pass -1 to clear.</summary>
    public void SetSelectedSlot(int slotIndex)
    {
        _selectedSlot = slotIndex;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_fusionFlash.Count == 0) return;
        foreach (var key in _fusionFlash.Keys.ToList())
        {
            _fusionFlash[key] -= (float)delta;
            if (_fusionFlash[key] <= 0) _fusionFlash.Remove(key);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_ownState.HasValue) return;

        var font = ThemeDB.FallbackFont;
        const int fs = 11;
        var ids = _ownState.Value.BenchEchoInstanceIds;
        var starLevels = _ownState.Value.BenchEchoStarLevels;

        for (int i = 0; i < BenchSlots; i++)
        {
            var rect = SlotRect(i);
            int instanceId = (i < ids.Length) ? ids[i] : -1;

            if (instanceId == -1)
            {
                DrawRect(rect, EmptySlot);
            }
            else
            {
                var def = EchoCatalog.GetByInstanceId(instanceId);
                DrawRect(rect, RarityColor(def?.Rarity));

                // Class body (inner rect with margin)
                const int margin = 4;
                const int bodyHeight = 36;
                var innerRect = new Rect2(
                    rect.Position + new Vector2(margin, margin),
                    new Vector2(rect.Size.X - margin * 2, bodyHeight));
                DrawRect(innerRect, new Color(ClassColor(def?.Class), 0.70f));

                // Resonance border around body
                DrawRect(innerRect, ResonanceColor(def?.Resonance), false, 1.5f);

                // Name + stars
                string name = def?.Name ?? $"#{instanceId / 1000}";
                DrawString(font, rect.Position + new Vector2(4, fs + 4),
                    name, HorizontalAlignment.Left, rect.Size.X - 4, fs, SlotNameColor);

                int stars = Mathf.Clamp((i < starLevels.Length) ? starLevels[i] : 1, 1, 3);
                string starText = new string('\u2605', stars);
                Color starColor = stars switch { 3 => Star3Color, 2 => Star2Color, _ => Star1Color };
                DrawString(font, rect.Position + new Vector2(4, 46),
                    starText, HorizontalAlignment.Left, rect.Size.X - 4, fs, starColor);

                // HP bar (always full — bench echoes don't take damage)
                DrawBars(rect);

                // Fusion flash overlay
                if (_fusionFlash.TryGetValue(i, out float timeLeft))
                {
                    float alpha = Mathf.Clamp(timeLeft / 1.5f, 0f, 1f);
                    DrawRect(rect, new Color(VfxFusion, alpha));
                }
            }

            if (i == _selectedSlot)
                DrawRect(rect, SelectedBorder, false, 2f);
        }
    }

    private void DrawBars(Rect2 slot)
    {
        const float hpY = 53f;
        const float mpY = 58f;
        const float hpH = 4f;
        const float mpH = 3f;
        const float margin = 3f;
        float barW = slot.Size.X - margin * 2;

        var hpBg = new Rect2(slot.Position.X + margin, slot.Position.Y + hpY, barW, hpH);
        DrawRect(hpBg, new Color(0f, 0.20f, 0f));
        DrawRect(new Rect2(hpBg.Position, new Vector2(barW, hpH)), new Color(0.15f, 0.80f, 0.15f));

        var mpBg = new Rect2(slot.Position.X + margin, slot.Position.Y + mpY, barW, mpH);
        DrawRect(mpBg, new Color(0f, 0f, 0.25f));
        // Mana bar intentionally empty: echoes start at 0 mana
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } btn) return;
        if (_currentPhase != GamePhase.Preparation) return;
        if (!_ownState.HasValue) return;

        int slotIndex = (int)(btn.Position.X / (SlotSize + SlotGap));
        if (slotIndex < 0 || slotIndex >= BenchSlots) return;

        var ids = _ownState.Value.BenchEchoInstanceIds;
        if (slotIndex >= ids.Length || ids[slotIndex] == -1) return;

        if (btn.ButtonIndex == MouseButton.Left)
            EmitSignal(SignalName.EchoSelected, ids[slotIndex], slotIndex);
        else if (btn.ButtonIndex == MouseButton.Right)
            EmitSignal(SignalName.SellRequested, ids[slotIndex]);
    }

    private void OnEchoFused(EchoFusedMessage msg)
    {
        if (msg.IsOnBoard) return;
        _fusionFlash[msg.SlotIndex] = 1.5f;
        QueueRedraw();
    }

    private void OnOwnStateChanged(PlayerState state)
    {
        _ownState = state;
        // Clear selection if the previously selected slot is now empty (unit was placed)
        if (_selectedSlot >= 0)
        {
            var ids = state.BenchEchoInstanceIds;
            if (_selectedSlot >= ids.Length || ids[_selectedSlot] == -1)
                _selectedSlot = -1;
        }
        QueueRedraw();
    }

    /// <summary>
    /// Sets the active game phase. Mirrors the logic on GridRenderer.SetGamePhase —
    /// called by GameBoardController for server-driven transitions that don't go through
    /// an explicit PhaseChanged message (e.g. OnRoundStarted → Preparation).
    /// </summary>
    public void SetGamePhase(GamePhase phase)
    {
        _currentPhase = phase;
        if (phase != GamePhase.Preparation)
            _selectedSlot = -1;
        QueueRedraw();
    }

    private void OnPhaseChanged(GamePhase phase, float _duration) => SetGamePhase(phase);

    private static Rect2 SlotRect(int index) =>
        new(index * (SlotSize + SlotGap), 0, SlotSize, SlotSize);

    private Color RarityColor(Rarity? rarity) => rarity switch
    {
        Rarity.Common => CommonColor,
        Rarity.Uncommon => UncommonColor,
        Rarity.Rare => RareColor,
        Rarity.Epic => EpicColor,
        Rarity.Legendary => LegendaryColor,
        _ => EmptySlot
    };

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
