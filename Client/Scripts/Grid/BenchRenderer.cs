#nullable enable
using Godot;
using Shared.Data;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Client.Scripts.Grid;

/// <summary>
/// Renders the 9-slot bench as a horizontal strip.
/// Each slot shows the Echo name and rarity color, or a grey placeholder if empty.
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

    /// <summary>
    /// Emitted when the player clicks an occupied bench slot during Preparation.
    /// </summary>
    [Signal]
    public delegate void EchoSelectedEventHandler(int instanceId, int slotIndex);

    private ClientStateManager? _stateManager;
    private PlayerState? _ownState;
    private GamePhase _currentPhase = GamePhase.WaitingForPlayers;
    private int _selectedSlot = -1;

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
    }

    public override void _ExitTree()
    {
        if (_stateManager == null) return;
        _stateManager.OnOwnStateChanged -= OnOwnStateChanged;
        _stateManager.OnPhaseChanged -= OnPhaseChanged;
    }

    /// <summary>Highlights the given slot with a white border. Pass -1 to clear.</summary>
    public void SetSelectedSlot(int slotIndex)
    {
        _selectedSlot = slotIndex;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_ownState.HasValue) return;

        var font = ThemeDB.FallbackFont;
        const int fs = 11;
        var ids = _ownState.Value.BenchEchoInstanceIds;

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

                string name = def?.Name ?? $"#{instanceId / 1000}";
                string stars = "\u2605";

                DrawString(font, rect.Position + new Vector2(4, fs + 4),  name,  HorizontalAlignment.Left, rect.Size.X - 4, fs, SlotNameColor);
                DrawString(font, rect.Position + new Vector2(4, fs * 2 + 6), stars, HorizontalAlignment.Left, rect.Size.X - 4, fs, SlotNameColor);
            }

            if (i == _selectedSlot)
                DrawRect(rect, SelectedBorder, false, 2f);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click) return;
        if (_currentPhase != GamePhase.Preparation) return;
        if (!_ownState.HasValue) return;

        int slotIndex = (int)(click.Position.X / (SlotSize + SlotGap));
        if (slotIndex < 0 || slotIndex >= BenchSlots) return;

        var ids = _ownState.Value.BenchEchoInstanceIds;
        if (slotIndex >= ids.Length || ids[slotIndex] == -1) return;

        EmitSignal(SignalName.EchoSelected, ids[slotIndex], slotIndex);
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
}
