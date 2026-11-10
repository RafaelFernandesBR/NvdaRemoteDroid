using System.Text.Json;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Services;

/// <summary>
/// Persiste as últimas conexões (host/porta/canal) e a tecla NVDA preferida via Preferences.
/// Simplificação da v1: a chave do canal fica em Preferences (armazenamento privado do app,
/// mas não criptografado) em vez de SecureStorage — aceitável porque é só uma senha de sala,
/// não uma credencial de conta; revisar para SecureStorage se o escopo crescer.
/// </summary>
public sealed class ConnectionPreferencesService : IConnectionPreferencesService
{
    private const string RecentConnectionsKey = "RecentConnections";
    private const string NvdaKeyPreferenceKey = "NvdaKeyChoice";
    private const string KeyboardGroupPreferenceKey = "KeyboardGroup";
    private const int MaxRecentConnections = 5;

    private sealed record StoredConnection(string Host, int Port, string Channel, bool Insecure);

    public IReadOnlyList<ConnectionInfo> GetRecentConnections()
    {
        var json = Preferences.Default.Get(RecentConnectionsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return [];

        var stored = JsonSerializer.Deserialize<List<StoredConnection>>(json) ?? [];
        return stored.Select(s => new ConnectionInfo(s.Host, s.Port, s.Channel, s.Insecure)).ToList();
    }

    public void SaveRecentConnection(ConnectionInfo connectionInfo)
    {
        var existing = GetRecentConnections()
            .Where(c => c.HostAndPort != connectionInfo.HostAndPort)
            .Take(MaxRecentConnections - 1)
            .ToList();

        var updated = new List<ConnectionInfo> { connectionInfo };
        updated.AddRange(existing);

        var stored = updated.Select(c => new StoredConnection(c.Host, c.Port, c.Channel, c.Insecure));
        Preferences.Default.Set(RecentConnectionsKey, JsonSerializer.Serialize(stored));
    }

    public void ClearRecentConnections() => Preferences.Default.Remove(RecentConnectionsKey);

    public VirtualKeyNvdaChoice NvdaKey
    {
        get
        {
            var stored = Preferences.Default.Get(NvdaKeyPreferenceKey, nameof(VirtualKeyNvdaChoice.Insert));
            return Enum.TryParse<VirtualKeyNvdaChoice>(stored, out var choice) ? choice : VirtualKeyNvdaChoice.Insert;
        }
        set => Preferences.Default.Set(NvdaKeyPreferenceKey, value.ToString());
    }

    public Core.Keyboard.VirtualKeyboardGroupId KeyboardGroup
    {
        get
        {
            var stored = Preferences.Default.Get(KeyboardGroupPreferenceKey, string.Empty);
            return Enum.TryParse<Core.Keyboard.VirtualKeyboardGroupId>(stored, out var group) && Enum.IsDefined(group)
                ? group
                : Core.Keyboard.VirtualKeyboardGroupId.Shortcuts;
        }
        set => Preferences.Default.Set(KeyboardGroupPreferenceKey, value.ToString());
    }
}
