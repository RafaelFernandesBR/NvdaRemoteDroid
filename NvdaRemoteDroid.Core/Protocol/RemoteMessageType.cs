namespace NvdaRemoteDroid.Core.Protocol;

/// <summary>Valores literais do campo "type" trocados no fio do protocolo NVDA Remote.</summary>
public static class RemoteMessageType
{
    public const string ProtocolVersion = "protocol_version";
    public const string Join = "join";
    public const string ChannelJoined = "channel_joined";
    public const string ClientJoined = "client_joined";
    public const string ClientLeft = "client_left";
    public const string GenerateKey = "generate_key";
    public const string Key = "key";
    public const string Speak = "speak";
    public const string Cancel = "cancel";
    public const string PauseSpeech = "pause_speech";
    public const string Tone = "tone";
    public const string Wave = "wave";
    public const string SendSas = "send_SAS";
    public const string Display = "display";
    public const string BrailleInput = "braille_input";
    public const string SetBrailleInfo = "set_braille_info";
    public const string SetDisplaySize = "set_display_size";
    public const string SetClipboardText = "set_clipboard_text";
    public const string Motd = "motd";
    public const string VersionMismatch = "version_mismatch";
    public const string Ping = "ping";
    public const string Error = "error";
}
