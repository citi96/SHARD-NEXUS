#nullable enable
using System.Collections.Generic;
using Godot;
using Shared.Data;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Client.Scripts.Shop;

/// <summary>
/// Renders the 5-slot shop strip with Refresh and BuyXP buttons.
/// Uses _Draw() + _GuiInput() (same pattern as GridRenderer/BenchRenderer).
/// Visible only during Preparation phase.
///
/// Initialization: _Ready() auto-wires to the parent GameClient node.
/// </summary>
public partial class ShopPanel : Control
{
    public const int ShopSize = 5;
    public const int SlotSize = 80;
    public const int SlotGap  = 4;
    public const int GoldRowH = 20;
    public const int ButtonW  = 90;
    public const int ButtonH  = 32;
    public const int ButtonGap = 6;
    public const int SlotY    = GoldRowH + 4;
    public const int ButtonY  = SlotY + SlotSize + 8;

    [ExportGroup("Colors")]
    [Export] public Color EmptySlot     = new(0.15f, 0.15f, 0.15f, 0.70f);
    [Export] public Color SlotNameColor = new(1.00f, 1.00f, 1.00f, 1.00f);
    [Export] public Color GoldColor     = new(1.00f, 0.85f, 0.20f, 1.00f);
    [Export] public Color ButtonBg      = new(0.25f, 0.25f, 0.25f, 1.00f);
    [Export] public Color ButtonBorder  = new(0.50f, 0.50f, 0.50f, 1.00f);
    [Export] public Color StatusColor   = new(1.00f, 0.30f, 0.30f, 1.00f);
    [Export] public Color PendingColor  = new(0.00f, 0.00f, 0.00f, 0.55f);
    [Export] public Color CommonColor    = new(0.55f, 0.55f, 0.55f, 0.70f);
    [Export] public Color UncommonColor  = new(0.20f, 0.80f, 0.20f, 0.70f);
    [Export] public Color RareColor      = new(0.10f, 0.40f, 0.90f, 0.70f);
    [Export] public Color EpicColor      = new(0.60f, 0.10f, 0.90f, 0.70f);
    [Export] public Color LegendaryColor = new(1.00f, 0.70f, 0.00f, 0.70f);

    [ExportGroup("Echo Colors - Class")]
    [Export] public Color ClassVanguard = new(0.25f, 0.45f, 0.90f, 1.00f);
    [Export] public Color ClassStriker  = new(0.90f, 0.50f, 0.15f, 1.00f);
    [Export] public Color ClassRanger   = new(0.20f, 0.75f, 0.35f, 1.00f);
    [Export] public Color ClassCaster   = new(0.65f, 0.25f, 0.90f, 1.00f);
    [Export] public Color ClassSupport  = new(0.90f, 0.80f, 0.15f, 1.00f);
    [Export] public Color ClassAssassin = new(0.70f, 0.10f, 0.20f, 1.00f);

    [ExportGroup("Echo Colors - Resonance")]
    [Export] public Color ResonanceFire      = new(1.00f, 0.30f, 0.10f, 1.00f);
    [Export] public Color ResonanceFrost     = new(0.30f, 0.80f, 1.00f, 1.00f);
    [Export] public Color ResonanceLightning = new(1.00f, 0.90f, 0.00f, 1.00f);
    [Export] public Color ResonanceEarth     = new(0.60f, 0.40f, 0.10f, 1.00f);
    [Export] public Color ResonanceVoid      = new(0.50f, 0.10f, 0.70f, 1.00f);
    [Export] public Color ResonanceLight     = new(1.00f, 0.95f, 0.70f, 1.00f);
    [Export] public Color ResonanceShadow    = new(0.25f, 0.20f, 0.35f, 1.00f);
    [Export] public Color ResonancePrism     = new(0.80f, 0.80f, 0.80f, 1.00f);

    private GameClient? _gameClient;
    private ClientStateManager? _sm;
    private List<int> _shopEchoIds = new();
    private int _gold = 0;
    private int _pendingSlot = -1;
    private float _statusTimer = 0f;
    private string _statusText = string.Empty;

