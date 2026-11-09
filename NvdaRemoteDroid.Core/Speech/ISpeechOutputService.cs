namespace NvdaRemoteDroid.Core.Speech;

/// <summary>
/// Reproduz localmente a fala recebida do follower via TTS nativo. A implementação Android usa
/// Android.Speech.Tts.TextToSpeech diretamente (não o Essentials, que não expõe QUEUE_FLUSH
/// nem pausa) para poder honrar priority e pause_speech.
/// </summary>
public interface ISpeechOutputService
{
    void Speak(string utterance, SpeechPriority priority);
    void Cancel();
    void SetPaused(bool paused);
}
