using NvdaRemoteDroid.Core.Gestures;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Gestures;

public class GestureRecognizerTests
{
    private readonly GestureRecognizer _sut = new();

    [Theory]
    [InlineData(100, 0, GestureKind.SwipeRight)]
    [InlineData(-100, 0, GestureKind.SwipeLeft)]
    [InlineData(0, 100, GestureKind.SwipeDown)]
    [InlineData(0, -100, GestureKind.SwipeUp)]
    [InlineData(100, 40, GestureKind.SwipeRight)]
    [InlineData(-30, -90, GestureKind.SwipeUp)]
    public void Up_AfterLongMovement_IsSwipeOnDominantAxis(double dx, double dy, GestureKind expected)
    {
        _sut.Down(0, 200, 200);
        _sut.Move(200 + dx, 200 + dy);

        _sut.Up(120, 200 + dx, 200 + dy).ShouldBe(expected);
    }

    [Fact]
    public void ShortMovement_IsTap_ReleasedOnlyAfterDoubleTapWindow()
    {
        _sut.Down(0, 10, 10);
        _sut.Up(80, 12, 11).ShouldBeNull();

        _sut.NeedsTick.ShouldBeTrue();
        _sut.Tick(80 + GestureRecognizer.DoubleTapWindowMs - 1).ShouldBeNull();
        _sut.Tick(80 + GestureRecognizer.DoubleTapWindowMs).ShouldBe(GestureKind.Tap);
        _sut.NeedsTick.ShouldBeFalse();
        _sut.Tick(2000).ShouldBeNull();
    }

    [Fact]
    public void SecondTapInsideWindow_IsDoubleTap_AndNoSingleTapFollows()
    {
        _sut.Down(0, 10, 10);
        _sut.Up(50, 10, 10).ShouldBeNull();
        _sut.Down(200, 12, 10);

        _sut.Up(250, 12, 10).ShouldBe(GestureKind.DoubleTap);
        _sut.Tick(5000).ShouldBeNull();
    }

    [Fact]
    public void SecondTapOutsideWindow_IsNewTap()
    {
        _sut.Down(0, 10, 10);
        _sut.Up(50, 10, 10);
        _sut.Tick(50 + GestureRecognizer.DoubleTapWindowMs).ShouldBe(GestureKind.Tap);

        _sut.Down(1000, 10, 10);
        _sut.Up(1050, 10, 10).ShouldBeNull();
        _sut.Tick(1050 + GestureRecognizer.DoubleTapWindowMs).ShouldBe(GestureKind.Tap);
    }

    [Fact]
    public void HeldStill_FiresLongPressOnce_AndReleaseEmitsNothing()
    {
        _sut.Down(0, 10, 10);

        _sut.Tick(GestureRecognizer.LongPressMs - 1).ShouldBeNull();
        _sut.Tick(GestureRecognizer.LongPressMs).ShouldBe(GestureKind.LongPress);
        _sut.Tick(GestureRecognizer.LongPressMs + 50).ShouldBeNull();
        _sut.Up(900, 10, 10).ShouldBeNull();
        _sut.Tick(5000).ShouldBeNull();
    }

    [Fact]
    public void MovingFinger_NeverBecomesLongPress()
    {
        _sut.Down(0, 10, 10);
        _sut.Move(10 + GestureRecognizer.TapMaxMovementDp + 5, 10);

        _sut.Tick(2000).ShouldBeNull();
    }

    [Fact]
    public void AmbiguousMovement_IsDiscarded()
    {
        _sut.Down(0, 10, 10);
        _sut.Up(100, 10 + 30, 10).ShouldBeNull();

        _sut.NeedsTick.ShouldBeFalse();
    }

    [Fact]
    public void TwoFingers_QuickAndStill_IsTwoFingerTap()
    {
        _sut.Down(0, 10, 10);
        _sut.SecondFingerDown();

        _sut.Up(100, 10, 10).ShouldBe(GestureKind.TwoFingerTap);
        _sut.Tick(5000).ShouldBeNull();
    }

    [Fact]
    public void TwoFingers_UpReportsOtherFingerPosition_StillATap()
    {
        // Regressão: se o 1º dedo sai antes, o Android entrega o Up com as coordenadas do 2º.
        _sut.Down(0, 10, 10);
        _sut.SecondFingerDown();

        _sut.Up(100, 90, 120).ShouldBe(GestureKind.TwoFingerTap);
    }

    [Fact]
    public void TwoFingers_Slow_IsNotATap()
    {
        _sut.Down(0, 10, 10);
        _sut.SecondFingerDown();

        _sut.Tick(GestureRecognizer.LongPressMs + 100).ShouldBeNull();
        _sut.Up(GestureRecognizer.LongPressMs + 200, 10, 10).ShouldBeNull();
    }

    [Fact]
    public void Cancel_DropsEverythingWithoutEmitting()
    {
        _sut.Down(0, 10, 10);
        _sut.Up(50, 10, 10);
        _sut.Cancel();

        _sut.NeedsTick.ShouldBeFalse();
        _sut.Tick(5000).ShouldBeNull();
    }

    [Fact]
    public void Up_WithoutDown_IsIgnored() => _sut.Up(0, 0, 0).ShouldBeNull();
}
