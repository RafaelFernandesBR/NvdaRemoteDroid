using NvdaRemoteDroid.Core.Resources.Strings;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Resources;

[Collection(CultureCollection.Name)]
public class AppResourcesTests
{
    [Fact]
    public void EveryKey_ExistsInBothLanguages_AndHasADesignerProperty()
    {
        // O Designer é mantido à mão: este teste pega a chave esquecida em um dos .resx ou sem
        // a propriedade correspondente.
        var neutral = ReadKeys(new System.Globalization.CultureInfo("pt-BR"));
        var english = ReadKeys(new System.Globalization.CultureInfo("en"));

        english.ShouldBe(neutral, ignoreOrder: true);

        var properties = typeof(AppResources)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToHashSet();

        neutral.Where(key => !properties.Contains(key)).ShouldBeEmpty();
    }

    private static List<string> ReadKeys(System.Globalization.CultureInfo culture)
    {
        var resourceSet = AppResources.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        resourceSet.ShouldNotBeNull($"sem recursos para {culture.Name}");

        return resourceSet.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).ToList();
    }

    [Theory]
    [InlineData("pt-BR", "Desconectado", "Falha de conexão, tentando novamente...")]
    [InlineData("en", "Disconnected", "Connection failed, retrying...")]
    public void ConnectionStatus_IsLocalized(string cultureName, string disconnected, string error)
    {
        AppResources.Culture = new System.Globalization.CultureInfo(cultureName);

        NvdaRemoteDroid.Core.Transport.ConnectionStateDescriber.Describe(NvdaRemoteDroid.Core.Transport.ConnectionState.Disconnected).ShouldBe(disconnected);
        NvdaRemoteDroid.Core.Transport.ConnectionStateDescriber.Describe(NvdaRemoteDroid.Core.Transport.ConnectionState.Error).ShouldBe(error);
    }

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
