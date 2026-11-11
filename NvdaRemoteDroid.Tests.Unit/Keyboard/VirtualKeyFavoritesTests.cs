using System.Globalization;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Resources.Strings;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Keyboard;

[Collection(CultureCollection.Name)]
public class VirtualKeyFavoritesTests
{
    private static readonly VirtualKeyMapping NvdaInsert = VirtualKeyboardLayout.NvdaKeyMapping(VirtualKeyNvdaChoice.Insert);
    private static readonly VirtualKeyMapping NvdaCaps = VirtualKeyboardLayout.NvdaKeyMapping(VirtualKeyNvdaChoice.CapsLock);

    private static IReadOnlyList<VirtualKeyGroup> Sources(VirtualKeyMapping nvda) => VirtualKeyboardLayout.BuildSourceGroups(nvda);

    private static string IdOf(VirtualKeyMapping nvda, VirtualKeyboardGroupId group, Func<VirtualKey, bool> pick) =>
        Sources(nvda).Single(g => g.Id == group).Keys.Single(pick).Id;

    private static string LetterId(char letter) => IdOf(NvdaInsert, VirtualKeyboardGroupId.Letters, k => k.Label == letter.ToString());

    private static T WithCulture<T>(string culture, Func<T> action)
    {
        var previous = AppResources.Culture;
        AppResources.Culture = new CultureInfo(culture);
        try { return action(); }
        finally { AppResources.Culture = previous; }
    }

    [Fact]
    public void SerializeDeserialize_RoundTrips()
    {
        var favorites = new[]
        {
            new FavoriteKey(LetterId('C'), FavoriteModifiers.Ctrl),
            new FavoriteKey("Shortcuts:Shortcut_Title:", FavoriteModifiers.None),
        };

        VirtualKeyFavorites.Deserialize(VirtualKeyFavorites.Serialize(favorites)).ShouldBe(favorites);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2,3]")]
    public void Deserialize_BadInput_ReturnsEmpty_NeverThrows(string? json) =>
        VirtualKeyFavorites.Deserialize(json).ShouldBeEmpty();

    [Fact]
    public void Deserialize_DropsNullBlankAndDuplicateEntries()
    {
        var json = "[null,{\"KeyId\":\"\",\"Modifiers\":0},{\"KeyId\":\"A\",\"Modifiers\":1},{\"KeyId\":\"A\",\"Modifiers\":1}]";

        VirtualKeyFavorites.Deserialize(json).ShouldBe([new FavoriteKey("A", FavoriteModifiers.Ctrl)]);
    }

    [Fact]
    public void Resolve_SkipsUnknownIds_AndKeepsOrder()
    {
        var favorites = new[]
        {
            new FavoriteKey(LetterId('B'), FavoriteModifiers.None),
            new FavoriteKey("Letters:gone:", FavoriteModifiers.None),
            new FavoriteKey(LetterId('A'), FavoriteModifiers.None),
        };

        VirtualKeyFavorites.Resolve(Sources(NvdaInsert), favorites, NvdaInsert).Select(k => k.Label).ShouldBe(["B", "A"]);
    }

    [Fact]
    public void Resolve_WithoutExtraModifiers_ReturnsTheOriginalKey()
    {
        var resolved = VirtualKeyFavorites.Resolve(Sources(NvdaInsert), [new FavoriteKey(LetterId('C'), FavoriteModifiers.None)], NvdaInsert);

        resolved.Single().Modifiers.ShouldBeEmpty();
        resolved.Single().DisplayLabel.ShouldBe("C");
    }

    [Fact]
    public void Resolve_AddsModifiersInFixedOrder_WithLocalizedNameAndLabel()
    {
        var favorite = new FavoriteKey(LetterId('C'), FavoriteModifiers.Shift | FavoriteModifiers.Ctrl);

        var key = WithCulture("pt-BR", () => VirtualKeyFavorites.Resolve(Sources(NvdaInsert), [favorite], NvdaInsert).Single());

        key.Modifiers.ShouldBe([VirtualKeyboardLayout.Ctrl, VirtualKeyboardLayout.Shift]);
        key.Mapping.VkCode.ShouldBe(VirtualKeyCode.C);
        WithCulture("pt-BR", () => key.Name).ShouldBe("Ctrl + Shift + C");
        WithCulture("pt-BR", () => key.DisplayLabel).ShouldBe("Ctrl + Shift + C");
    }

    [Fact]
    public void Resolve_NvdaModifier_FollowsTheConfiguredNvdaKey()
    {
        var favorite = new FavoriteKey(LetterId('T'), FavoriteModifiers.Nvda);

        VirtualKeyFavorites.Resolve(Sources(NvdaInsert), [favorite], NvdaInsert).Single().Modifiers
            .ShouldBe([new VirtualKeyMapping(VirtualKeyCode.Insert, true)]);
        VirtualKeyFavorites.Resolve(Sources(NvdaCaps), [favorite], NvdaCaps).Single().Modifiers
            .ShouldBe([new VirtualKeyMapping(VirtualKeyCode.CapsLock, false)]);
    }

