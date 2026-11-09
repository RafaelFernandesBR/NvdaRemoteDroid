namespace NvdaRemoteDroid.Core.Gateway;

/// <summary>Abstrai a área de transferência nativa, para set_clipboard_text (enviado e recebido).</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string?> GetTextAsync();
}
