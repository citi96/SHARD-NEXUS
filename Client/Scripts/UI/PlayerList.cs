#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Client.Scripts.UI;

/// <summary>
/// Always-visible panel listing all players (own row first, then opponents sorted by PlayerId).
/// Highlights the current combat opponent in orange.
/// Greys out eliminated players (NexusHealth &lt;= 0).
///
/// Shows per row: label ("Me" / "P{id}"), level, HP bar, HP number.
///
/// Initialization: _Ready() auto-wires to the parent GameClient node.
/// </summary>
public partial class PlayerList : Control
{
    public const int RowH   = 28;
    public const int PanelW = 200;
    public const int HpMax  = 100;

    [ExportGroup("Colors")]
    [Export] public Color BgNormal      = new(0.10f, 0.10f, 0.10f, 0.85f);
    [Export] public Color BgSelf        = new(0.15f, 0.25f, 0.45f, 0.90f);
    [Export] public Color BgOpponent    = new(0.45f, 0.20f, 0.10f, 0.90f);
    [Export] public Color BgEliminated  = new(0.08f, 0.08f, 0.08f, 0.60f);
    [Export] public Color HpBarFg       = new(0.15f, 0.80f, 0.15f, 1.00f);
    [Export] public Color HpBarBg       = new(0.20f, 0.10f, 0.10f, 1.00f);
    [Export] public Color TextColor     = new(1.00f, 1.00f, 1.00f, 1.00f);
    [Export] public Color EliminatedText = new(0.45f, 0.45f, 0.45f, 1.00f);

    private ClientStateManager? _sm;
    private PlayerState? _ownState;
    private int? _combatOpponentId;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(PanelW, 8 * RowH);

        var gc = GetParent<GameClient>();
        _sm = gc.StateManager;

        _sm.OnOwnStateChanged    += state => { _ownState = state; QueueRedraw(); };
        _sm.OnOpponentInfoChanged += (_, _) => QueueRedraw();
        _sm.OnCombatStarted      += (opponentId, _) => { _combatOpponentId = opponentId; QueueRedraw(); };
        _sm.OnCombatEnded        += (_, _) => { _combatOpponentId = null; QueueRedraw(); };
        _sm.OnPlayerEliminated   += (_, _) => QueueRedraw();

        // Seed from current state if already in match
        if (_sm.OwnState.HasValue)
            _ownState = _sm.OwnState;
        _combatOpponentId = _sm.CombatOpponentId;
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        const int fs = 11;

        var rows = BuildRows();
        for (int i = 0; i < rows.Count; i++)
            DrawRow(i, rows[i], font, fs);
    }

    private void DrawRow(int i, PlayerRow row, Font font, int fs)
    {
        float y    = i * RowH;
        var rect   = new Rect2(0, y, PanelW, RowH);
        bool elim  = row.Hp <= 0;

        // Background
        Color bg = row.IsSelf       ? BgSelf :
                   row.IsOpponent   ? BgOpponent :
                   elim             ? BgEliminated :
                                      BgNormal;
        DrawRect(rect, bg);

        Color textC = elim ? EliminatedText : TextColor;
        float ty = y + RowH / 2f + fs / 2f;

        // Label (Me / P{id})
        DrawString(font, new Vector2(6, ty),
            row.Label, HorizontalAlignment.Left, 60, fs, textC);

        // Level
        DrawString(font, new Vector2(68, ty),
            $"Lv{row.Level}", HorizontalAlignment.Left, 28, fs, textC);

        // HP bar (90px wide, 6px tall)
        const float barX = 100f;
        const float barW = 90f;
        const float barH = 6f;
        float barY = y + (RowH - barH) / 2f;
        float frac = Mathf.Clamp((float)row.Hp / HpMax, 0f, 1f);

        DrawRect(new Rect2(barX, barY, barW, barH), HpBarBg);
        if (frac > 0)
            DrawRect(new Rect2(barX, barY, barW * frac, barH), HpBarFg);

        // HP number (right of bar)
        DrawString(font, new Vector2(barX + barW + 4, ty),
            row.Hp.ToString(), HorizontalAlignment.Left, -1, fs, textC);
    }

    private List<PlayerRow> BuildRows()
    {
        var list = new List<PlayerRow>();

        if (_ownState.HasValue)
        {
            var s = _ownState.Value;
            list.Add(new PlayerRow(
                Label:      "Me",
                Hp:         s.NexusHealth,
                Level:      s.Level,
                IsSelf:     true,
                IsOpponent: false));
        }

        if (_sm != null)
        {
            foreach (var kv in _sm.Opponents.OrderBy(x => x.Key))
            {
                var opp = kv.Value;
                list.Add(new PlayerRow(
                    Label:      $"P{opp.PlayerId}",
                    Hp:         opp.NexusHealth,
                    Level:      opp.Level,
                    IsSelf:     false,
                    IsOpponent: _combatOpponentId.HasValue && _combatOpponentId.Value == opp.PlayerId));
            }
        }

        return list;
    }

    private readonly record struct PlayerRow(
        string Label,
        int Hp,
        int Level,
        bool IsSelf,
        bool IsOpponent);
}
