using NvdaRemoteDroid.Core.Gestures;
using NvdaRemoteDroid.Core.Keyboard;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Gestures;

public class GesturePadActionMapTests
{
    [Theory]
    [InlineData(GesturePadMode.Object, GestureKind.SwipeRight, "navigatorObject_nextInFlow")]
    [InlineData(GesturePadMode.Object, GestureKind.SwipeLeft, "navigatorObject_previousInFlow")]
    [InlineData(GesturePadMode.Object, GestureKind.SwipeUp, "navigatorObject_parent")]
    [InlineData(GesturePadMode.Object, GestureKind.SwipeDown, "navigatorObject_firstChild")]
    [InlineData(GesturePadMode.Object, GestureKind.Tap, "navigatorObject_current")]
    [InlineData(GesturePadMode.Object, GestureKind.DoubleTap, "review_activate")]
    [InlineData(GesturePadMode.Text, GestureKind.SwipeRight, "review_nextCharacter")]
    [InlineData(GesturePadMode.Text, GestureKind.SwipeLeft, "review_previousCharacter")]
    [InlineData(GesturePadMode.Text, GestureKind.SwipeUp, "review_previousLine")]
    [InlineData(GesturePadMode.Text, GestureKind.SwipeDown, "review_nextLine")]
    [InlineData(GesturePadMode.Text, GestureKind.Tap, "review_currentLine")]
    [InlineData(GesturePadMode.Text, GestureKind.DoubleTap, "review_activate")]
    public void Resolve_ObjectAndTextModes_MapToNvdaScripts(GesturePadMode mode, GestureKind gesture, string script)
    {
        var action = GesturePadActionMap.Resolve(mode, gesture).ShouldBeOfType<GesturePadAction.NvdaScript>();

        action.ScriptName.ShouldBe(script);
        action.GestureId.ShouldBe(GesturePadActionMap.GestureId(gesture));
    }

    [Theory]
    [InlineData(GestureKind.SwipeRight, VirtualKeyCode.Right, true)]
    [InlineData(GestureKind.SwipeLeft, VirtualKeyCode.Left, true)]
    [InlineData(GestureKind.SwipeUp, VirtualKeyCode.Up, true)]
    [InlineData(GestureKind.SwipeDown, VirtualKeyCode.Down, true)]
    [InlineData(GestureKind.DoubleTap, VirtualKeyCode.Enter, false)]
    [InlineData(GestureKind.LongPress, VirtualKeyCode.Escape, false)]
    public void Resolve_ArrowsMode_MapsToKeys_WithExtendedFlagOnArrows(GestureKind gesture, VirtualKeyCode code, bool extended)
    {
        var action = GesturePadActionMap.Resolve(GesturePadMode.Arrows, gesture).ShouldBeOfType<GesturePadAction.Keys>();

        action.Key.ShouldBe(new VirtualKeyMapping(code, extended));
    }

    [Fact]
    public void Resolve_TwoFingerTap_CyclesModeInEveryMode()
    {
        foreach (var mode in Enum.GetValues<GesturePadMode>())
            GesturePadActionMap.Resolve(mode, GestureKind.TwoFingerTap).ShouldBeOfType<GesturePadAction.CycleMode>();
    }

    [Fact]
    public void Resolve_EveryScriptInTheMap_IsInTheAllowedList()
    {
        foreach (var mode in Enum.GetValues<GesturePadMode>())
        foreach (var gesture in Enum.GetValues<GestureKind>())
        {
            if (GesturePadActionMap.Resolve(mode, gesture) is GesturePadAction.NvdaScript script)
                GesturePadActionMap.AllowedScripts.ShouldContain(script.ScriptName);
        }
    }

    [Fact]
    public void Next_CyclesThroughAllModes()
    {
        GesturePadMode.Object.Next().ShouldBe(GesturePadMode.Text);
        GesturePadMode.Text.Next().ShouldBe(GesturePadMode.Arrows);
        GesturePadMode.Arrows.Next().ShouldBe(GesturePadMode.Object);
    }

    [Fact]
    public void GestureId_IsCamelCase() =>
        GesturePadActionMap.GestureId(GestureKind.SwipeRight).ShouldBe("swipeRight");
}
