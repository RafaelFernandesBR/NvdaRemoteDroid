using NvdaRemoteDroid.Core.Gateway;

namespace NvdaRemoteDroid.Services;

/// <summary>Área de transferência nativa via Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.</summary>
public sealed class ClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);

    public Task<string?> GetTextAsync() => Clipboard.Default.GetTextAsync();
}
