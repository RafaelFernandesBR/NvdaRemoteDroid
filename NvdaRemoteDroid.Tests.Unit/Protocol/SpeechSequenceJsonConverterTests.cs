using NvdaRemoteDroid.Core.Protocol;
using NvdaRemoteDroid.Core.Protocol.Messages;
using NvdaRemoteDroid.Core.Protocol.SpeechSequence;
using NvdaRemoteDroid.Core.Speech;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Protocol;

public class SpeechSequenceJsonConverterTests
{
    [Fact]
    public void TryParse_SequenceWithPlainStrings_ParsesAsTextItems()
    {
        var json = """{"type":"speak","sequence":["olá ","mundo"],"priority":0}""";

        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();

        var speak = message.ShouldBeOfType<SpeakMessage>();
        speak.Sequence.Count.ShouldBe(2);
        speak.Sequence[0].ShouldBeOfType<SpeechTextItem>().Text.ShouldBe("olá ");
        speak.Sequence[1].ShouldBeOfType<SpeechTextItem>().Text.ShouldBe("mundo");
    }

    [Fact]
    public void TryParse_SequenceWithKnownCommand_ParsesClassNameAndAttributes()
    {
        var json = """{"type":"speak","sequence":[["LangChangeCommand",{"lang":"en_US","isDefault":false}]],"priority":0}""";

        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();

        var speak = message.ShouldBeOfType<SpeakMessage>();
        var command = speak.Sequence[0].ShouldBeOfType<SpeechCommandItem>();
        command.ClassName.ShouldBe("LangChangeCommand");
        command.Attributes["lang"].GetString().ShouldBe("en_US");
        command.Attributes["isDefault"].GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void TryParse_SequenceWithUnknownCommandClass_IsIgnoredSafely()
    {
        var json = """{"type":"speak","sequence":["a",["SomeFutureCommand",{"x":1}],"b"],"priority":0}""";

        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();

        var speak = message.ShouldBeOfType<SpeakMessage>();
        speak.Sequence.Count.ShouldBe(3);
        speak.Sequence[1].ShouldBeOfType<SpeechCommandItem>().ClassName.ShouldBe("SomeFutureCommand");
    }

    [Fact]
    public void ToUtterances_ConcatenatesTextAndBreaksOnEndUtterance()
    {
        var sequence = new List<ISpeechSequenceItem>
        {
            new SpeechTextItem("Linha "),
            new SpeechTextItem("um"),
            new SpeechCommandItem(SpeechCommandItem.EndUtteranceCommand, new Dictionary<string, System.Text.Json.JsonElement>()),
            new SpeechTextItem("Linha dois"),
        };

        var utterances = SpeechSequenceTextBuilder.ToUtterances(sequence);

        utterances.ShouldBe(["Linha um", "Linha dois"]);
    }

    [Fact]
    public void ToUtterances_IgnoresOtherCommandsButKeepsText()
    {
        var sequence = new List<ISpeechSequenceItem>
        {
            new SpeechTextItem("texto"),
            new SpeechCommandItem("BreakCommand", new Dictionary<string, System.Text.Json.JsonElement>()),
        };

        var utterances = SpeechSequenceTextBuilder.ToUtterances(sequence);

        utterances.ShouldBe(["texto"]);
    }
}
