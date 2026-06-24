namespace ScreenApp.Donations;

/// <summary>
/// Донат, полученный от DonationAlerts (через Centrifugo) или сгенерированный
/// как тестовый. Содержит только то, что нужно приложению для подбора действия
/// и показа подписи оверлея.
/// </summary>
public sealed class Donation
{
    /// <summary>Идентификатор доната DA (для дедупликации). У тестовых — null.</summary>
    public string? DonationId { get; init; }

    /// <summary>Сумма доната (в рублях). Используется для подбора действия по цене.</summary>
    public decimal Amount { get; init; }

    /// <summary>Ник зрителя или null (аноним).</summary>
    public string? Username { get; init; }

    /// <summary>Сообщение зрителя (для текстовых действий и поиска ключевого слова).</summary>
    public string? Message { get; init; }

    /// <summary>true — это тестовый алерт DA или ручной тест из настроек.</summary>
    public bool IsTest { get; init; }

    /// <summary>Ник для показа: реальный или «Аноним».</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Username) ? "Аноним" : Username!;
}
