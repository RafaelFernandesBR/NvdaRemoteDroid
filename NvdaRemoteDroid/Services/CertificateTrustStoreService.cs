using System.Text.Json;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Services;

/// <summary>
/// Persiste fingerprints confiados (TOFU) em Preferences, como um dicionário host:porta ->
/// fingerprint serializado em JSON — simples o bastante para não precisar de SQLite nesta v1.
/// </summary>
public sealed class CertificateTrustStoreService : ICertificateTrustStore
{
    private const string PreferenceKey = "TrustedCertificates";

    private readonly Lock _gate = new();

    public bool IsTrusted(string hostAndPort, string fingerprintSha256)
    {
        lock (_gate)
        {
            var all = Load();
            return all.TryGetValue(hostAndPort, out var trusted) &&
                   string.Equals(trusted, fingerprintSha256, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Trust(string hostAndPort, string fingerprintSha256)
    {
        lock (_gate)
        {
            var all = Load();
            all[hostAndPort] = fingerprintSha256;
            Save(all);
        }
    }

    public void Revoke(string hostAndPort)
    {
        lock (_gate)
        {
            var all = Load();
            all.Remove(hostAndPort);
            Save(all);
        }
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        lock (_gate)
        {
            return Load();
        }
    }

    private static Dictionary<string, string> Load()
    {
        var json = Preferences.Default.Get(PreferenceKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    private static void Save(Dictionary<string, string> all) =>
        Preferences.Default.Set(PreferenceKey, JsonSerializer.Serialize(all));
}
