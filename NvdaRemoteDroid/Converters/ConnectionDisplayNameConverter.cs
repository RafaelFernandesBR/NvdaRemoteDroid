using System.Globalization;
using NvdaRemoteDroid.Core.Transport;
using AppRes = NvdaRemoteDroid.Core.Resources.Strings.AppResources;

namespace NvdaRemoteDroid.Converters;

/// <summary>
/// Nome de uma conexão recente na lista: "Servidor relay público" para o relay oficial (em vez
/// de "nvdaremote.com:6837", que um leitor de tela soletra) e host:porta para as diretas.
/// </summary>
public sealed class ConnectionDisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ConnectionInfo { Host: RemoteDefaults.PublicRelayHost, Port: RemoteDefaults.Port } => AppRes.Connection_Recent_PublicRelay,
        ConnectionInfo info => info.HostAndPort,
        _ => value,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
