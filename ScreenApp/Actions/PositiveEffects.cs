namespace ScreenApp.Actions;

/// <summary>Конфетти, падающее сверху (action_id = confetti).</summary>
public sealed class ConfettiAction : ParticleOverlayAction
{
    public override string ActionId => "confetti";
    protected override string[] Glyphs => new[] { "🎉", "🎊", "✨", "🟥", "🟦", "🟨", "🟩" };
    protected override int Count => 80;
    protected override bool FallDown => true;
}

/// <summary>Сердечки, всплывающие снизу (action_id = hearts).</summary>
public sealed class HeartsAction : ParticleOverlayAction
{
    public override string ActionId => "hearts";
    protected override string[] Glyphs => new[] { "💜", "❤️", "💖", "💕", "💗" };
    protected override int Count => 50;
    protected override bool FallDown => false;
}

/// <summary>Снегопад (action_id = snowfall).</summary>
public sealed class SnowfallAction : ParticleOverlayAction
{
    public override string ActionId => "snowfall";
    protected override string[] Glyphs => new[] { "❄️", "❅", "❆", "✻" };
    protected override int Count => 90;
    protected override bool FallDown => true;
}

/// <summary>Воздушные шарики, всплывающие снизу (action_id = balloons).</summary>
public sealed class BalloonsAction : ParticleOverlayAction
{
    public override string ActionId => "balloons";
    protected override string[] Glyphs => new[] { "🎈", "🎈", "🎈" };
    protected override int Count => 30;
    protected override double Size => 56;
    protected override bool FallDown => false;
}

/// <summary>Падающие звёзды (action_id = stars).</summary>
public sealed class StarsAction : ParticleOverlayAction
{
    public override string ActionId => "stars";
    protected override string[] Glyphs => new[] { "⭐", "🌟", "✨", "💫" };
    protected override int Count => 70;
    protected override bool FallDown => true;
}

/// <summary>Дождь из монет (action_id = coin_rain).</summary>
public sealed class CoinRainAction : ParticleOverlayAction
{
    public override string ActionId => "coin_rain";
    protected override string[] Glyphs => new[] { "🪙", "💰", "💵", "🤑" };
    protected override int Count => 70;
    protected override bool FallDown => true;
}

/// <summary>Фейерверк (action_id = fireworks): взрывы салюта сыплются сверху.</summary>
public sealed class FireworksAction : ParticleOverlayAction
{
    public override string ActionId => "fireworks";
    protected override string[] Glyphs => new[] { "🎆", "🎇", "✨", "💥" };
    protected override int Count => 50;
    protected override double Size => 56;
    protected override bool FallDown => true;
}
