using NvdaRemoteDroid.Core.Keyboard;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Keyboard;

public class KeyEventFactoryTests
{
    [Fact]
    public void Create_AlwaysSetsScanCodeToNull()
    {
        var mapping = new VirtualKeyMapping(VirtualKeyCode.A, Extended: false);

        var message = KeyEventFactory.Create(mapping, pressed: true);

        message.ScanCode.ShouldBeNull();
        message.VkCode.ShouldBe((int)VirtualKeyCode.A);
        message.Pressed.ShouldBeTrue();
    }

    [Fact]
    public void CreateTapPair_ProducesMatchingDownThenUp()
    {
        var mapping = new VirtualKeyMapping(VirtualKeyCode.Enter, Extended: false);

        var (down, up) = KeyEventFactory.CreateTapPair(mapping);

        down.Pressed.ShouldBeTrue();
        up.Pressed.ShouldBeFalse();
        down.VkCode.ShouldBe(up.VkCode);
        down.Extended.ShouldBe(up.Extended);
    }

    [Fact]
    public void CreateReleaseAllPair_UsesVkNoneForBothDownAndUp()
    {
        var (down, up) = KeyEventFactory.CreateReleaseAllPair();

        down.VkCode.ShouldBe((int)VirtualKeyCode.None);
        up.VkCode.ShouldBe((int)VirtualKeyCode.None);
        down.Pressed.ShouldBeTrue();
        up.Pressed.ShouldBeFalse();
    }
}
