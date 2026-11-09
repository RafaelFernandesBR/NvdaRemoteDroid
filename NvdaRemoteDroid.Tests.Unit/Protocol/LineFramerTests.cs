using System.Text;
using NvdaRemoteDroid.Core.Protocol;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Protocol;

public class LineFramerTests
{
    [Fact]
    public async Task ReadLinesAsync_SingleCompleteLine_YieldsOneLine()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"type\":\"ping\"}\n"));
        var framer = new LineFramer();

        var lines = await Collect(framer, stream);

        lines.ShouldBe(["{\"type\":\"ping\"}"]);
    }

    [Fact]
    public async Task ReadLinesAsync_MultipleLinesInOneChunk_YieldsAllInOrder()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("a\nb\nc\n"));
        var framer = new LineFramer();

        var lines = await Collect(framer, stream);

        lines.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public async Task ReadLinesAsync_LineSplitAcrossMultipleReads_IsReassembled()
    {
        // ChunkedStream força leituras de poucos bytes por vez, simulando um socket real.
        var payload = "{\"type\":\"speak\",\"sequence\":[\"olá mundo\"],\"priority\":0}\n";
        using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(payload), chunkSize: 3);
        var framer = new LineFramer();

        var lines = await Collect(framer, stream);

        lines.ShouldBe([payload.TrimEnd('\n')]);
    }

    [Fact]
    public async Task ReadLinesAsync_MultibyteUtf8CharacterSplitAcrossReads_IsDecodedCorrectly()
    {
        // "é" (0xC3 0xA9 em UTF-8) é forçado a cair exatamente na borda de dois reads de 1 byte.
        var payload = "café\n";
        using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(payload), chunkSize: 1);
        var framer = new LineFramer();

        var lines = await Collect(framer, stream);

        lines.ShouldBe(["café"]);
    }

    [Fact]
    public async Task ReadLinesAsync_EmptyLineIsSkipped()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("a\n\nb\n"));
        var framer = new LineFramer();

        var lines = await Collect(framer, stream);

        lines.ShouldBe(["a", "b"]);
    }

    private static async Task<List<string>> Collect(LineFramer framer, Stream stream)
    {
        var result = new List<string>();
        await foreach (var line in framer.ReadLinesAsync(stream))
            result.Add(line);
        return result;
    }

    /// <summary>Stream de teste que só entrega até chunkSize bytes por ReadAsync, para forçar framing partido.</summary>
    private sealed class ChunkedStream(byte[] data, int chunkSize) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var remaining = data.Length - _position;
            if (remaining <= 0)
                return ValueTask.FromResult(0);

            var toCopy = Math.Min(chunkSize, Math.Min(remaining, buffer.Length));
            data.AsSpan(_position, toCopy).CopyTo(buffer.Span);
            _position += toCopy;
            return ValueTask.FromResult(toCopy);
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
