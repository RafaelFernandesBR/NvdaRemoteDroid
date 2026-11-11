using NSubstitute;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.ViewModels;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.ViewModels;

[Collection(CultureCollection.Name)]
public class FavoriteKeysViewModelBaseTests
{
    private readonly IConnectionPreferencesService _preferences = Substitute.For<IConnectionPreferencesService>();
    private IReadOnlyList<FavoriteKey> _stored = [];

    public FavoriteKeysViewModelBaseTests()
    {
        _preferences.NvdaKey.Returns(VirtualKeyNvdaChoice.Insert);
        _preferences.FavoriteKeys.Returns(_ => _stored);
        _preferences.When(p => p.FavoriteKeys = Arg.Any<IReadOnlyList<FavoriteKey>>())
            .Do(call => _stored = call.Arg<IReadOnlyList<FavoriteKey>>());
    }

    private TestViewModel CreateSut() => new(_preferences);

    private static void Pick(TestViewModel sut, VirtualKeyboardGroupId group, string label)
    {
        sut.SelectedGroup = sut.Groups.Single(g => g.Id == group);
        sut.SelectedKey = sut.Keys.Single(k => k.Label == label);
    }

    [Fact]
    public void Groups_ListRealKeyGroups_WithoutFavoritesNorGestures()
    {
        var sut = CreateSut();

        sut.Groups.Select(g => g.Id).ShouldNotContain(VirtualKeyboardGroupId.Favorites);
        sut.Groups.Select(g => g.Id).ShouldNotContain(VirtualKeyboardGroupId.Gestures);
        sut.Groups.ShouldNotBeEmpty();
    }

    [Fact]
    public void ChoosingAGroup_FillsKeys_AndClearsTheSelectedKey()
    {
        var sut = CreateSut();
        Pick(sut, VirtualKeyboardGroupId.Letters, "C");

        sut.SelectedGroup = sut.Groups.Single(g => g.Id == VirtualKeyboardGroupId.Function);

        sut.Keys.ShouldBe(sut.SelectedGroup.Keys);
        sut.SelectedKey.ShouldBeNull();
    }

    [Fact]
    public void Add_PersistsTheFavoriteWithModifiers_ResetsTheSwitches_AndAnnounces()
    {
        var sut = CreateSut();
        Pick(sut, VirtualKeyboardGroupId.Letters, "C");
        sut.UseCtrl = true;
        sut.UseShift = true;

        sut.AddCommand.Execute(null);

        _stored.Count.ShouldBe(1);
        _stored[0].Modifiers.ShouldBe(FavoriteModifiers.Ctrl | FavoriteModifiers.Shift);
        sut.Entries.Single().Favorite.ShouldBe(_stored[0]);
        (sut.UseCtrl || sut.UseAlt || sut.UseShift || sut.UseWin || sut.UseNvda).ShouldBeFalse();
        sut.Announcements.Single().ShouldBe(string.Format(AppResources.FavoriteKeys_Added, sut.Entries.Single().Name));
    }

    [Fact]
    public void Add_WithoutAKey_DoesNothingButAnnounces()
    {
        var sut = CreateSut();

        sut.AddCommand.Execute(null);

        _stored.ShouldBeEmpty();
        sut.Announcements.Single().ShouldBe(AppResources.FavoriteKeys_NoKeySelected);
    }

    [Fact]
    public void Add_Duplicate_DoesNotPersistAgain_AndAnnouncesItAlreadyExists()
    {
        var sut = CreateSut();
        Pick(sut, VirtualKeyboardGroupId.Letters, "C");
        sut.AddCommand.Execute(null);
        sut.Announcements.Clear();
        _preferences.ClearReceivedCalls();

        sut.AddCommand.Execute(null);

        _stored.Count.ShouldBe(1);
        _preferences.DidNotReceive().FavoriteKeys = Arg.Any<IReadOnlyList<FavoriteKey>>();
        sut.Announcements.Single().ShouldBe(string.Format(AppResources.FavoriteKeys_AlreadyExists, sut.Entries.Single().Name));
    }

