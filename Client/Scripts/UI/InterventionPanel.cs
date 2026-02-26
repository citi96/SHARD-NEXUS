#nullable enable
using Godot;
using Shared.Models.Enums;
using Shared.Network.Messages;

namespace Client.Scripts.UI;

/// <summary>
/// Renders all five intervention buttons during Combat.
/// Uses _Draw() + _GuiInput() (same pattern as GridRenderer/BenchRenderer).
/// Visible only while combat is active; hidden during other phases.
///
/// Energy bar and per-button cooldowns are driven by server messages
/// (EnergyUpdate → OnEnergyChanged, InterventionActivated → OnInterventionActivated).
/// Emits InterventionRequested(typeIndex) so GameBoardController can handle
/// target selection before calling SendUseIntervention.
///
/// Initialization: _Ready() auto-wires to the parent GameClient node.
/// </summary>
public partial class InterventionPanel : Control
{
    public const int ButtonW   = 80;
    public const int ButtonH   = 48;
    public const int ButtonGap = 6;

    [ExportGroup("Colors")]
    [Export] public Color ButtonBg       = new(0.25f, 0.35f, 0.55f, 1.00f);
    [Export] public Color ButtonBorder   = new(0.50f, 0.65f, 0.90f, 1.00f);
    [Export] public Color ButtonText     = new(1.00f, 1.00f, 1.00f, 1.00f);
    [Export] public Color CostText       = new(1.00f, 0.85f, 0.20f, 1.00f);
    [Export] public Color CooldownBg     = new(0.20f, 0.20f, 0.20f, 1.00f);
    [Export] public Color CooldownBorder = new(0.35f, 0.35f, 0.35f, 1.00f);
    [Export] public Color CooldownText   = new(0.50f, 0.50f, 0.50f, 1.00f);
    [Export] public Color CooldownTimer  = new(1.00f, 0.80f, 0.10f, 1.00f);
    [Export] public Color StatusColor    = new(1.00f, 0.30f, 0.30f, 1.00f);
    [Export] public Color EnergyBarFg    = new(0.20f, 0.80f, 1.00f, 1.00f);
    [Export] public Color EnergyBarBg    = new(0.05f, 0.10f, 0.20f, 1.00f);
    [Export] public Color PendingBorder  = new(1.00f, 1.00f, 0.20f, 1.00f);
    [Export] public Color DisabledBg     = new(0.12f, 0.12f, 0.12f, 1.00f);

    // Mirrors InterventionSettings order: Reposition, Focus, Barrier, Accelerate, TacticalRetreat
    public static readonly InterventionType[] ButtonTypes =
    {
        InterventionType.Reposition,
        InterventionType.Focus,
        InterventionType.Barrier,
        InterventionType.Accelerate,
        InterventionType.TacticalRetreat,
    };

    private static readonly string[] ButtonLabels =
        { "Riposiziona", "Focus", "Barriera", "Accelera", "Ritiro" };

    // Mirrors InterventionSettings.EnergyCosts
    private static readonly int[] EnergyCosts = { 3, 5, 4, 6, 8 };

    // Mirrors InterventionSettings.CooldownSeconds
    private static readonly float[] CooldownSeconds = { 8f, 12f, 15f, 20f, 25f };

    private GameClient?          _gameClient;
    private ClientStateManager?  _sm;
    private int                  _energy       = 0;
    private int                  _maxEnergy    = 15;
    private int                  _pendingIndex = -1;
    private readonly float[]     _cooldowns    = new float[5];
    private float                _statusTimer  = 0f;
    private string               _statusText   = string.Empty;

    [Signal] public delegate void InterventionRequestedEventHandler(int typeIndex);

