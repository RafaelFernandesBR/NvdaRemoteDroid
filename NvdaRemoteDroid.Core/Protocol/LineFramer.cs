using System.Text;

namespace NvdaRemoteDroid.Core.Protocol;

/// <summary>
/// Framing do protocolo NVDA Remote: uma mensagem JSON UTF-8 por linha, terminada em '\n',
/// sem prefixo de tamanho. Opera sobre Stream puro (testável com MemoryStream) e lida com
/// linhas/caracteres multibyte partidos entre leituras sucessivas do socket.
/// </summary>
public sealed class LineFramer
{
    private const byte Separator = (byte)'\n';
    private readonly byte[] _readBuffer = new byte[8192];
    private readonly List<byte> _pending = [];

    public async IAsyncEnumerable<string> ReadLinesAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(_readBuffer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (read == 0)
                yield break; // conexão fechada pelo peer

            _pending.AddRange(_readBuffer.AsSpan(0, read).ToArray());

            int lineStart = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i] != Separator)
                    continue;

                int length = i - lineStart;
                if (length > 0)
                    yield return Encoding.UTF8.GetString(CollectionsMarshal(_pending, lineStart, length));

                lineStart = i + 1;
            }

            if (lineStart > 0)
                _pending.RemoveRange(0, lineStart);
        }
    }

    public static string FrameLine(string json) => json + "\n";

    private static byte[] CollectionsMarshal(List<byte> source, int start, int length)
    {
        var slice = new byte[length];
        source.CopyTo(start, slice, 0, length);
        return slice;
    }
}
