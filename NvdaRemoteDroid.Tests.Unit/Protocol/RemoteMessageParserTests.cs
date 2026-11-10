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
        channelJoined.UserIds.ShouldBe([2L, 5L]);
        channelJoined.Clients.Count.ShouldBe(1);
        channelJoined.Clients[0].ConnectionType.ShouldBe("slave");
        channelJoined.Clients[0].IsFollower.ShouldBeTrue();
    }

    // Regressão do log de campo: "Mensagem desconhecida do relay: type=channel_joined" deixava a
    // conexão presa em Handshaking (teclado inteiro desabilitado). O "type" sozinho tem de bastar,
    // qualquer que seja o formato do resto.
    [Theory]
    [InlineData("""{"type":"channel_joined"}""")]
    [InlineData("""{"type":"channel_joined","channel":"abc"}""")]
    [InlineData("""{"type":"channel_joined","channel":"abc","origin":7,"clients":[]}""")]
    [InlineData("""{"type":"channel_joined","channel":null,"user_ids":null,"clients":null}""")]
    [InlineData("""{"type":"channel_joined","user_ids":"oops","clients":{"id":1}}""")]
    [InlineData("""{"type":"channel_joined","user_ids":[{"x":1},true,null],"clients":[1,"a",{"connection_type":"slave"}]}""")]
    public void TryParse_ChannelJoined_IsRecognizedWhateverTheShapeOfOptionalFields(string json)
    {
        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();

        var channelJoined = message.ShouldBeOfType<ChannelJoinedMessage>();
        channelJoined.Channel.ShouldNotBeNull();
        channelJoined.UserIds.ShouldBeEmpty();
        channelJoined.Clients.ShouldBeEmpty();
    }

    [Fact]
    public void TryParse_ChannelJoined_AcceptsIdsAsLongFloatOrString()
    {
        var json = """{"type":"channel_joined","user_ids":[9007199254740993,12.0,"34"],"clients":[{"id":"56","connection_type":"master"},{"id":78.0}]}""";

        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();

        var channelJoined = message.ShouldBeOfType<ChannelJoinedMessage>();
        channelJoined.UserIds.ShouldBe([9007199254740993L, 12L, 34L]);
        channelJoined.Clients.Select(c => c.Id).ShouldBe([56L, 78L]);
        channelJoined.Clients[0].IsFollower.ShouldBeFalse();
        channelJoined.Clients[1].ConnectionType.ShouldBe(string.Empty);
    }

    [Fact]
    public void TryParse_ClientJoinedAndLeft_ParseWithOrWithoutOptionalFields()
    {
        RemoteMessageParser.TryParse("""{"type":"client_joined","user_id":3,"client":{"id":3,"connection_type":"slave"}}""", out var joined).ShouldBeTrue();
        var clientJoined = joined.ShouldBeOfType<ClientJoinedMessage>();
        clientJoined.UserId.ShouldBe(3L);
        clientJoined.Client!.IsFollower.ShouldBeTrue();

        RemoteMessageParser.TryParse("""{"type":"client_left"}""", out var left).ShouldBeTrue();
        var clientLeft = left.ShouldBeOfType<ClientLeftMessage>();
        clientLeft.UserId.ShouldBeNull();
        clientLeft.Client.ShouldBeNull();
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"texto\"")]
    [InlineData("42")]
    public void TryParse_JsonThatIsNotAnObject_ReturnsUnknownMessageInsteadOfThrowing(string json)
    {
        RemoteMessageParser.TryParse(json, out var message).ShouldBeTrue();
        message.ShouldBeOfType<UnknownMessage>();
    }

    [Fact]
    public void MessageShape_DescribesKeysAndKinds_WithoutLeakingValues()
    {
        var shape = RemoteMessageShape.Describe("""{"type":"channel_joined","channel":"segredo123","user_ids":["9"],"clients":[],"n":1}""");

        shape.ShouldBe("type:String,channel:String,user_ids:Array[String],clients:Array[],n:Number");
        shape.ShouldNotContain("segredo123");
    }

    [Fact]
    public void MessageShape_NeverThrows_OnInvalidJsonOrHostileKeys()
    {
        RemoteMessageShape.Describe("{not json").ShouldBe("(json inválido)");
        RemoteMessageShape.Describe("[1]").ShouldBe("Array[Number]");
        RemoteMessageShape.Describe("{\"a\\nb c\":1}").ShouldBe("a?b?c:Number");
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
