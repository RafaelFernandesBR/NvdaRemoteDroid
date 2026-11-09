using NvdaRemoteDroid.Core.Keyboard;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Keyboard;

public class AndroidKeyToVirtualKeyMapTests
{
    [Fact]
    public void TryMap_LetterA_MapsToVkAWithoutExtended()
    {
        AndroidKeyToVirtualKeyMap.TryMap((int)AndroidKeyCode.A, out var mapping).ShouldBeTrue();

        mapping.VkCode.ShouldBe(VirtualKeyCode.A);
        mapping.Extended.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AndroidKeyCode.DpadDown, VirtualKeyCode.Down)]
    [InlineData(AndroidKeyCode.Insert, VirtualKeyCode.Insert)]
    [InlineData(AndroidKeyCode.ForwardDel, VirtualKeyCode.Delete)]
    [InlineData(AndroidKeyCode.MoveHome, VirtualKeyCode.Home)]
    [InlineData(AndroidKeyCode.MoveEnd, VirtualKeyCode.End)]
    [InlineData(AndroidKeyCode.PageUp, VirtualKeyCode.PageUp)]
    [InlineData(AndroidKeyCode.PageDown, VirtualKeyCode.PageDown)]
    public void TryMap_NavigationKeys_AreMarkedExtended(AndroidKeyCode input, VirtualKeyCode expectedVk)
    {
        AndroidKeyToVirtualKeyMap.TryMap((int)input, out var mapping).ShouldBeTrue();

        mapping.VkCode.ShouldBe(expectedVk);
        mapping.Extended.ShouldBeTrue();
    }

    [Fact]
    public void TryMap_NumpadDigit_IsNotExtended()
    {
        AndroidKeyToVirtualKeyMap.TryMap((int)AndroidKeyCode.Numpad5, out var mapping).ShouldBeTrue();

        mapping.VkCode.ShouldBe(VirtualKeyCode.Numpad5);
        mapping.Extended.ShouldBeFalse();
    }

    [Fact]
    public void TryMap_NumpadEnter_IsExtendedUnlikeMainEnter()
    {
        AndroidKeyToVirtualKeyMap.TryMap((int)AndroidKeyCode.NumpadEnter, out var numpadEnter).ShouldBeTrue();
        AndroidKeyToVirtualKeyMap.TryMap((int)AndroidKeyCode.Enter, out var mainEnter).ShouldBeTrue();

        numpadEnter.VkCode.ShouldBe(VirtualKeyCode.Enter);
        numpadEnter.Extended.ShouldBeTrue();
        mainEnter.Extended.ShouldBeFalse();
    }

    [Fact]
    public void TryMap_UnmappedKeyCode_ReturnsFalse()
    {
        AndroidKeyToVirtualKeyMap.TryMap(999_999, out _).ShouldBeFalse();
    }
}
