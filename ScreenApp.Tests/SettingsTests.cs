using System.IO;
using ScreenApp.Actions;
using ScreenApp.Settings;
using Xunit;

namespace ScreenApp.Tests;

public class SettingsTests
{
    [Fact]
    public void CreateDefault_ContainsEveryCatalogAction()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal(ActionCatalog.All.Count, settings.Actions.Count);
        foreach (var info in ActionCatalog.All)
        {
            Assert.Contains(settings.Actions, a => a.ActionId == info.ActionId);
        }
    }

    [Fact]
    public void Catalog_EveryActionIsRegistered()
    {
        // Каждое действие каталога должно быть реализовано в реестре.
        var registry = new ActionRegistry();
        foreach (var info in ActionCatalog.All)
        {
            Assert.True(registry.IsRegistered(info.ActionId), $"не зарегистрировано: {info.ActionId}");
        }
    }

    [Fact]
    public void SyncWithCatalog_AddsMissingAndDropsUnknown()
    {
        var settings = new AppSettings
        {
            Actions =
            {
                new ActionSetting { ActionId = "blackout", Price = 999, Enabled = false },
                new ActionSetting { ActionId = "does_not_exist", Price = 5 },
            },
        };

        settings.SyncWithCatalog();

        Assert.Equal(ActionCatalog.All.Count, settings.Actions.Count);
        Assert.DoesNotContain(settings.Actions, a => a.ActionId == "does_not_exist");

        // Существующая запись сохраняет правки пользователя.
        var blackout = settings.Actions.First(a => a.ActionId == "blackout");
        Assert.Equal(999, blackout.Price);
        Assert.False(blackout.Enabled);
    }

    [Fact]
    public void Store_SaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"screenapp_{Guid.NewGuid():N}.json");
        var store = new SettingsStore(path);
        try
        {
            var settings = AppSettings.CreateDefault();
            settings.DaAccessToken = "tok-123";
            settings.DaClientId = "cid";
            settings.Actions.First(a => a.ActionId == "blackout").Price = 321;

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal("tok-123", loaded.DaAccessToken);
            Assert.Equal("cid", loaded.DaClientId);
            Assert.Equal(321, loaded.Actions.First(a => a.ActionId == "blackout").Price);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Store_MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.json");
        var store = new SettingsStore(path);

        var loaded = store.Load();

        Assert.Equal(ActionCatalog.All.Count, loaded.Actions.Count);
        Assert.Equal("", loaded.DaAccessToken);
    }

    [Fact]
    public void ActionSetting_NegativeValues_ClampedToZero()
    {
        var s = new ActionSetting { Price = -5, DurationSeconds = -10 };
        Assert.Equal(0, s.Price);
        Assert.Equal(0, s.DurationSeconds);
    }
}
