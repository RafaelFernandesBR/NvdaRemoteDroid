using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.SpeechSequence;

/// <summary>
/// Converte speak.sequence: um array JSON onde cada elemento é OU uma string pura OU um array
/// de 2 posições [ClassName:string, attrs:object]. Um elemento em formato inesperado é
/// ignorado com segurança (nunca derruba o parse da mensagem inteira) — mesma tolerância que
/// o NVDA aplica a SEQUENCE_CLASSES desconhecidas.
/// </summary>
public sealed class SpeechSequenceJsonConverter : JsonConverter<List<ISpeechSequenceItem>>
{
    public override List<ISpeechSequenceItem> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("speak.sequence deve ser um array JSON.");

        var items = new List<ISpeechSequenceItem>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    items.Add(new SpeechTextItem(reader.GetString() ?? string.Empty));
                    break;

                case JsonTokenType.StartArray:
                    TryReadCommand(ref reader, items);
                    break;

                default:
                    // Tipo inesperado (número, bool, objeto solto): ignora o item, segue o array.
                    reader.Skip();
                    break;
            }
        }

        return items;
    }

    private static void TryReadCommand(ref Utf8JsonReader reader, List<ISpeechSequenceItem> items)
    {
        string? className = null;
        Dictionary<string, JsonElement>? attributes = null;

        if (reader.Read() && reader.TokenType == JsonTokenType.String)
            className = reader.GetString();

        if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            attributes = new Dictionary<string, JsonElement>();
            foreach (var property in doc.RootElement.EnumerateObject())
                attributes[property.Name] = property.Value.Clone();
        }

        // Consome o resto do array interno, caso tenha mais posições do que esperamos.
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            if (!reader.Read())
                break;
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                reader.Skip();
        }

        if (className is not null)
            items.Add(new SpeechCommandItem(className, (IReadOnlyDictionary<string, JsonElement>?)attributes ?? new Dictionary<string, JsonElement>()));
    }

    public override void Write(Utf8JsonWriter writer, List<ISpeechSequenceItem> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            switch (item)
            {
                case SpeechTextItem text:
                    writer.WriteStringValue(text.Text);
                    break;

                case SpeechCommandItem command:
                    writer.WriteStartArray();
                    writer.WriteStringValue(command.ClassName);
                    writer.WriteStartObject();
                    foreach (var (key, element) in command.Attributes)
                    {
                        writer.WritePropertyName(key);
                        element.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                    writer.WriteEndArray();
                    break;
            }
        }
        writer.WriteEndArray();
    }
}
