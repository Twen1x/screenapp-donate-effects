using ScreenApp.Donations;
using Xunit;

namespace ScreenApp.Tests;

public class DonationParserTests
{
    [Fact]
    public void Parse_NewProtocol_PushPubData()
    {
        const string json = """
        {
          "push": { "pub": { "data": {
            "id": 12345,
            "amount_main": 100,
            "username": "viewer1",
            "message": "Привет!",
            "_is_test_alert": false
          } } }
        }
        """;

        var d = DonationParser.Parse(json);

        Assert.NotNull(d);
        Assert.Equal("12345", d!.DonationId);
        Assert.Equal(100m, d.Amount);
        Assert.Equal("viewer1", d.Username);
        Assert.Equal("Привет!", d.Message);
        Assert.False(d.IsTest);
    }

    [Fact]
    public void Parse_OldProtocol_ResultDataData()
    {
        const string json = """
        {
          "result": { "data": { "data": {
            "id": 7,
            "amount": "55.5",
            "username": "buyer",
            "message": "hi"
          } } }
        }
        """;

        var d = DonationParser.Parse(json);

        Assert.NotNull(d);
        Assert.Equal("7", d!.DonationId);
        Assert.Equal(55.5m, d.Amount);
        Assert.Equal("buyer", d.Username);
    }

    [Fact]
    public void Parse_TestAlert_FlaggedAsTest()
    {
        const string json = """
        { "push": { "pub": { "data": {
          "id": 1, "amount_main": 10, "username": "t", "_is_test_alert": true
        } } } }
        """;

        var d = DonationParser.Parse(json);

        Assert.NotNull(d);
        Assert.True(d!.IsTest);
    }

    [Fact]
    public void Parse_ServiceMessage_ReturnsNull()
    {
        // Ответ на connect — не донат.
        const string json = """{ "id": 1, "result": { "client": "abc-123" } }""";
        Assert.Null(DonationParser.Parse(json));
    }

    [Fact]
    public void Parse_EmptyPing_ReturnsNull()
    {
        Assert.Null(DonationParser.Parse("{}"));
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        Assert.Null(DonationParser.Parse("not json"));
    }

    [Fact]
    public void Parse_ZeroAmountWithoutDonationAlertType_ReturnsNull()
    {
        const string json = """
        { "push": { "pub": { "data": { "id": 2, "amount": 0, "alert_type": "4" } } } }
        """;
        Assert.Null(DonationParser.Parse(json));
    }
}
