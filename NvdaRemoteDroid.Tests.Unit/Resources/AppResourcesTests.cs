using NvdaRemoteDroid.Core.Resources.Strings;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Resources;

public class AppResourcesTests
{
    [Fact]
    public void ConnectionTitle_PtBr_ResolvesEmbeddedResourceInsteadOfFallingBackToKey()
    {
        AppResources.Culture = new System.Globalization.CultureInfo("pt-BR");

        AppResources.Connection_Title.ShouldBe("Conectar ao NVDA");
    }

    [Fact]
    public void ConnectionTitle_English_ResolvesSatelliteResource()
    {
        AppResources.Culture = new System.Globalization.CultureInfo("en");

        AppResources.Connection_Title.ShouldBe("Connect to NVDA");
    }
}
