using System.Text.Json;
using ScreenApp.Donations;
using Xunit;

namespace ScreenApp.Tests;

public class DonateXClientTests
{
    private static JsonElement Json(string s)
    {
        using var doc = JsonDocument.Parse(s);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void MapDonation_UsesAmountInRub()
    {
        var d = DonateXClient.MapDonation(Json("""
        {
          "id": "d8c9",
          "username": "viewer123",
          "message": "hello",
          "currency": "USD",
          "amount": 2.00,
          "amountInRub": 150.00,
          "isTest": false
        }
        """));

        Assert.NotNull(d);
        Assert.Equal("d8c9", d!.DonationId);
        Assert.Equal(150.00m, d.Amount); // в рублях, а не 2 USD
        Assert.Equal("viewer123", d.Username);
        Assert.Equal("hello", d.Message);
        Assert.False(d.IsTest);
    }

    [Fact]
    public void MapDonation_TestFlag()
    {
        var d = DonateXClient.MapDonation(Json("""
        { "id": "x", "username": "t", "amountInRub": 100, "isTest": true }
        """));

        Assert.NotNull(d);
        Assert.True(d!.IsTest);
    }

    [Fact]
    public void MapDonation_FallsBackToAmountWhenNoRub()
    {
        var d = DonateXClient.MapDonation(Json("""
        { "id": "y", "username": "t", "amount": 75 }
        """));

        Assert.NotNull(d);
        Assert.Equal(75m, d!.Amount);
    }

    [Fact]
    public void MapDonation_NonObject_ReturnsNull()
    {
        Assert.Null(DonateXClient.MapDonation(Json("123")));
    }
}