    [Fact]
    public void Resolve_WinModifier_KeepsTheExtendedFlag()
    {
        var favorite = new FavoriteKey(LetterId('E'), FavoriteModifiers.Win);

        VirtualKeyFavorites.Resolve(Sources(NvdaInsert), [favorite], NvdaInsert).Single().Modifiers
            .Single().Extended.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_DoesNotRepeatAModifierTheKeyAlreadyHas()
    {
        // "Título" já é NVDA+T: pedir NVDA de novo não pode duplicar o modificador.
        var titleId = IdOf(NvdaInsert, VirtualKeyboardGroupId.Shortcuts, k => k.NameKey == "Shortcut_Title");
        var favorite = new FavoriteKey(titleId, FavoriteModifiers.Nvda | FavoriteModifiers.Shift);

        var key = VirtualKeyFavorites.Resolve(Sources(NvdaInsert), [favorite], NvdaInsert).Single();

        key.Modifiers.ShouldBe([NvdaInsert, VirtualKeyboardLayout.Shift]);
    }

    [Fact]
    public void Add_IgnoresDuplicates_AndKeepsInsertionOrder()
    {
        var a = new FavoriteKey("A", FavoriteModifiers.None);
        var ctrlA = new FavoriteKey("A", FavoriteModifiers.Ctrl);

        var list = VirtualKeyFavorites.Add(VirtualKeyFavorites.Add([], a), ctrlA);

        list.ShouldBe([a, ctrlA]);
        // Duplicata devolve a mesma lista (o chamador usa isso para anunciar "já está").
        ReferenceEquals(VirtualKeyFavorites.Add(list, a), list).ShouldBeTrue();
    }

    [Fact]
    public void Add_SameModifiersInADifferentOrder_IsTheSameFavorite()
    {
        var one = new FavoriteKey("A", FavoriteModifiers.Ctrl | FavoriteModifiers.Alt);
        var other = new FavoriteKey("A", FavoriteModifiers.Alt | FavoriteModifiers.Ctrl);

        var list = VirtualKeyFavorites.Add([one], other);

        list.Count.ShouldBe(1);
    }

    [Fact]
    public void Remove_DropsOnlyTheMatchingFavorite()
    {
        var a = new FavoriteKey("A", FavoriteModifiers.None);
        var b = new FavoriteKey("B", FavoriteModifiers.None);

        VirtualKeyFavorites.Remove([a, b], a).ShouldBe([b]);
        VirtualKeyFavorites.Remove([a, b], new FavoriteKey("Z", FavoriteModifiers.None)).ShouldBe([a, b]);
    }

    [Fact]
    public void Move_SwapsNeighbours_AndStopsAtTheEdges()
    {
        var a = new FavoriteKey("A", FavoriteModifiers.None);
        var b = new FavoriteKey("B", FavoriteModifiers.None);
        var c = new FavoriteKey("C", FavoriteModifiers.None);
        IReadOnlyList<FavoriteKey> list = [a, b, c];

        VirtualKeyFavorites.Move(list, b, -1).ShouldBe([b, a, c]);
        VirtualKeyFavorites.Move(list, b, +1).ShouldBe([a, c, b]);
        ReferenceEquals(VirtualKeyFavorites.Move(list, a, -1), list).ShouldBeTrue();
        ReferenceEquals(VirtualKeyFavorites.Move(list, c, +1), list).ShouldBeTrue();
        ReferenceEquals(VirtualKeyFavorites.Move(list, new FavoriteKey("Z", FavoriteModifiers.None), +1), list).ShouldBeTrue();
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    public void ComposedNames_AreLocalizedAndHaveNoPlaceholders(string culture)
    {
        var favorites = new[]
        {
            new FavoriteKey(IdOf(NvdaInsert, VirtualKeyboardGroupId.Numpad, k => k.Label == "1"),
                FavoriteModifiers.Ctrl | FavoriteModifiers.Alt | FavoriteModifiers.Shift | FavoriteModifiers.Win | FavoriteModifiers.Nvda),
        };

        var name = WithCulture(culture, () => VirtualKeyFavorites.Resolve(Sources(NvdaInsert), favorites, NvdaInsert).Single().Name);

        name.ShouldNotContain("{0}");
        name.ShouldNotContain("RemoteControl_");
        name.Split(" + ").Length.ShouldBe(6);
    }

    [Fact]
    public void Build_WithFavorites_PutsThemInTheFavoritesGroup()
    {
        var groups = VirtualKeyboardLayout.Build(NvdaInsert, [new FavoriteKey(LetterId('C'), FavoriteModifiers.Ctrl)]);

        groups[0].Id.ShouldBe(VirtualKeyboardGroupId.Favorites);
        groups[0].Keys.Single().Mapping.VkCode.ShouldBe(VirtualKeyCode.C);
        groups[0].Keys.Single().Modifiers.ShouldBe([VirtualKeyboardLayout.Ctrl]);
    }
}
