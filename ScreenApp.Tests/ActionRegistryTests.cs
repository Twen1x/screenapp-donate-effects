using ScreenApp.Actions;
using Xunit;

namespace ScreenApp.Tests;

public class ActionRegistryTests
{
    [Theory]
    [InlineData("rotate_180")]
    [InlineData("white_screen")]
    [InlineData("blackout")]
    [InlineData("dim_50")]
    [InlineData("disable_mouse")]
    [InlineData("disable_keyboard")]
    [InlineData("mirror")]
    public void Registry_RegistersAllSeededActions(string actionId)
    {
        var registry = new ActionRegistry();
        Assert.True(registry.IsRegistered(actionId));
    }

    [Fact]
    public void Resolve_Mirror_ReturnsMirrorActionWithMatchingId()
    {
        var registry = new ActionRegistry();

        var action = registry.Resolve("mirror");

        Assert.NotNull(action);
        Assert.IsType<MirrorAction>(action);
        Assert.Equal("mirror", action!.ActionId);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var registry = new ActionRegistry();

        var action = registry.Resolve("MIRROR");

        Assert.NotNull(action);
        Assert.Equal("mirror", action!.ActionId);
    }

    [Fact]
    public void Resolve_UnknownAction_ReturnsNull()
    {
        var registry = new ActionRegistry();
        Assert.Null(registry.Resolve("does_not_exist"));
    }

    [Fact]
    public void Resolve_CreatesFreshInstanceEachCall()
    {
        var registry = new ActionRegistry();

        var first = registry.Resolve("mirror");
        var second = registry.Resolve("mirror");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }
}
