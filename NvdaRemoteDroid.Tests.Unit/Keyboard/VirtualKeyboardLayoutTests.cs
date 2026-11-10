using System.Globalization;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Resources.Strings;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Keyboard;

[Collection(CultureCollection.Name)]
public class VirtualKeyboardLayoutTests
{
    private static readonly VirtualKeyMapping NvdaInsert = VirtualKeyboardLayout.NvdaKeyMapping(VirtualKeyNvdaChoice.Insert);

    private static IReadOnlyList<VirtualKeyGroup> Groups => VirtualKeyboardLayout.Build(NvdaInsert);

    private static VirtualKeyGroup Group(VirtualKeyboardGroupId id) => Groups.Single(g => g.Id == id);

    [Fact]
    public void Build_ReturnsEveryGroupOnce_AndNoneIsEmpty()
    {
        Groups.Select(g => g.Id).ShouldBe(Enum.GetValues<VirtualKeyboardGroupId>());
        Groups.ShouldAllBe(g => g.Keys.Count > 0);
    }

    [Fact]
    public void Build_NoDuplicateKeyInsideAGroup()
    {
        foreach (var group in Groups)
        {
            var signatures = group.Keys
                .Select(k => $"{k.Mapping}|{string.Join('+', k.Modifiers)}")
                .ToList();

            signatures.Distinct().Count().ShouldBe(signatures.Count, $"grupo {group.Id} tem tecla repetida");
        }
    }

    [Fact]
    public void Build_CoversTheWholeAlphabetDigitsAndFunctionKeys()
    {
        Group(VirtualKeyboardGroupId.Letters).Keys.Select(k => k.Label)
            .ShouldBe(Enumerable.Range('A', 26).Select(c => ((char)c).ToString()));

        var numbers = Group(VirtualKeyboardGroupId.NumbersAndSymbols).Keys;
        for (var digit = 0; digit <= 9; digit++)
            numbers.ShouldContain(k => k.Label == digit.ToString() && (int)k.Mapping.VkCode == 0x30 + digit);

        var function = Group(VirtualKeyboardGroupId.Function).Keys;
        for (var f = 1; f <= 12; f++)
            function.ShouldContain(k => k.Label == $"F{f}" && (int)k.Mapping.VkCode == 0x70 + f - 1);
    }

    [Theory]
    [InlineData(VirtualKeyCode.Up)]
    [InlineData(VirtualKeyCode.Down)]
    [InlineData(VirtualKeyCode.Left)]
    [InlineData(VirtualKeyCode.Right)]
    [InlineData(VirtualKeyCode.Home)]
    [InlineData(VirtualKeyCode.End)]
    [InlineData(VirtualKeyCode.PageUp)]
    [InlineData(VirtualKeyCode.PageDown)]
    [InlineData(VirtualKeyCode.Insert)]
    [InlineData(VirtualKeyCode.Delete)]
    [InlineData(VirtualKeyCode.LeftWindows)]
    [InlineData(VirtualKeyCode.Apps)]
    [InlineData(VirtualKeyCode.Divide)]
    [InlineData(VirtualKeyCode.NumLock)]
    public void Build_ExtendedKeys_AlwaysCarryTheExtendedFlag(VirtualKeyCode vkCode)
    {
        var occurrences = Groups.SelectMany(g => g.Keys).Where(k => k.Mapping.VkCode == vkCode).ToList();

        occurrences.ShouldNotBeEmpty();
        occurrences.ShouldAllBe(k => k.Mapping.Extended);
    }

    [Fact]
    public void Build_EnterIsExtendedOnlyOnTheNumpad()
    {
        Group(VirtualKeyboardGroupId.Navigation).Keys.Single(k => k.Mapping.VkCode == VirtualKeyCode.Enter).Mapping.Extended.ShouldBeFalse();
        Group(VirtualKeyboardGroupId.Numpad).Keys.Single(k => k.Mapping.VkCode == VirtualKeyCode.Enter).Mapping.Extended.ShouldBeTrue();
    }

    [Theory]
    [InlineData(VirtualKeyNvdaChoice.Insert, VirtualKeyCode.Insert, true)]
    [InlineData(VirtualKeyNvdaChoice.CapsLock, VirtualKeyCode.CapsLock, false)]
    public void Build_NvdaShortcuts_UseTheConfiguredNvdaKeyAsModifier(VirtualKeyNvdaChoice choice, VirtualKeyCode expectedVk, bool expectedExtended)
    {
        var nvdaKey = VirtualKeyboardLayout.NvdaKeyMapping(choice);
        nvdaKey.ShouldBe(new VirtualKeyMapping(expectedVk, expectedExtended));

        var shortcuts = VirtualKeyboardLayout.Build(nvdaKey).Single(g => g.Id == VirtualKeyboardGroupId.Shortcuts).Keys;

        var title = shortcuts.Single(k => k.NameKey == "Shortcut_Title");
        title.Mapping.VkCode.ShouldBe(VirtualKeyCode.T);
        title.Modifiers.ShouldBe([nvdaKey]);

        shortcuts.Count(k => k.Modifiers.Contains(nvdaKey)).ShouldBe(6);
    }

    [Fact]
    public void Build_OtherShortcuts_UseTheRightModifier()
    {
        var shortcuts = Group(VirtualKeyboardGroupId.Shortcuts).Keys;

        shortcuts.Single(k => k.NameKey == "Shortcut_AltTab").Modifiers.ShouldBe([VirtualKeyboardLayout.Alt]);
        shortcuts.Single(k => k.NameKey == "Shortcut_Desktop").Modifiers.ShouldBe([VirtualKeyboardLayout.Win]);
        shortcuts.Single(k => k.NameKey == "Shortcut_StopSpeech").Modifiers.ShouldBeEmpty();
        Group(VirtualKeyboardGroupId.Navigation).Keys.Single(k => k.NameKey == "Key_ShiftTab").Modifiers.ShouldBe([VirtualKeyboardLayout.Shift]);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    public void Build_EveryTitleHintAndKeyName_ResolvesToARealLocalizedString(string cultureName)
    {
        var previous = AppResources.Culture;
        AppResources.Culture = new CultureInfo(cultureName);
        try
        {
            foreach (var group in Groups)
            {
                group.Title.ShouldNotBe(group.TitleKey);
                if (group.HintKey is not null)
                    group.Hint.ShouldNotBe(group.HintKey);

                foreach (var key in group.Keys)
                {
                    key.Name.ShouldNotBeNullOrWhiteSpace();
                    key.DisplayLabel.ShouldNotBeNullOrWhiteSpace();
                    if (key.NameKey is not null)
                    {
                        key.Name.ShouldNotBe(key.NameKey);
                        key.Name.ShouldNotContain("{0}");
                    }
                }
            }
        }
        finally
        {
            AppResources.Culture = previous;
        }
    }

    [Fact]
    public void NumpadKeys_HaveAnAccessibleNameThatSaysNumpad()
    {
        var previous = AppResources.Culture;
        AppResources.Culture = new CultureInfo("pt-BR");
        try
        {
            Group(VirtualKeyboardGroupId.Numpad).Keys.First(k => k.Label == "1").Name.ShouldBe("Teclado numérico 1");
        }
        finally
        {
            AppResources.Culture = previous;
        }
    }
}
