namespace NvdaRemoteDroid.Core.Protocol.SpeechSequence;

/// <summary>Trecho de texto puro dentro de speak.sequence.</summary>
public sealed record SpeechTextItem(string Text) : ISpeechSequenceItem;
