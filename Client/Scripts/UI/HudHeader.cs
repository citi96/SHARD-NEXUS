#nullable enable
using Godot;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Client.Scripts.UI;

/// <summary>
/// Always-visible header bar showing: Round, Phase, Phase timer, Nexus HP bar,
/// Gold, and Level + XP bar.
///
/// Timer is seeded by OnPhaseChanged.PhaseDurationSecs and counts down in _Process().
/// XP thresholds mirror PlayerSettings.XpToLevel (static, server config).
/// HP max = 100 (PlayerSettings.StartingHP).
///
/// Initialization: _Ready() auto-wires to the parent GameClient node.
/// </summary>
public partial class HudHeader : Control
{
    public const int Height  = 52;
    public const int HpMax   = 100;
    public const int BarW    = 80;
    public const int BarH    = 4;

    [ExportGroup("Colors")]
    [Export] public Color Bg        = new(0.08f, 0.08f, 0.08f, 0.92f);
    [Export] public Color TextColor = new(1.00f, 1.00f, 1.00f, 1.00f);
    [Export] public Color GoldColor = new(1.00f, 0.85f, 0.20f, 1.00f);
    [Export] public Color HpHigh    = new(0.15f, 0.80f, 0.15f, 1.00f);
    [Export] public Color HpMid     = new(0.90f, 0.75f, 0.10f, 1.00f);
    [Export] public Color HpLow     = new(0.90f, 0.15f, 0.10f, 1.00f);
    [Export] public Color BarBg     = new(0.15f, 0.15f, 0.15f, 1.00f);
    [Export] public Color XpBarFg   = new(0.30f, 0.60f, 1.00f, 1.00f);
    [Export] public Color TimerWarn = new(1.00f, 0.40f, 0.10f, 1.00f);

    private GameClient? _gameClient;
    private ClientStateManager? _sm;
    private int _round = 0;
    private GamePhase _phase = GamePhase.WaitingForPlayers;
    private float _timerLeft = 0f;
    private int _nexusHp = HpMax;
    private int _gold = 0;
    private int _level = 1;
    private int _xp = 0;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(1280, Height);

        _gameClient = GetParent<GameClient>();
        _sm = _gameClient.StateManager;

        _sm.OnOwnStateChanged += OnOwnStateChanged;
        _sm.OnPhaseChanged    += OnPhaseChanged;
        _sm.OnRoundStarted    += OnRoundStarted;
        _sm.OnGameEnded       += _ => { _phase = GamePhase.GameOver; _timerLeft = 0; QueueRedraw(); };

        // Seed from current state if match already in progress
        if (_sm.OwnState.HasValue)
        {
            var s = _sm.OwnState.Value;
            _nexusHp = s.NexusHealth;
            _gold    = s.Gold;
            _level   = s.Level;
            _xp      = s.Xp;
        }
        _round = _sm.CurrentRound;
        _phase = _sm.CurrentPhase;

        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_sm == null) return;
        _sm.OnOwnStateChanged -= OnOwnStateChanged;
        _sm.OnPhaseChanged    -= OnPhaseChanged;
        _sm.OnRoundStarted    -= OnRoundStarted;
    }

    public override void _Process(double delta)
    {
        if (_timerLeft <= 0) return;
        _timerLeft -= (float)delta;
        if (_timerLeft < 0) _timerLeft = 0;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        const int fs   = 12;
        const float ty = Height / 2f + fs / 2f;   // text baseline, vertically centered
        const float by = Height / 2f + 6f;         // bar top (below text center)

        // Background
        DrawRect(new Rect2(0, 0, CustomMinimumSize.X, Height), Bg);

        // Round
        DrawString(font, new Vector2(12, ty),
            $"Round {_round}", HorizontalAlignment.Left, -1, fs, TextColor);

        DrawString(font, new Vector2(110, ty), "|", HorizontalAlignment.Left, -1, fs, TextColor);

        // Phase
        DrawString(font, new Vector2(122, ty),
            PhaseName(_phase), HorizontalAlignment.Left, -1, fs, TextColor);

        // Timer
        bool timerWarn = _timerLeft > 0 && _timerLeft < 10f;
        DrawString(font, new Vector2(250, ty),
            _timerLeft > 0 ? $"{_timerLeft:F0}s" : "--",
            HorizontalAlignment.Left, -1, fs,
            timerWarn ? TimerWarn : TextColor);

        DrawString(font, new Vector2(320, ty), "|", HorizontalAlignment.Left, -1, fs, TextColor);

        // Nexus HP
        float hpFrac = Mathf.Clamp((float)_nexusHp / HpMax, 0f, 1f);
        DrawString(font, new Vector2(332, ty),
            $"HP: {_nexusHp}", HorizontalAlignment.Left, -1, fs, TextColor);
        DrawRect(new Rect2(332, by, BarW, BarH), BarBg);
        DrawRect(new Rect2(332, by, BarW * hpFrac, BarH), HpColor(hpFrac));

        DrawString(font, new Vector2(460, ty), "|", HorizontalAlignment.Left, -1, fs, TextColor);

        // Gold
        DrawString(font, new Vector2(472, ty),
            $"G: {_gold}", HorizontalAlignment.Left, -1, fs, GoldColor);

        DrawString(font, new Vector2(560, ty), "|", HorizontalAlignment.Left, -1, fs, TextColor);

        // Level + XP bar
        int xpMax = XpForNextLevel(_level);
        float xpFrac = xpMax > 0 ? Mathf.Clamp((float)_xp / xpMax, 0f, 1f) : 1f;
        DrawString(font, new Vector2(572, ty),
            $"Lv{_level} ({_xp}/{xpMax}XP)", HorizontalAlignment.Left, -1, fs, TextColor);
        DrawRect(new Rect2(572, by, BarW, BarH), BarBg);
        DrawRect(new Rect2(572, by, BarW * xpFrac, BarH), XpBarFg);
    }

    private void OnOwnStateChanged(PlayerState state)
    {
        _nexusHp = state.NexusHealth;
        _gold    = state.Gold;
        _level   = state.Level;
        _xp      = state.Xp;
        QueueRedraw();
    }

    private void OnPhaseChanged(GamePhase phase, float duration)
    {
        _phase     = phase;
        _timerLeft = duration;
        QueueRedraw();
    }

    private void OnRoundStarted(int round)
    {
        _round   = round;
        Visible  = true;
        QueueRedraw();
    }

    private Color HpColor(float frac) =>
        frac > 0.5f ? HpHigh :
        frac > 0.25f ? HpMid :
        HpLow;

    private static string PhaseName(GamePhase phase) => phase switch
    {
        GamePhase.WaitingForPlayers => "Attesa",
        GamePhase.Preparation       => "Preparazione",
        GamePhase.Combat            => "Combattimento",
        GamePhase.Reward            => "Ricompensa",
        GamePhase.MutationChoice    => "Mutazione",
        GamePhase.GameOver          => "Fine Partita",
        _                           => phase.ToString()
    };

    // Mirrors PlayerSettings.XpToLevel: key = current level, value = XP needed to level up.
    private static int XpForNextLevel(int level) => level switch
    {
        1 => 2,
        2 => 6,
        3 => 10,
        4 => 20,
        5 => 36,
        6 => 48,
        7 => 72,
        8 => 84,
        _ => 0   // level 9 = max
    };
}
