using NSubstitute;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.Core.ViewModels;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.ViewModels;

[Collection(CultureCollection.Name)]
public class SettingsViewModelBaseTests
{
    private readonly IConnectionPreferencesService _preferences = Substitute.For<IConnectionPreferencesService>();
    private readonly ISpeechOutputService _speech = Substitute.For<ISpeechOutputService>();

    public SettingsViewModelBaseTests()
    {
        _preferences.TtsEnginePackage.Returns(string.Empty);
        _preferences.SpeechRate.Returns(1.0f);
        _speech.GetInstalledEngines().Returns(
        [
            new TtsEngineInfo("com.google.tts", "Google"),
            new TtsEngineInfo("com.rhvoice", "RHVoice"),
        ]);
    }

    private TestSettingsViewModel CreateSut() => new(_preferences, _speech);

    [Fact]
    public void Engines_StartWithDeviceDefault_ThenInstalledEngines()
    {
        var sut = CreateSut();

        sut.Engines.Select(e => e.Key).ShouldBe(["", "com.google.tts", "com.rhvoice"]);
    }

    [Fact]
    public void Constructor_SelectsSavedEngine()
    {
        _preferences.TtsEnginePackage.Returns("com.rhvoice");

        var sut = CreateSut();

        sut.SelectedEngine!.Key.ShouldBe("com.rhvoice");
    }

    [Fact]
    public void Constructor_FallsBackToDefault_WhenSavedEngineIsNotInstalled()
    {
        _preferences.TtsEnginePackage.Returns("com.removed.engine");

        var sut = CreateSut();

        sut.SelectedEngine!.Key.ShouldBe(string.Empty);
        _preferences.Received().TtsEnginePackage = string.Empty;
        _speech.Received().Configure(string.Empty, 1.0f);
    }

    [Fact]
    public void Loading_WithInstalledEngine_DoesNotPersistNorConfigure()
    {
        _preferences.TtsEnginePackage.Returns("com.rhvoice");
        _preferences.SpeechRate.Returns(1.5f);

        CreateSut();

        _preferences.DidNotReceive().TtsEnginePackage = Arg.Any<string>();
        _preferences.DidNotReceive().SpeechRate = Arg.Any<float>();
        _speech.DidNotReceiveWithAnyArgs().Configure(default, default);
    }

    [Fact]
    public void ChangingEngine_PersistsConfiguresAndAnnounces()
    {
        var sut = CreateSut();
        sut.SpeechRate = 1.5;
        _speech.ClearReceivedCalls();

        sut.SelectedEngine = sut.Engines.Single(e => e.Key == "com.rhvoice");

        _preferences.Received().TtsEnginePackage = "com.rhvoice";
        _speech.Received().Configure("com.rhvoice", 1.5f);
        sut.Announcements.Count.ShouldBe(1);
        sut.Announcements[0].ShouldContain("RHVoice");
    }

    [Fact]
    public void ChangingRate_PersistsAndConfiguresWithSelectedEngine()
    {
        _preferences.TtsEnginePackage.Returns("com.google.tts");
        var sut = CreateSut();

        sut.SpeechRate = 1.3;

        _preferences.Received().SpeechRate = 1.3f;
        _speech.Received().Configure("com.google.tts", 1.3f);
    }

    [Theory]
    [InlineData(5.0, 2.0)]
    [InlineData(0.1, 0.5)]
    public void RateOutsideRange_IsClampedBeforePersisting(double input, double expected)
    {
        var sut = CreateSut();

        sut.SpeechRate = input;

        sut.SpeechRate.ShouldBe(expected);
        _preferences.Received().SpeechRate = (float)expected;
    }

    [Fact]
    public void TestVoiceCommand_SpeaksSample()
    {
        var sut = CreateSut();

        sut.TestVoiceCommand.Execute(null);

        _speech.Received(1).SpeakSample(Arg.Is<string>(s => !string.IsNullOrWhiteSpace(s)));
    }

    [Theory]
    [InlineData(0.2f, 0.5f)]
    [InlineData(3f, 2f)]
    [InlineData(1.2f, 1.2f)]
    [InlineData(float.NaN, 1.0f)]
    public void SpeechRateLimits_Clamp(float input, float expected) =>
        SpeechRateLimits.Clamp(input).ShouldBe(expected);

    private sealed class TestSettingsViewModel(IConnectionPreferencesService preferences, ISpeechOutputService speech)
        : SettingsViewModelBase(preferences, speech)
    {
        public List<string> Announcements { get; } = [];

        protected override void Announce(string message) => Announcements.Add(message);
    }
}
