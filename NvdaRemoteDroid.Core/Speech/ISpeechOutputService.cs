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

    /// <summary>
    /// Pausa (true) ou retoma (false). Enquanto pausado, a fala recebida é descartada — o TTS do
    /// Android não tem pausa real. Quem recebe <c>cancel</c> do protocolo deve também chamar
    /// <c>SetPaused(false)</c>: o NVDA zera a pausa ao cancelar sem mandar pause_speech(false).
    /// </summary>
    void SetPaused(bool paused);

    /// <summary>Motores de TTS instalados, ordenados por nome (sem inicializar nenhum).</summary>
    IReadOnlyList<TtsEngineInfo> GetInstalledEngines();

    /// <summary>
    /// Define o motor (pacote; vazio/null = padrão do dispositivo) e a velocidade. Se o motor
    /// escolhido falhar ao iniciar, a implementação cai no motor padrão para nunca ficar mudo.
    /// </summary>
    void Configure(string? enginePackage, float rate);

    /// <summary>Fala de teste (priority Now) que ignora o estado de pausa do NVDA.</summary>
    void SpeakSample(string text);
}
