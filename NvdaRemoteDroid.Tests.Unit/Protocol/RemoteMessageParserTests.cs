using NvdaRemoteDroid.Core.Protocol;
using NvdaRemoteDroid.Core.Protocol.Messages;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Protocol;

public class RemoteMessageParserTests
{
    [Fact]
    public void TryParse_ChannelJoined_ParsesUserIdsAndClients()
    {
        var json = """{"type":"channel_joined","channel":"abc123","user_ids":[2,5],"clients":[{"id":2,"connection_type":"slave"}]}""";

        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();

        var channelJoined = message.ShouldBeOfType<ChannelJoinedMessage>();
        channelJoined.Channel.ShouldBe("abc123");
        channelJoined.UserIds.ShouldBe([2, 5]);
        channelJoined.Clients.ShouldNotBeNull();
        channelJoined.Clients![0].ConnectionType.ShouldBe("slave");
    }

    [Fact]
    public void TryParse_ErrorIncorrectPassword_ParsesMessageField()
    {
        var json = """{"type":"error","message":"incorrect_password"}""";

        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();

        message.ShouldBeOfType<ErrorMessage>().Message.ShouldBe("incorrect_password");
    }

    [Fact]
    public void TryParse_Ping_ReturnsPingMessage()
    {
        RemoteMessageParser.TryParse("""{"type":"ping"}""", out var message).ShouldBeTrue();
        message.ShouldBeOfType<PingMessage>();
    }

    [Fact]
    public void TryParse_VersionMismatch_ReturnsVersionMismatchMessage()
    {
        RemoteMessageParser.TryParse("""{"type":"version_mismatch"}""", out var message).ShouldBeTrue();
        message.ShouldBeOfType<VersionMismatchMessage>();
    }

    [Fact]
    public void TryParse_UnknownType_ReturnsUnknownMessageInsteadOfThrowing()
    {
        RemoteMessageParser.TryParse("""{"type":"generate_key"}""", out var message).ShouldBeTrue();

        var unknown = message.ShouldBeOfType<UnknownMessage>();
        unknown.Type.ShouldBe("generate_key");
    }

    [Fact]
    public void TryParse_SpeakMissingSequence_ReturnsUnknownMessageInsteadOfThrowing()
    {
        // "sequence" é obrigatório para SpeakMessage (ver relatório §6); sem ele, não deve lançar.
        RemoteMessageParser.TryParse("""{"type":"speak","priority":0}""", out var message).ShouldBeTrue();

        message.ShouldBeOfType<UnknownMessage>();
    }

    [Fact]
    public void TryParse_SetClipboardTextMissingText_ReturnsUnknownMessage()
    {
        RemoteMessageParser.TryParse("""{"type":"set_clipboard_text"}""", out var message).ShouldBeTrue();

        message.ShouldBeOfType<UnknownMessage>();
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalseAndUnknownMessage()
    {
        RemoteMessageParser.TryParse("{not json", out var message).ShouldBeFalse();
        message.ShouldBeOfType<UnknownMessage>();
    }

    [Fact]
    public void TryParse_MissingTypeField_ReturnsUnknownMessage()
    {
        RemoteMessageParser.TryParse("""{"foo":"bar"}""", out var message).ShouldBeTrue();
        message.ShouldBeOfType<UnknownMessage>();
    }

    [Fact]
    public void TryParse_ToneMessage_ParsesAllFields()
    {
        RemoteMessageParser.TryParse("""{"type":"tone","hz":500.0,"length":100,"left":50,"right":50}""", out var message).ShouldBeTrue();

        var tone = message.ShouldBeOfType<ToneMessage>();
        tone.Hz.ShouldBe(500.0);
        tone.Length.ShouldBe(100);
    }

    [Fact]
    public void TryParse_ExtraUnknownFields_AreIgnoredSafely()
    {
        RemoteMessageParser.TryParse("""{"type":"ping","unexpectedField":"whatever"}""", out var message).ShouldBeTrue();
        message.ShouldBeOfType<PingMessage>();
    }
}
