using System.Text;
using System.Text.Json;

namespace NvdaRemoteDroid.Core.Protocol;

/// <summary>
/// Descreve o FORMATO de uma mensagem JSON (nomes das chaves de primeiro nível + tipo de cada
/// valor), nunca os valores — é o que vai para o log de diagnóstico quando o parser não
/// reconhece uma mensagem, e o log é compartilhado livremente pelo usuário (a chave do canal
/// viaja em "channel"). Nunca lança.
/// </summary>
public static class RemoteMessageShape
{
    private const int MaxKeys = 16;
    private const int MaxKeyLength = 24;

    public static string Describe(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return DescribeKind(root);

            var builder = new StringBuilder();
            var count = 0;
            foreach (var property in root.EnumerateObject())
            {
                if (count == MaxKeys)
                {
                    builder.Append(",...");
                    break;
                }

                if (count > 0)
                    builder.Append(',');

                builder.Append(SanitizeKey(property.Name)).Append(':').Append(DescribeKind(property.Value));
                count++;
            }

            return builder.ToString();
        }
        catch (JsonException)
        {
            return "(json inválido)";
        }
    }

    private static string DescribeKind(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return element.ValueKind.ToString();

        // Para arrays, o tipo do primeiro item basta para diagnosticar (ex. ids como String).
        foreach (var item in element.EnumerateArray())
            return $"Array[{item.ValueKind}]";

        return "Array[]";
    }

    private static string SanitizeKey(string key)
    {
        var builder = new StringBuilder(Math.Min(key.Length, MaxKeyLength));
        foreach (var c in key)
        {
            if (builder.Length == MaxKeyLength)
                break;

            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '?');
        }

        return builder.ToString();
    }
}