    public override void _Ready()
    {
        float totalW = ShopSize * SlotSize + (ShopSize - 1) * SlotGap;
        float totalH = ButtonY + ButtonH + 8 + 16;
        CustomMinimumSize = new Vector2(totalW, totalH);
        MouseFilter = MouseFilterEnum.Stop;

        _gameClient = GetParent<GameClient>();
        _sm = _gameClient.StateManager;

        _sm.OnShopChanged     += OnShopChanged;
        _sm.OnOwnStateChanged += OnOwnStateChanged;
        _sm.OnActionRejected  += OnActionRejected;
        _sm.OnPhaseChanged    += OnPhaseChanged;
        _sm.OnRoundStarted    += _ => { Visible = true; QueueRedraw(); };

        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_sm == null) return;
        _sm.OnShopChanged     -= OnShopChanged;
        _sm.OnOwnStateChanged -= OnOwnStateChanged;
        _sm.OnActionRejected  -= OnActionRejected;
        _sm.OnPhaseChanged    -= OnPhaseChanged;
    }

    public override void _Process(double delta)
    {
        if (_statusTimer <= 0) return;
        _statusTimer -= (float)delta;
        if (_statusTimer <= 0) _statusText = string.Empty;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        const int fs = 11;

        // Gold display
        DrawString(font, new Vector2(0, GoldRowH - 3),
            $"G: {_gold}", HorizontalAlignment.Left, -1, fs + 2, GoldColor);

        // Shop slots
        for (int i = 0; i < ShopSize; i++)
        {
            var rect  = SlotRect(i);
            int defId = i < _shopEchoIds.Count ? _shopEchoIds[i] : -1;

            if (defId <= 0)
            {
                DrawRect(rect, EmptySlot);
            }
            else
            {
                var def = EchoCatalog.GetById(defId);
                DrawRect(rect, RarityColor(def?.Rarity));

                const int margin = 4;
                const int bodyH  = 38;
                var inner = new Rect2(
                    rect.Position + new Vector2(margin, margin),
                    new Vector2(rect.Size.X - margin * 2, bodyH));
                DrawRect(inner, ClassColor(def?.Class));
                DrawRect(inner, ResonanceColor(def?.Resonance), false, 1.5f);

                string name = def?.Name ?? $"#{defId}";
                DrawString(font, rect.Position + new Vector2(4, fs + 4),
                    name, HorizontalAlignment.Left, rect.Size.X - 4, fs, SlotNameColor);

                DrawString(font, rect.Position + new Vector2(4, rect.Size.Y - 6),
                    $"{CostForRarity(def?.Rarity)}G", HorizontalAlignment.Left,
                    rect.Size.X - 4, fs, GoldColor);

                if (_pendingSlot == i)
                {
                    DrawRect(rect, PendingColor);
                    DrawString(font, rect.Position + new Vector2(0, rect.Size.Y / 2f + fs / 2f),
                        "...", HorizontalAlignment.Center, rect.Size.X, fs, SlotNameColor);
                }
            }
        }

        // Refresh button
        var refreshRect = RefreshRect();
        DrawRect(refreshRect, ButtonBg);
        DrawRect(refreshRect, ButtonBorder, false, 1f);
        DrawString(font, refreshRect.Position + new Vector2(6, ButtonH / 2f + fs / 2f),
            "\u21ba 2G", HorizontalAlignment.Left, ButtonW - 6, fs, SlotNameColor);

        // BuyXP button
        var buyXpRect = BuyXpRect();
        DrawRect(buyXpRect, ButtonBg);
        DrawRect(buyXpRect, ButtonBorder, false, 1f);
        DrawString(font, buyXpRect.Position + new Vector2(6, ButtonH / 2f + fs / 2f),
            "+XP 4G", HorizontalAlignment.Left, ButtonW - 6, fs, SlotNameColor);

        // Status text (fades over 3 s)
        if (_statusText.Length > 0 && _statusTimer > 0)
        {
            float alpha = Mathf.Clamp(_statusTimer / 3f, 0f, 1f);
            DrawString(font, new Vector2(0, ButtonY + ButtonH + 14),
                _statusText, HorizontalAlignment.Left, CustomMinimumSize.X, fs,
                new Color(StatusColor, alpha));
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } btn) return;

        var pos = btn.Position;

        for (int i = 0; i < ShopSize; i++)
        {
            if (!SlotRect(i).HasPoint(pos)) continue;
            int defId = i < _shopEchoIds.Count ? _shopEchoIds[i] : -1;
            if (defId <= 0 || _pendingSlot == i) return;
            _pendingSlot = i;
            QueueRedraw();
            _gameClient?.SendBuyEcho(i);
            return;
        }

        if (RefreshRect().HasPoint(pos))
        {
            _gameClient?.SendRefreshShop();
            return;
        }

        if (BuyXpRect().HasPoint(pos))
            _gameClient?.SendBuyXP();
    }

    private void OnShopChanged(List<int> ids)
    {
        _shopEchoIds = ids;
        _pendingSlot = -1;
        QueueRedraw();
    }

    private void OnOwnStateChanged(PlayerState state)
    {
        _gold = state.Gold;
        _pendingSlot = -1;
        QueueRedraw();
    }

    private void OnActionRejected(ActionRejectedMessage msg)
    {
        if (msg.Action is "BuyEcho" or "RefreshShop")
        {
            _pendingSlot = -1;
            ShowStatus(msg.Reason);
        }
    }

    private void OnPhaseChanged(GamePhase phase, float _duration)
    {
        Visible = phase == GamePhase.Preparation;
        if (!Visible) _pendingSlot = -1;
    }

    private void ShowStatus(string text)
    {
        _statusText  = text;
        _statusTimer = 3f;
        QueueRedraw();
    }

    private static Rect2 SlotRect(int i) =>
        new(i * (SlotSize + SlotGap), SlotY, SlotSize, SlotSize);

    private static Rect2 RefreshRect() =>
        new(0, ButtonY, ButtonW, ButtonH);

    private static Rect2 BuyXpRect() =>
        new(ButtonW + ButtonGap, ButtonY, ButtonW, ButtonH);

    private Color RarityColor(Rarity? rarity) => rarity switch
    {
        Rarity.Common    => CommonColor,
        Rarity.Uncommon  => UncommonColor,
        Rarity.Rare      => RareColor,
        Rarity.Epic      => EpicColor,
        Rarity.Legendary => LegendaryColor,
        _                => EmptySlot
    };

    private Color ClassColor(EchoClass? cls) => cls switch
    {
        EchoClass.Vanguard  => ClassVanguard,
        EchoClass.Striker   => ClassStriker,
        EchoClass.Ranger    => ClassRanger,
        EchoClass.Caster    => ClassCaster,
        EchoClass.Support   => ClassSupport,
        EchoClass.Assassin  => ClassAssassin,
        _                   => new Color(0.3f, 0.3f, 0.3f)
    };

    private Color ResonanceColor(Resonance? res) => res switch
    {
        Resonance.Fire      => ResonanceFire,
        Resonance.Frost     => ResonanceFrost,
        Resonance.Lightning => ResonanceLightning,
        Resonance.Earth     => ResonanceEarth,
        Resonance.Void      => ResonanceVoid,
        Resonance.Light     => ResonanceLight,
        Resonance.Shadow    => ResonanceShadow,
        Resonance.Prism     => ResonancePrism,
        _                   => new Color(0.5f, 0.5f, 0.5f)
    };

    private static int CostForRarity(Rarity? rarity) => rarity switch
    {
        Rarity.Common    => 1,
        Rarity.Uncommon  => 2,
        Rarity.Rare      => 3,
        Rarity.Epic      => 4,
        Rarity.Legendary => 5,
        _                => 1
    };
}
