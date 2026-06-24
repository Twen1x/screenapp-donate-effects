using System.Diagnostics;

namespace ScreenApp.Actions;

/// <summary>
/// Озвучка текста зрителя (action_id = tts).
///
/// Использует системный SAPI через позднее связывание COM (ProgID "SAPI.SpVoice"),
/// чтобы НЕ тянуть NuGet-пакет System.Speech (важно: сборка идёт офлайн, без
/// докачки пакетов). Озвучка запускается асинхронно (SVSFlagsAsync = 1), поэтому
/// Execute не блокирует UI-поток. Revert останавливает текущую речь.
/// </summary>
public sealed class TtsAction : IScreenAction, ITextAction
{
    // SVSFlagsAsync (1) | SVSFPurgeBeforeSpeak (2) — говорить асинхронно, очищая очередь.
    private const int SpeakFlags = 1 | 2;
    private const int PurgeFlag = 2;

    private string _text = "";
    private object? _voice;

    public string ActionId => "tts";

    public void SetText(string? text) => _text = (text ?? "").Trim();

    public void Execute()
    {
        if (string.IsNullOrEmpty(_text))
        {
            return;
        }

        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (type is null)
            {
                Debug.WriteLine("[TtsAction] SAPI.SpVoice недоступен на этой системе.");
                return;
            }

            _voice = Activator.CreateInstance(type);
            // voice.Speak(text, SpeakFlags) — асинхронно.
            type.InvokeMember(
                "Speak",
                System.Reflection.BindingFlags.InvokeMethod,
                null, _voice, new object[] { _text, SpeakFlags });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TtsAction] ошибка озвучки: {ex.Message}");
        }
    }

    public void Revert()
    {
        if (_voice is null)
        {
            return;
        }

        try
        {
            // Остановить текущую речь: Speak("", PurgeBeforeSpeak).
            _voice.GetType().InvokeMember(
                "Speak",
                System.Reflection.BindingFlags.InvokeMethod,
                null, _voice, new object[] { "", PurgeFlag });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TtsAction] ошибка остановки озвучки: {ex.Message}");
        }
        finally
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(_voice))
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(_voice);
            }
            _voice = null;
        }
    }
}
