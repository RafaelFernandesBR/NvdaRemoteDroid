namespace NvdaRemoteDroid.Core.Speech;

/// <summary>Motor de TTS instalado no dispositivo (Google, RHVoice, Eloquence…). <c>Id</c> é o pacote.</summary>
public sealed record TtsEngineInfo(string Id, string Display);
