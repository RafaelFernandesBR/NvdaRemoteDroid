using System.Text.Json;
using NvdaRemoteDroid.Core.Protocol;
using NvdaRemoteDroid.Core.Protocol.Messages;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Protocol;

public class BrailleInputMessageTests
{
    [Fact]
    public void Serialize_ProducesTheFieldsAnNvdaLeaderSends()
    {
        var message = BrailleInputMessage.ForGlobalCommand("navigatorObject_nextInFlow", "swipeRight");

        using var json = JsonDocument.Parse(RemoteMessageSerializer.Serialize(message));
        var root = json.RootElement;

        root.EnumerateObject().Select(p => p.Name).Order().ShouldBe(["id", "model", "scriptPath", "source", "type"]);
        root.GetProperty("type").GetString().ShouldBe("braille_input");
        root.GetProperty("source").GetString().ShouldBe("nvdaRemoteDroid");
        root.GetProperty("model").GetString().ShouldBe("gesturePad");
        root.GetProperty("id").GetString().ShouldBe("swipeRight");
        root.GetProperty("scriptPath").EnumerateArray().Select(e => e.GetString())
            .ShouldBe(["globalCommands", "GlobalCommands", "navigatorObject_nextInFlow"]);
    }
}
