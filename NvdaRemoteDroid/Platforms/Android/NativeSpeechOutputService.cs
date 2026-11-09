using Android.Speech.Tts;
using NvdaRemoteDroid.Core.Speech;
using Application = Android.App.Application;
using AndroidTts = Android.Speech.Tts.TextToSpeech;

namespace NvdaRemoteDroid.Services;

/// <summary>
/// TTS nativo Android (Android.Speech.Tts.TextToSpeech, não o Essentials) para poder honrar
/// QUEUE_FLUSH (priority=Now) e um pause/resume best-effort — o Essentials não expõe nenhum
/// dos dois. Inicialização é lazy: a primeira chamada de Speak dispara o OnInit e falas
/// anteriores a ele ficam enfileiradas no próprio engine assim que ele fica pronto.
/// </summary>
public sealed class NativeSpeechOutputService : Java.Lang.Object, ISpeechOutputService, AndroidTts.IOnInitListener
{
    private AndroidTts? _tts;
    private bool _ready;
    private bool _paused;
    private readonly Queue<(string Utterance, SpeechPriority Priority)> _pendingBeforeReady = new();
    private readonly object _gate = new();

    public NativeSpeechOutputService()
    {
        _tts = new AndroidTts(Application.Context, this);
    }

    public void OnInit(OperationResult status)
    {
        lock (_gate)
        {
            _ready = status == OperationResult.Success;
            if (!_ready || _tts is null)
                return;

            while (_pendingBeforeReady.Count > 0)
            {
                var (utterance, priority) = _pendingBeforeReady.Dequeue();
                SpeakInternal(utterance, priority);
            }
        }
    }

    public void Speak(string utterance, SpeechPriority priority)
    {
        lock (_gate)
        {
            if (_paused)
                return;

            if (!_ready || _tts is null)
            {
                _pendingBeforeReady.Enqueue((utterance, priority));
                return;
            }

            SpeakInternal(utterance, priority);
        }
    }

    private void SpeakInternal(string utterance, SpeechPriority priority)
    {
        var queueMode = priority == SpeechPriority.Now ? QueueMode.Flush : QueueMode.Add;
        _tts?.Speak(utterance, queueMode, null, Guid.NewGuid().ToString("N"));
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _pendingBeforeReady.Clear();
            _tts?.Stop();
        }
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            _paused = paused;
            if (paused)
                _tts?.Stop(); // sem fila real a retomar: best-effort, ver relatório do plano §TTS
        }
    }
}
