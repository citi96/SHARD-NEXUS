#nullable enable
using Godot;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Client.Scripts.UI;

/// <summary>
/// Displays active resonance bonuses for the local player.
/// Shows one row per active resonance: tier dots, name, and count/next threshold.
/// Visible during Preparation, Combat, and Reward phases.
///
/// Initialization: _Ready() auto-wires to the parent GameClient node.
/// </summary>
public partial class ResonancePanel : Control
{
    public const int RowHeight = 20;
    public const int MaxRows = 8;
    public const int PanelWidth = 160;
    public const int DotSize = 6;
    public const int DotGap = 3;
    public const int MaxTiers = 3;

    [ExportGroup("Colors")]
    [Export] public Color Bg = new(0.06f, 0.06f, 0.10f, 0.85f);
    [Export] public Color TextColor = new(0.85f, 0.85f, 0.85f, 1.00f);
    [Export] public Color DotEmpty = new(0.25f, 0.25f, 0.30f, 1.00f);

    private static readonly int[] Thresholds = { 2, 4, 6 };

    private GameClient? _gameClient;
    private ClientStateManager? _sm;
    private ResonanceBonus[] _resonances = [];

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(PanelWidth, MaxRows * RowHeight + 8);
        MouseFilter = MouseFilterEnum.Ignore;

        _gameClient = GetParent<GameClient>();
        _sm = _gameClient.StateManager;

        _sm.OnOwnStateChanged += OnOwnStateChanged;
        _sm.OnPhaseChanged += OnPhaseChanged;
        _sm.OnRoundStarted += OnRoundStarted;

        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_sm == null) return;
        _sm.OnOwnStateChanged -= OnOwnStateChanged;
        _sm.OnPhaseChanged -= OnPhaseChanged;
        _sm.OnRoundStarted -= OnRoundStarted;
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        const int fs = 10;

        if (_resonances.Length == 0) return;

        float totalH = _resonances.Length * RowHeight + 8;
        DrawRect(new Rect2(0, 0, PanelWidth, totalH), Bg);

        for (int i = 0; i < _resonances.Length; i++)
        {
            var r = _resonances[i];
            float y = 4 + i * RowHeight;
            Color resColor = GetResonanceColor(r.ResonanceType);

            // Tier dots
            float dotX = 6;
            for (int t = 0; t < MaxTiers; t++)
            {
                Color dotColor = t < r.Tier ? resColor : DotEmpty;
                DrawCircle(new Vector2(dotX + DotSize / 2f, y + RowHeight / 2f), DotSize / 2f, dotColor);
                dotX += DotSize + DotGap;
            }

            // Resonance name
            float textX = dotX + 4;
            DrawString(font, new Vector2(textX, y + fs + 3),
                r.ResonanceType, HorizontalAlignment.Left, -1, fs, resColor);

            // Count / next threshold
            int nextThreshold = GetNextThreshold(r.Count);
            string countText = nextThreshold > 0 ? $"{r.Count}/{nextThreshold}" : $"{r.Count}";
            DrawString(font, new Vector2(PanelWidth - 30, y + fs + 3),
                countText, HorizontalAlignment.Right, 26, fs, TextColor);
        }
    }

    private void OnOwnStateChanged(PlayerState state)
    {
        _resonances = state.ActiveResonances ?? [];
        QueueRedraw();
    }

    private void OnPhaseChanged(GamePhase phase, float _duration)
    {
        Visible = phase is GamePhase.Preparation or GamePhase.Combat or GamePhase.Reward;
        QueueRedraw();
    }

    private void OnRoundStarted(int _round)
    {
        Visible = true;
        QueueRedraw();
    }

    private static int GetNextThreshold(int count)
    {
        foreach (int t in Thresholds)
        {
            if (count < t) return t;
        }
        return 0; // all thresholds reached
    }

    private static Color GetResonanceColor(string resonanceType) => resonanceType switch
    {
        nameof(Resonance.Fire) => new Color(1.00f, 0.30f, 0.10f, 1.00f),
        nameof(Resonance.Frost) => new Color(0.30f, 0.80f, 1.00f, 1.00f),
        nameof(Resonance.Lightning) => new Color(1.00f, 0.90f, 0.00f, 1.00f),
        nameof(Resonance.Earth) => new Color(0.60f, 0.40f, 0.10f, 1.00f),
        nameof(Resonance.Void) => new Color(0.50f, 0.10f, 0.70f, 1.00f),
        nameof(Resonance.Light) => new Color(1.00f, 0.95f, 0.70f, 1.00f),
        nameof(Resonance.Shadow) => new Color(0.25f, 0.20f, 0.35f, 1.00f),
        nameof(Resonance.Prism) => new Color(0.80f, 0.80f, 0.80f, 1.00f),
        _ => new Color(0.50f, 0.50f, 0.50f, 1.00f),
    };
}
