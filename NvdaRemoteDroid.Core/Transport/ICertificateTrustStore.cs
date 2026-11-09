namespace NvdaRemoteDroid.Core.Transport;

/// <summary>
/// Persiste fingerprints SHA-256 de certificados aceitos em modo inseguro (trust-on-first-use),
/// indexados por "host:porta" — mesma estratégia do TCPTransport.isFingerprintTrusted do NVDA.
/// </summary>
public interface ICertificateTrustStore
{
    bool IsTrusted(string hostAndPort, string fingerprintSha256);
    void Trust(string hostAndPort, string fingerprintSha256);
    void Revoke(string hostAndPort);
    IReadOnlyDictionary<string, string> GetAll();
}
