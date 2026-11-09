using System.Text.Json;

namespace NvdaRemoteDroid.Core.Protocol.SpeechSequence;

/// <summary>
/// Comando de fala dentro de speak.sequence, no formato [ClassName, {attrs}] (ex.
/// LangChangeCommand, BreakCommand, EndUtteranceCommand). Atributos ficam crus em JsonElement
/// porque cada classe tem seu próprio conjunto de campos (ver relatório do protocolo §7).
/// </summary>
public sealed record SpeechCommandItem(string ClassName, IReadOnlyDictionary<string, JsonElement> Attributes) : ISpeechSequenceItem
{
    public const string EndUtteranceCommand = "EndUtteranceCommand";
}
