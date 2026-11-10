using NSubstitute;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Transport;
using NvdaRemoteDroid.Core.ViewModels;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.ViewModels;

public class ConnectionViewModelBaseTests
{
    [Theory]
    [InlineData(ConnectionState.Disconnected, false)]
    [InlineData(ConnectionState.Connecting, true)]
    [InlineData(ConnectionState.AwaitingCertificateTrust, true)]
    [InlineData(ConnectionState.Handshaking, true)]
    [InlineData(ConnectionState.Authenticated, true)]
    [InlineData(ConnectionState.AuthFailed, false)]
    [InlineData(ConnectionState.VersionMismatch, false)]
    [InlineData(ConnectionState.Error, true)] // Error = backoff de reconexão automática em andamento; só Desconectar para o loop.
    public void IsConnectedOrConnecting_ReflectsEachConnectionState(ConnectionState state, bool expected)
    {
        var transport = Substitute.For<ITransportService>();
        var preferences = Substitute.For<IConnectionPreferencesService>();
        preferences.GetRecentConnections().Returns([]);

        var sut = new TestConnectionViewModel(transport, preferences);

        transport.StateChanged += Raise.Event<EventHandler<ConnectionState>>(transport, state);

        sut.IsConnectedOrConnecting.ShouldBe(expected);
    }

    [Fact]
    public void Constructor_SeedsIsConnectedOrConnecting_FromTransportStateAlreadyAuthenticated()
    {
        // Mesma regressão corrigida em RemoteControlViewModelBase: este VM também pode ser
        // (re)criado depois que o transporte singleton já mudou de estado.
        var transport = Substitute.For<ITransportService>();
        transport.State.Returns(ConnectionState.Authenticated);
        var preferences = Substitute.For<IConnectionPreferencesService>();
        preferences.GetRecentConnections().Returns([]);

        var sut = new TestConnectionViewModel(transport, preferences);

        sut.IsConnectedOrConnecting.ShouldBeTrue();
    }

    [Fact]
    public void CanConnect_IsFalse_WhenAlreadyConnectedOrConnecting()
    {
        // Evita que UseRecentConnectionCommand (ou um segundo toque em Conectar) dispare uma
        // segunda ConnectAsync concorrente enquanto já há uma conexão ativa/em andamento.
        var transport = Substitute.For<ITransportService>();
        var preferences = Substitute.For<IConnectionPreferencesService>();
        preferences.GetRecentConnections().Returns([]);

        var sut = new TestConnectionViewModel(transport, preferences) { Channel = "abc", UseRelay = true };
        sut.CanConnect.ShouldBeTrue();

        transport.StateChanged += Raise.Event<EventHandler<ConnectionState>>(transport, ConnectionState.Authenticated);

        sut.CanConnect.ShouldBeFalse();
    }

    private sealed class TestConnectionViewModel(ITransportService transportService, IConnectionPreferencesService preferencesService)
        : ConnectionViewModelBase(transportService, preferencesService)
    {
        protected override void AnnounceStatus(string statusText) { }
        protected override Task<bool> ConfirmCertificateTrustAsync(string fingerprintSha256) => Task.FromResult(false);
        protected override void OnAuthenticated() { }
    }
}
