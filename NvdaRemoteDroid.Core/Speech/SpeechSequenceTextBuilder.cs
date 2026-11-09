using NvdaRemoteDroid.Core.Protocol.SpeechSequence;

namespace NvdaRemoteDroid.Core.Speech;

/// <summary>
/// Reduz um speak.sequence heterogêneo a texto simples para TTS: concatena os trechos de
/// texto e trata EndUtteranceCommand como quebra de frase. Qualquer outro comando
/// (LangChangeCommand, BreakCommand, PitchCommand, etc.) é ignorado com segurança — a v1 não
/// tenta reproduzir prosódia, apenas o conteúdo falado.
/// </summary>
public static class SpeechSequenceTextBuilder
{
    public static IReadOnlyList<string> ToUtterances(IEnumerable<ISpeechSequenceItem> sequence)
    {
        var utterances = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var item in sequence)
        {
            switch (item)
            {
                case SpeechTextItem text:
                    current.Append(text.Text);
                    break;

                case SpeechCommandItem { ClassName: SpeechCommandItem.EndUtteranceCommand }:
                    FlushInto(utterances, current);
                    break;
            }
        }

        FlushInto(utterances, current);
        return utterances;
    }

    private static void FlushInto(List<string> utterances, System.Text.StringBuilder current)
    {
        if (current.Length > 0)
        {
            utterances.Add(current.ToString());
            current.Clear();
        }
    }
}
