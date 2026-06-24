using ScreenApp.Donations;
using ScreenApp.Settings;
using Xunit;

namespace ScreenApp.Tests;

public class DonationRouterTests
{
    // Цены выше дефолтных (макс. 150 в каталоге), чтобы не пересекаться с другими действиями.
    private static AppSettings SettingsWith(params (string id, int price, bool enabled)[] overrides)
    {
        var s = AppSettings.CreateDefault();
        foreach (var (id, price, enabled) in overrides)
        {
            var setting = s.Actions.First(a => a.ActionId == id);
            setting.Price = price;
            setting.Enabled = enabled;
        }
        return s;
    }

    [Fact]
    public void Route_ExactPriceMatch_ReturnsAction()
    {
        var settings = SettingsWith(("blackout", 200, true));
        var router = new DonationRouter(settings);

        var result = router.Route(new Donation { Amount = 200 });

        Assert.NotNull(result);
        Assert.Equal("blackout", result!.ActionId);
    }

    [Fact]
    public void Route_NoMatchingPrice_ReturnsNull()
    {
        var settings = SettingsWith(("blackout", 200, true));
        var router = new DonationRouter(settings);

        Assert.Null(router.Route(new Donation { Amount = 999 }));
    }

    [Fact]
    public void Route_DisabledAction_NotMatched()
    {
        var settings = SettingsWith(("blackout", 205, false));
        var router = new DonationRouter(settings);

        Assert.Null(router.Route(new Donation { Amount = 205 }));
    }

    [Fact]
    public void Route_TextAction_PassesMessageAsText()
    {
        var settings = SettingsWith(("caption", 210, true));
        var router = new DonationRouter(settings);

        var result = router.Route(new Donation { Amount = 210, Message = "Привет" });

        Assert.NotNull(result);
        Assert.Equal("caption", result!.ActionId);
        Assert.Equal("Привет", result.Text);
    }

    [Fact]
    public void Route_NonTextAction_TextIsNull()
    {
        var settings = SettingsWith(("blackout", 215, true));
        var router = new DonationRouter(settings);

        var result = router.Route(new Donation { Amount = 215, Message = "msg" });

        Assert.NotNull(result);
        Assert.Null(result!.Text);
    }

    [Fact]
    public void Route_FractionalAmount_NoMatch()
    {
        var settings = SettingsWith(("blackout", 200, true));
        var router = new DonationRouter(settings);

        Assert.Null(router.Route(new Donation { Amount = 200.5m }));
    }

    [Fact]
    public void Route_DuplicatePrice_PicksFirstInCatalogOrder()
    {
        // rotate_180 идёт раньше mirror в каталоге.
        var settings = SettingsWith(("rotate_180", 220, true), ("mirror", 220, true));
        var router = new DonationRouter(settings);

        var result = router.Route(new Donation { Amount = 220 });

        Assert.NotNull(result);
        Assert.Equal("rotate_180", result!.ActionId);
    }

    [Fact]
    public void Route_UsesDurationFromSettings()
    {
        var settings = SettingsWith(("blackout", 230, true));
        settings.Actions.First(a => a.ActionId == "blackout").DurationSeconds = 33;
        var router = new DonationRouter(settings);

        var result = router.Route(new Donation { Amount = 230 });

        Assert.Equal(33, result!.DurationSeconds);
    }
}
