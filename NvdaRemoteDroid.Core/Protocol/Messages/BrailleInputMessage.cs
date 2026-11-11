using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// "Gesto" enviado ao follower (NVDA) para executar um script dele. Reaproveita braille_input: o
/// NVDA reconstrói um BrailleInputGesture a partir dos campos e resolve <c>scriptPath</c>
/// ([módulo, classe, nomeDoScript]) — o que cai em <c>globalCommands.commands.script_&lt;nome&gt;</c>.
/// <c>source</c> é obrigatório (o NVDA chama <c>.capitalize()</c> nele); <c>model</c> e <c>id</c>
/// vão para ficar idênticos ao que um leader NVDA de verdade envia. Um nome de script inexistente
/// é ignorado em silêncio pelo NVDA (NoInputGestureAction), então só use nomes validados.
/// </summary>
public sealed record BrailleInputMessage(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("scriptPath")] IReadOnlyList<string> ScriptPath
) : IRemoteMessage
{
    public const string GestureSource = "nvdaRemoteDroid";
    public const string GestureModel = "gesturePad";

    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.BrailleInput;

    /// <summary>Executa um script de <c>globalCommands.GlobalCommands</c> (ex.: "navigatorObject_parent").</summary>
    public static BrailleInputMessage ForGlobalCommand(string scriptName, string gestureId) => new(
        GestureSource,
        GestureModel,
        gestureId,
        ["globalCommands", "GlobalCommands", scriptName]);
}
