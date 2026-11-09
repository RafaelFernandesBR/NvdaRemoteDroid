namespace NvdaRemoteDroid.Core.Keyboard;

/// <summary>
/// Mapeia Android.Views.Keycode (teclado físico/Bluetooth) para o VK do Windows + flag
/// "extended" (KEYEVENTF_EXTENDEDKEY) que o follower espera em KeyMessage. extended=true para
/// setas, Insert/Delete/Home/End/PgUp/PgDn, Ctrl/Alt direitos, NumLock e o Enter/barra do
/// numpad — mesmo conjunto que o Windows marca como "extended" nativamente.
/// </summary>
public static class AndroidKeyToVirtualKeyMap
{
    private static readonly Dictionary<AndroidKeyCode, VirtualKeyMapping> Map = new()
    {
        [AndroidKeyCode.Del] = new(VirtualKeyCode.Back, false),
        [AndroidKeyCode.Tab] = new(VirtualKeyCode.Tab, false),
        [AndroidKeyCode.Enter] = new(VirtualKeyCode.Enter, false),
        [AndroidKeyCode.Escape] = new(VirtualKeyCode.Escape, false),
        [AndroidKeyCode.Space] = new(VirtualKeyCode.Space, false),

        [AndroidKeyCode.PageUp] = new(VirtualKeyCode.PageUp, true),
        [AndroidKeyCode.PageDown] = new(VirtualKeyCode.PageDown, true),
        [AndroidKeyCode.MoveEnd] = new(VirtualKeyCode.End, true),
        [AndroidKeyCode.MoveHome] = new(VirtualKeyCode.Home, true),
        [AndroidKeyCode.DpadLeft] = new(VirtualKeyCode.Left, true),
        [AndroidKeyCode.DpadUp] = new(VirtualKeyCode.Up, true),
        [AndroidKeyCode.DpadRight] = new(VirtualKeyCode.Right, true),
        [AndroidKeyCode.DpadDown] = new(VirtualKeyCode.Down, true),
        [AndroidKeyCode.Insert] = new(VirtualKeyCode.Insert, true),
        [AndroidKeyCode.ForwardDel] = new(VirtualKeyCode.Delete, true),

        [AndroidKeyCode.Num0] = new(VirtualKeyCode.D0, false),
        [AndroidKeyCode.Num1] = new(VirtualKeyCode.D1, false),
        [AndroidKeyCode.Num2] = new(VirtualKeyCode.D2, false),
        [AndroidKeyCode.Num3] = new(VirtualKeyCode.D3, false),
        [AndroidKeyCode.Num4] = new(VirtualKeyCode.D4, false),
        [AndroidKeyCode.Num5] = new(VirtualKeyCode.D5, false),
        [AndroidKeyCode.Num6] = new(VirtualKeyCode.D6, false),
        [AndroidKeyCode.Num7] = new(VirtualKeyCode.D7, false),
        [AndroidKeyCode.Num8] = new(VirtualKeyCode.D8, false),
        [AndroidKeyCode.Num9] = new(VirtualKeyCode.D9, false),

        [AndroidKeyCode.A] = new(VirtualKeyCode.A, false),
        [AndroidKeyCode.B] = new(VirtualKeyCode.B, false),
        [AndroidKeyCode.C] = new(VirtualKeyCode.C, false),
        [AndroidKeyCode.D] = new(VirtualKeyCode.D, false),
        [AndroidKeyCode.E] = new(VirtualKeyCode.E, false),
        [AndroidKeyCode.F] = new(VirtualKeyCode.F, false),
        [AndroidKeyCode.G] = new(VirtualKeyCode.G, false),
        [AndroidKeyCode.H] = new(VirtualKeyCode.H, false),
        [AndroidKeyCode.I] = new(VirtualKeyCode.I, false),
        [AndroidKeyCode.J] = new(VirtualKeyCode.J, false),
        [AndroidKeyCode.K] = new(VirtualKeyCode.K, false),
        [AndroidKeyCode.L] = new(VirtualKeyCode.L, false),
        [AndroidKeyCode.M] = new(VirtualKeyCode.M, false),
        [AndroidKeyCode.N] = new(VirtualKeyCode.N, false),
        [AndroidKeyCode.O] = new(VirtualKeyCode.O, false),
        [AndroidKeyCode.P] = new(VirtualKeyCode.P, false),
        [AndroidKeyCode.Q] = new(VirtualKeyCode.Q, false),
        [AndroidKeyCode.R] = new(VirtualKeyCode.R, false),
        [AndroidKeyCode.S] = new(VirtualKeyCode.S, false),
        [AndroidKeyCode.T] = new(VirtualKeyCode.T, false),
        [AndroidKeyCode.U] = new(VirtualKeyCode.U, false),
        [AndroidKeyCode.V] = new(VirtualKeyCode.V, false),
        [AndroidKeyCode.W] = new(VirtualKeyCode.W, false),
        [AndroidKeyCode.X] = new(VirtualKeyCode.X, false),
        [AndroidKeyCode.Y] = new(VirtualKeyCode.Y, false),
        [AndroidKeyCode.Z] = new(VirtualKeyCode.Z, false),

        [AndroidKeyCode.CapsLock] = new(VirtualKeyCode.CapsLock, false),

        [AndroidKeyCode.Numpad0] = new(VirtualKeyCode.Numpad0, false),
        [AndroidKeyCode.Numpad1] = new(VirtualKeyCode.Numpad1, false),
        [AndroidKeyCode.Numpad2] = new(VirtualKeyCode.Numpad2, false),
        [AndroidKeyCode.Numpad3] = new(VirtualKeyCode.Numpad3, false),
        [AndroidKeyCode.Numpad4] = new(VirtualKeyCode.Numpad4, false),
        [AndroidKeyCode.Numpad5] = new(VirtualKeyCode.Numpad5, false),
        [AndroidKeyCode.Numpad6] = new(VirtualKeyCode.Numpad6, false),
        [AndroidKeyCode.Numpad7] = new(VirtualKeyCode.Numpad7, false),
        [AndroidKeyCode.Numpad8] = new(VirtualKeyCode.Numpad8, false),
        [AndroidKeyCode.Numpad9] = new(VirtualKeyCode.Numpad9, false),
        [AndroidKeyCode.NumpadMultiply] = new(VirtualKeyCode.Multiply, false),
        [AndroidKeyCode.NumpadAdd] = new(VirtualKeyCode.Add, false),
        [AndroidKeyCode.NumpadSubtract] = new(VirtualKeyCode.Subtract, false),
        [AndroidKeyCode.NumpadDot] = new(VirtualKeyCode.Decimal, false),
        [AndroidKeyCode.NumpadDivide] = new(VirtualKeyCode.Divide, true),
        [AndroidKeyCode.NumpadEnter] = new(VirtualKeyCode.Enter, true),

        [AndroidKeyCode.F1] = new(VirtualKeyCode.F1, false),
        [AndroidKeyCode.F2] = new(VirtualKeyCode.F2, false),
        [AndroidKeyCode.F3] = new(VirtualKeyCode.F3, false),
        [AndroidKeyCode.F4] = new(VirtualKeyCode.F4, false),
        [AndroidKeyCode.F5] = new(VirtualKeyCode.F5, false),
        [AndroidKeyCode.F6] = new(VirtualKeyCode.F6, false),
        [AndroidKeyCode.F7] = new(VirtualKeyCode.F7, false),
        [AndroidKeyCode.F8] = new(VirtualKeyCode.F8, false),
        [AndroidKeyCode.F9] = new(VirtualKeyCode.F9, false),
        [AndroidKeyCode.F10] = new(VirtualKeyCode.F10, false),
        [AndroidKeyCode.F11] = new(VirtualKeyCode.F11, false),
        [AndroidKeyCode.F12] = new(VirtualKeyCode.F12, false),

        [AndroidKeyCode.NumLock] = new(VirtualKeyCode.NumLock, true),
        [AndroidKeyCode.ShiftLeft] = new(VirtualKeyCode.LeftShift, false),
        [AndroidKeyCode.ShiftRight] = new(VirtualKeyCode.RightShift, false),
        [AndroidKeyCode.CtrlLeft] = new(VirtualKeyCode.LeftControl, false),
        [AndroidKeyCode.CtrlRight] = new(VirtualKeyCode.RightControl, true),
        [AndroidKeyCode.AltLeft] = new(VirtualKeyCode.LeftMenu, false),
        [AndroidKeyCode.AltRight] = new(VirtualKeyCode.RightMenu, true),
        [AndroidKeyCode.MetaLeft] = new(VirtualKeyCode.LeftWindows, true),
        [AndroidKeyCode.MetaRight] = new(VirtualKeyCode.RightWindows, true),

        [AndroidKeyCode.Semicolon] = new(VirtualKeyCode.Oem1, false),
        [AndroidKeyCode.Equals] = new(VirtualKeyCode.OemPlus, false),
        [AndroidKeyCode.Comma] = new(VirtualKeyCode.OemComma, false),
        [AndroidKeyCode.Minus] = new(VirtualKeyCode.OemMinus, false),
        [AndroidKeyCode.Period] = new(VirtualKeyCode.OemPeriod, false),
        [AndroidKeyCode.Slash] = new(VirtualKeyCode.Oem2, false),
        [AndroidKeyCode.Grave] = new(VirtualKeyCode.Oem3, false),
        [AndroidKeyCode.LeftBracket] = new(VirtualKeyCode.Oem4, false),
        [AndroidKeyCode.Backslash] = new(VirtualKeyCode.Oem5, false),
        [AndroidKeyCode.RightBracket] = new(VirtualKeyCode.Oem6, false),
        [AndroidKeyCode.Apostrophe] = new(VirtualKeyCode.Oem7, false),
    };

    /// <summary>Tenta mapear um keycode Android (valor numérico de Android.Views.Keycode) para o VK do Windows.</summary>
    public static bool TryMap(int androidKeyCode, out VirtualKeyMapping mapping)
    {
        if (Enum.IsDefined((AndroidKeyCode)androidKeyCode) && Map.TryGetValue((AndroidKeyCode)androidKeyCode, out mapping))
            return true;

        mapping = default;
        return false;
    }
}