    public override void _Ready()
    {
        int count  = ButtonTypes.Length;
        float totalW = count * ButtonW + (count - 1) * ButtonGap;
        float totalH = ButtonH + 8 + 16;
        CustomMinimumSize = new Vector2(totalW, totalH);
        MouseFilter = MouseFilterEnum.Stop;

        _gameClient = GetParent<GameClient>();
        _sm = _gameClient.StateManager;

        _sm.OnCombatStarted         += (_, _) => { Visible = true; QueueRedraw(); };
        _sm.OnCombatEnded           += OnCombatEnded;
        _sm.OnActionRejected        += OnActionRejected;
        _sm.OnEnergyChanged         += OnEnergyChanged;
        _sm.OnInterventionActivated += OnInterventionActivated;

        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_sm == null) return;
        _sm.OnCombatEnded           -= OnCombatEnded;
        _sm.OnActionRejected        -= OnActionRejected;
        _sm.OnEnergyChanged         -= OnEnergyChanged;
        _sm.OnInterventionActivated -= OnInterventionActivated;
    }

    public override void _Process(double delta)
    {
        bool dirty = false;

        for (int i = 0; i < _cooldowns.Length; i++)
        {
            if (_cooldowns[i] > 0)
            {
                _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - (float)delta);
                dirty = true;
            }
        }

        if (_statusTimer > 0)
        {
            _statusTimer -= (float)delta;
            if (_statusTimer <= 0) _statusText = string.Empty;
            dirty = true;
        }

        if (dirty) QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        const int fs = 10;

        // Energy bar (drawn above buttons, barY is negative = above local origin)
        const float barH = 8f;
        const float barY = -barH - 4f;
        float totalW = CustomMinimumSize.X;
        float frac = _maxEnergy > 0 ? Mathf.Clamp(_energy / (float)_maxEnergy, 0f, 1f) : 0f;
        DrawRect(new Rect2(0, barY, totalW, barH), EnergyBarBg);
        if (frac > 0)
            DrawRect(new Rect2(0, barY, totalW * frac, barH), EnergyBarFg);
        DrawString(font, new Vector2(0, barY - 2),
            $"E: {_energy}/{_maxEnergy}", HorizontalAlignment.Left, -1, fs, ButtonText);

        // Buttons
        for (int i = 0; i < ButtonTypes.Length; i++)
        {
            bool onCooldown = _cooldowns[i] > 0;
            bool canAfford  = _energy >= EnergyCosts[i];
            bool usable     = !onCooldown && canAfford;
            bool pending    = _pendingIndex == i;

            var rect = ButtonRect(i);

            // Background
            DrawRect(rect, usable ? ButtonBg : DisabledBg);

            // Border — thick yellow if pending, normal/dimmed otherwise
            if (pending)
                DrawRect(rect, PendingBorder, false, 2f);
            else
                DrawRect(rect, usable ? ButtonBorder : CooldownBorder, false, 1f);

            Color labelC = usable || pending ? ButtonText : CooldownText;

            // Label
            DrawString(font, rect.Position + new Vector2(4, fs + 4),
                ButtonLabels[i], HorizontalAlignment.Left, ButtonW - 8, fs, labelC);

            // Cost
            DrawString(font, rect.Position + new Vector2(4, fs * 2 + 8),
                $"{EnergyCosts[i]}E", HorizontalAlignment.Left, ButtonW - 8, fs,
                usable ? CostText : CooldownText);

            // Bottom row: pending hint OR cooldown countdown
            if (pending)
                DrawString(font, rect.Position + new Vector2(4, ButtonH - 6),
                    "Target...", HorizontalAlignment.Left, ButtonW - 8, fs, PendingBorder);
            else if (onCooldown)
                DrawString(font, rect.Position + new Vector2(4, ButtonH - 6),
                    $"{_cooldowns[i]:F1}s", HorizontalAlignment.Left, ButtonW - 8, fs, CooldownTimer);
        }

        // Status text (rejection feedback)
        if (_statusText.Length > 0 && _statusTimer > 0)
        {
            float alpha = Mathf.Clamp(_statusTimer / 3f, 0f, 1f);
            DrawString(font, new Vector2(0, ButtonH + 14),
                _statusText, HorizontalAlignment.Left, CustomMinimumSize.X, fs,
                new Color(StatusColor, alpha));
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } btn) return;

        for (int i = 0; i < ButtonTypes.Length; i++)
        {
            if (!ButtonRect(i).HasPoint(btn.Position)) continue;

            bool canUse = _cooldowns[i] <= 0 && _energy >= EnergyCosts[i];
            if (!canUse) return;

            _pendingIndex = i;
            EmitSignal(SignalName.InterventionRequested, i);
            QueueRedraw();
            return;
        }
    }

    /// <summary>
    /// Called by GameBoardController to cancel the pending target selection
    /// (e.g. after sending the intervention or when phase changes).
    /// </summary>
    public void SetPendingMode(bool active, int buttonIndex)
    {
        _pendingIndex = active ? buttonIndex : -1;
        QueueRedraw();
    }

    // ── Private handlers ──────────────────────────────────────────────────────

    private void OnCombatEnded(int _, int __)
    {
        Visible       = false;
        _pendingIndex = -1;
        Array.Clear(_cooldowns, 0, _cooldowns.Length);
        QueueRedraw();
    }

    private void OnEnergyChanged(int energy, int maxEnergy)
    {
        _energy    = energy;
        _maxEnergy = maxEnergy;
        QueueRedraw();
    }

    private void OnInterventionActivated(InterventionActivatedMessage msg)
    {
        // Set cooldown for own interventions only
        int ownId = _sm?.OwnState?.PlayerId ?? -1;
        if (msg.PlayerId != ownId) return;

        for (int i = 0; i < ButtonTypes.Length; i++)
        {
            if (ButtonTypes[i].ToString() != msg.InterventionType) continue;
            _cooldowns[i] = CooldownSeconds[i];
            if (_pendingIndex == i) _pendingIndex = -1;
            QueueRedraw();
            return;
        }
    }

    private void OnActionRejected(ActionRejectedMessage msg)
    {
        if (msg.Action != "UseIntervention") return;
        _statusText   = msg.Reason;
        _statusTimer  = 3f;
        _pendingIndex = -1;
        QueueRedraw();
    }

    private static Rect2 ButtonRect(int i) =>
        new(i * (ButtonW + ButtonGap), 0, ButtonW, ButtonH);
}
