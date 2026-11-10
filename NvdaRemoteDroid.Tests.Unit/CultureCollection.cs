namespace NvdaRemoteDroid.Tests.Unit;

/// <summary>
/// AppResources.Culture é estático: toda classe de teste que o altera (ou que confere texto
/// localizado) entra nesta coleção, para o xUnit não rodá-las em paralelo umas com as outras.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CultureCollection
{
    public const string Name = "AppResources culture";
}
