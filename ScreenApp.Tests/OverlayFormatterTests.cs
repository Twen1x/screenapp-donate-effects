using ScreenApp.Donations;
using ScreenApp.Overlay;
using Xunit;

namespace ScreenApp.Tests;

public class OverlayFormatterTests
{
    [Fact]
    public void FormatAmount_Whole_NoDecimals()
    {
        Assert.Equal("100 ₽", OverlayFormatter.FormatAmount(100m));
    }

    [Fact]
    public void FormatAmount_Fractional_TrimsZeros()
    {
        Assert.Equal("99.5 ₽", OverlayFormatter.FormatAmount(99.5m));
    }

    [Fact]
    public void FormatLine_AllParts_JoinedWithDash()
    {
        string line = OverlayFormatter.FormatLine("nick", "100 ₽", "Перевернуть экран");
        Assert.Equal("nick — 100 ₽ — Перевернуть экран", line);
    }

    [Fact]
    public void FormatLine_EmptyParts_OmittedWithoutDanglingSeparators()
    {
        string line = OverlayFormatter.FormatLine("nick", "", "Зеркало");
        Assert.Equal("nick — Зеркало", line);
    }

    [Fact]
    public void FromDonation_FormatsLineWithRubles()
    {
        var donation = new Donation { Username = "someviewer", Amount = 100 };
        var msg = OverlayMessage.FromDonation(donation, "Перевернуть экран");
        Assert.Equal("someviewer — 100 ₽ — Перевернуть экран", msg.Text);
    }

    [Fact]
    public void FromDonation_Anonymous_UsesPlaceholder()
    {
        var donation = new Donation { Username = null, Amount = 50 };
        var msg = OverlayMessage.FromDonation(donation, "Конфетти");
        Assert.Equal("Аноним — 50 ₽ — Конфетти", msg.Text);
    }
}