    [Fact]
    public void Remove_PersistsAndAnnounces()
    {
        var sut = CreateSut();
        Pick(sut, VirtualKeyboardGroupId.Letters, "A");
        sut.AddCommand.Execute(null);
        Pick(sut, VirtualKeyboardGroupId.Letters, "B");
        sut.AddCommand.Execute(null);
        var first = sut.Entries[0];
        sut.Announcements.Clear();

        sut.RemoveCommand.Execute(first);

        _stored.Count.ShouldBe(1);
        sut.Entries.Single().Favorite.ShouldBe(_stored[0]);
        sut.Announcements.Single().ShouldBe(string.Format(AppResources.FavoriteKeys_Removed, first.Name));
    }

    [Fact]
    public void Move_ReordersPersists_AndFlagsTheEdges()
    {
        var sut = CreateSut();
        foreach (var letter in new[] { "A", "B", "C" })
        {
            Pick(sut, VirtualKeyboardGroupId.Letters, letter);
            sut.AddCommand.Execute(null);
        }

        sut.Entries.Select(e => (e.IsFirst, e.IsLast)).ShouldBe([(true, false), (false, false), (false, true)]);

        var c = sut.Entries[2];
        sut.Announcements.Clear();
        sut.MoveUpCommand.Execute(c);

        _stored.Select(f => f.KeyId).ShouldBe(sut.Entries.Select(e => e.Favorite.KeyId));
        sut.Entries.Select(e => e.Name).ShouldBe(["A", "C", "B"]);
        sut.Announcements.Single().ShouldBe(string.Format(AppResources.FavoriteKeys_Moved, "C", 2, 3));
    }

    [Fact]
    public void Move_AtTheEdge_DoesNotPersistNorAnnounce()
    {
        var sut = CreateSut();
        Pick(sut, VirtualKeyboardGroupId.Letters, "A");
        sut.AddCommand.Execute(null);
        _preferences.ClearReceivedCalls();
        sut.Announcements.Clear();

        sut.MoveUpCommand.Execute(sut.Entries.Single());
        sut.MoveDownCommand.Execute(sut.Entries.Single());

        _preferences.DidNotReceive().FavoriteKeys = Arg.Any<IReadOnlyList<FavoriteKey>>();
        sut.Announcements.ShouldBeEmpty();
    }

    [Fact]
    public void Entries_DropOrphanFavoritesWhoseKeyNoLongerExists_AndPersistTheCleanList()
    {
        var valid = new FavoriteKey("Letters::A", FavoriteModifiers.None);
        _stored = [new FavoriteKey("Letters:gone:", FavoriteModifiers.None), valid];

        var sut = CreateSut();

        sut.Entries.Single().Favorite.ShouldBe(valid);
        _stored.ShouldBe([valid]);
    }

    [Fact]
    public void EntryButtonNames_AreLocalizedPerFavorite()
    {
        var sut = CreateSut();
        Pick(sut, VirtualKeyboardGroupId.Letters, "C");
        sut.UseCtrl = true;
        sut.AddCommand.Execute(null);

        var entry = sut.Entries.Single();

        entry.RemoveName.ShouldBe(string.Format(AppResources.FavoriteKeys_Remove_Format, entry.Name));
        entry.MoveUpName.ShouldBe(string.Format(AppResources.FavoriteKeys_MoveUp_Format, entry.Name));
        entry.MoveDownName.ShouldBe(string.Format(AppResources.FavoriteKeys_MoveDown_Format, entry.Name));
    }

    private sealed class TestViewModel(IConnectionPreferencesService preferences) : FavoriteKeysViewModelBase(preferences)
    {
        public List<string> Announcements { get; } = [];

        protected override void Announce(string message) => Announcements.Add(message);
    }
}
