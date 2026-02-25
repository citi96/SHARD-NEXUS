#nullable enable
using Godot;
using Shared.Models.Enums;
using Shared.Network.Messages;

namespace Client.Scripts.UI;

/// <summary>
/// Renders all five intervention buttons during Combat.
/// Uses _Draw() + _GuiInput() (same pattern as GridRenderer/BenchRenderer).
/// Visible only while combat is active; hidden during Preparation and other phases.
///
/// Each button shows: label, cost ("—" — no cost system yet), cooldown countdown.
/// Sends UseIntervention to the server — currently always rejected with
/// "Sistema interventi non ancora disponibile".
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

    private static readonly InterventionType[] ButtonTypes =
    {
        InterventionType.Reposition,
        InterventionType.Focus,
        InterventionType.Barrier,
        InterventionType.Accelerate,
        InterventionType.TacticalRetreat,
    };

    private static readonly string[] ButtonLabels =
        { "Riposiziona", "Focus", "Barriera", "Accelera", "Ritiro" };

    private GameClient? _gameClient;
    private ClientStateManager? _sm;
    private float _cooldownTimer = 0f;
    private float _statusTimer = 0f;
    private string _statusText = string.Empty;

    public override void _Ready()
    {
        int count = ButtonTypes.Length;
        float totalW = count * ButtonW + (count - 1) * ButtonGap;
        float totalH = ButtonH + 8 + 16;
        CustomMinimumSize = new Vector2(totalW, totalH);
        MouseFilter = MouseFilterEnum.Stop;

        _gameClient = GetParent<GameClient>();
        _sm = _gameClient.StateManager;

        _sm.OnCombatStarted  += (_, _) => { Visible = true; QueueRedraw(); };
        _sm.OnCombatEnded    += (_, _) => Visible = false;
        _sm.OnActionRejected += OnActionRejected;

        Visible = false;
    }

    public override void _Process(double delta)
    {
        bool dirty = false;

        if (_cooldownTimer > 0)
        {
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - (float)delta);
            dirty = true;
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
        bool onCooldown = _cooldownTimer > 0;

        for (int i = 0; i < ButtonTypes.Length; i++)
        {
            var rect = ButtonRect(i);
            DrawRect(rect, onCooldown ? CooldownBg : ButtonBg);
            DrawRect(rect, onCooldown ? CooldownBorder : ButtonBorder, false, 1f);

            // Label (top area of button)
            DrawString(font, rect.Position + new Vector2(4, fs + 4),
                ButtonLabels[i], HorizontalAlignment.Left, ButtonW - 4, fs,
                onCooldown ? CooldownText : ButtonText);

            // Cost row
            DrawString(font, rect.Position + new Vector2(4, fs * 2 + 8),
                "(—)", HorizontalAlignment.Left, ButtonW - 4, fs,
                onCooldown ? CooldownText : CostText);

            // Cooldown countdown (bottom of button)
            if (onCooldown)
                DrawString(font, rect.Position + new Vector2(4, ButtonH - 4),
                    $"{_cooldownTimer:F1}s", HorizontalAlignment.Left, ButtonW - 4, fs,
                    CooldownTimer);
        }

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
        if (_cooldownTimer > 0) return;

        for (int i = 0; i < ButtonTypes.Length; i++)
        {
            if (!ButtonRect(i).HasPoint(btn.Position)) continue;
            _gameClient?.SendUseIntervention(ButtonTypes[i]);
            _cooldownTimer = 1f;
            QueueRedraw();
            return;
        }
    }

    private void OnActionRejected(ActionRejectedMessage msg)
    {
        if (msg.Action == "UseIntervention")
        {
            _statusText  = msg.Reason;
            _statusTimer = 3f;
            QueueRedraw();
        }
    }

    private static Rect2 ButtonRect(int i) =>
        new(i * (ButtonW + ButtonGap), 0, ButtonW, ButtonH);
}
