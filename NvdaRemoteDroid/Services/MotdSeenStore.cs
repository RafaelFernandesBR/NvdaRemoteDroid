using System.Security.Cryptography;
using System.Text;
using NvdaRemoteDroid.Core.Gateway;

namespace NvdaRemoteDroid.Services;

/// <summary>Deduplica mensagens "motd" via hash SHA-256 do texto, persistido em Preferences como lista de hashes vistos.</summary>
public sealed class MotdSeenStore : IMotdSeenStore
{
    private const string PreferenceKey = "SeenMotdHashes";
    private const int MaxTracked = 20;

    public bool HasSeen(string motdText) => LoadHashes().Contains(Hash(motdText));

    public void MarkSeen(string motdText)
    {
        var hashes = LoadHashes();
        var hash = Hash(motdText);
        if (!hashes.Remove(hash))
        {
            // nada a fazer: segue para inserir no topo
        }

        hashes.Insert(0, hash);
        if (hashes.Count > MaxTracked)
            hashes.RemoveRange(MaxTracked, hashes.Count - MaxTracked);

        Preferences.Default.Set(PreferenceKey, string.Join(',', hashes));
    }

    private static List<string> LoadHashes()
    {
        var stored = Preferences.Default.Get(PreferenceKey, string.Empty);
        return string.IsNullOrEmpty(stored) ? [] : stored.Split(',').ToList();
    }

    private static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
