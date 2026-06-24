namespace ScreenApp.Actions;

/// <summary>
/// Действие, которому нужен текст, введённый зрителем (caption, ticker, tts, subtitle).
/// Текст передаётся перед Execute через <see cref="SetText"/>.
/// </summary>
public interface ITextAction
{
    /// <summary>Установить текст задания (вызывается до Execute).</summary>
    void SetText(string? text);
}
