using NSubstitute;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.Core.Transport;
using NvdaRemoteDroid.Core.ViewModels;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.ViewModels;

public class RemoteControlViewModelBaseTests
{
    [Fact]
    public void Constructor_SeedsIsConnected_FromTransportStateAlreadyAuthenticated()
    {
        // Reproduz a regressão: o VM (transient) só é criado DEPOIS que o transporte (singleton)
        // já reportou Authenticated — o evento StateChanged one-shot já foi perdido, então
        // IsConnected precisa nascer certo a partir do estado atual, não só de eventos futuros.
        var transport = Substitute.For<ITransportService>();
        transport.State.Returns(ConnectionState.Authenticated);

        var sut = new TestRemoteControlViewModel(
            transport,
            Substitute.For<ISpeechOutputService>(),
            Substitute.For<IClipboardService>(),
            Substitute.For<IMotdSeenStore>());

        sut.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_SeedsIsConnected_FalseWhenTransportNotAuthenticated()
    {
        var transport = Substitute.For<ITransportService>();
        transport.State.Returns(ConnectionState.Disconnected);

        var sut = new TestRemoteControlViewModel(
            transport,
            Substitute.For<ISpeechOutputService>(),
            Substitute.For<IClipboardService>(),
            Substitute.For<IMotdSeenStore>());

        sut.IsConnected.ShouldBeFalse();
    }

    private sealed class TestRemoteControlViewModel(
        ITransportService transportService,
        ISpeechOutputService speechOutputService,
        IClipboardService clipboardService,
        IMotdSeenStore motdSeenStore)
        : RemoteControlViewModelBase(transportService, speechOutputService, clipboardService, motdSeenStore)
    {
        protected override void AnnounceStatus(string text) { }
        protected override void ShowMotd(string motdText) { }
        protected override void PlayTone(double hz, int lengthMs) { }
    }
}
