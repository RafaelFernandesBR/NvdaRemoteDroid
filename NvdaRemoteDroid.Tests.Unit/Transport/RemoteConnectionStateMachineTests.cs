using NvdaRemoteDroid.Core.Transport;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Transport;

public class RemoteConnectionStateMachineTests
{
    [Fact]
    public void HappyPath_DisconnectedToAuthenticated()
    {
        var sut = new RemoteConnectionStateMachine();

        sut.Apply(ConnectionEvent.ConnectRequested).ShouldBe(ConnectionState.Connecting);
        sut.Apply(ConnectionEvent.TlsReady).ShouldBe(ConnectionState.Handshaking);
        sut.Apply(ConnectionEvent.ChannelJoined).ShouldBe(ConnectionState.Authenticated);
    }

    [Fact]
    public void UntrustedCertificate_WaitsForTrustThenContinues()
    {
        var sut = new RemoteConnectionStateMachine();

        sut.Apply(ConnectionEvent.ConnectRequested);
        sut.Apply(ConnectionEvent.UntrustedCertificateEncountered).ShouldBe(ConnectionState.AwaitingCertificateTrust);
        sut.Apply(ConnectionEvent.CertificateTrusted).ShouldBe(ConnectionState.Connecting);
        sut.Apply(ConnectionEvent.TlsReady).ShouldBe(ConnectionState.Handshaking);
    }

    [Fact]
    public void IncorrectPassword_IsTerminalAndDoesNotAutoReconnect()
    {
        var sut = new RemoteConnectionStateMachine();

        sut.Apply(ConnectionEvent.ConnectRequested);
        sut.Apply(ConnectionEvent.TlsReady);
        sut.Apply(ConnectionEvent.IncorrectPassword).ShouldBe(ConnectionState.AuthFailed);

        // Uma falha de transporte subsequente NÃO deve sair do estado terminal AuthFailed.
        sut.Apply(ConnectionEvent.TransportFailed).ShouldBe(ConnectionState.AuthFailed);
        sut.ShouldAutoReconnect.ShouldBeFalse();
    }

    [Fact]
    public void VersionMismatch_IsTerminalAndDoesNotAutoReconnect()
    {
        var sut = new RemoteConnectionStateMachine();

        sut.Apply(ConnectionEvent.ConnectRequested);
        sut.Apply(ConnectionEvent.TlsReady);
        sut.Apply(ConnectionEvent.VersionMismatch).ShouldBe(ConnectionState.VersionMismatch);

        sut.Apply(ConnectionEvent.TransportFailed).ShouldBe(ConnectionState.VersionMismatch);
        sut.ShouldAutoReconnect.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Handshaking)]
    [InlineData(ConnectionState.Authenticated)]
    public void TransportFailure_FromNonTerminalStates_EntersErrorAndAllowsAutoReconnect(ConnectionState startState)
    {
        var sut = new RemoteConnectionStateMachine();
        DriveTo(sut, startState);

        sut.Apply(ConnectionEvent.TransportFailed).ShouldBe(ConnectionState.Error);
        sut.ShouldAutoReconnect.ShouldBeTrue();
    }

    [Fact]
    public void DisconnectRequested_FromAnyState_ReturnsToDisconnected()
    {
        var sut = new RemoteConnectionStateMachine();
        sut.Apply(ConnectionEvent.ConnectRequested);
        sut.Apply(ConnectionEvent.TlsReady);
        sut.Apply(ConnectionEvent.ChannelJoined);

        sut.Apply(ConnectionEvent.DisconnectRequested).ShouldBe(ConnectionState.Disconnected);
    }

    private static void DriveTo(RemoteConnectionStateMachine sut, ConnectionState state)
    {
        sut.Apply(ConnectionEvent.ConnectRequested);
        if (state == ConnectionState.Connecting)
            return;

        sut.Apply(ConnectionEvent.TlsReady);
        if (state == ConnectionState.Handshaking)
            return;

        sut.Apply(ConnectionEvent.ChannelJoined);
    }
}
