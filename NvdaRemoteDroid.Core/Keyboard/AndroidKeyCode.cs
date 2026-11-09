namespace NvdaRemoteDroid.Core.Keyboard;

/// <summary>
/// Espelha os valores numéricos de Android.Views.Keycode (android.view.KeyEvent) relevantes
/// para este app. Mantido como int puro para que o Core não precise referenciar o SDK Android
/// — o código de plataforma só faz um cast de Android.Views.Keycode para int ao chamar o mapa.
/// </summary>
public enum AndroidKeyCode
{
    Del = 67, // Backspace
    Tab = 61,
    Enter = 66,
    Escape = 111,
    Space = 62,
    PageUp = 92,
    PageDown = 93,
    MoveEnd = 123,
    MoveHome = 122,
    DpadLeft = 21,
    DpadUp = 19,
    DpadRight = 22,
    DpadDown = 20,
    Insert = 124,
    ForwardDel = 112, // Delete

    Num0 = 7, Num1 = 8, Num2 = 9, Num3 = 10, Num4 = 11,
    Num5 = 12, Num6 = 13, Num7 = 14, Num8 = 15, Num9 = 16,

    A = 29, B = 30, C = 31, D = 32, E = 33, F = 34, G = 35,
    H = 36, I = 37, J = 38, K = 39, L = 40, M = 41, N = 42,
    O = 43, P = 44, Q = 45, R = 46, S = 47, T = 48, U = 49,
    V = 50, W = 51, X = 52, Y = 53, Z = 54,

    CapsLock = 115,

    Numpad0 = 144, Numpad1 = 145, Numpad2 = 146, Numpad3 = 147, Numpad4 = 148,
    Numpad5 = 149, Numpad6 = 150, Numpad7 = 151, Numpad8 = 152, Numpad9 = 153,
    NumpadMultiply = 155, NumpadAdd = 157, NumpadSubtract = 156, NumpadDot = 158,
    NumpadDivide = 154, NumpadEnter = 160,

    F1 = 131, F2 = 132, F3 = 133, F4 = 134, F5 = 135, F6 = 136,
    F7 = 137, F8 = 138, F9 = 139, F10 = 140, F11 = 141, F12 = 142,

    NumLock = 143,
    ShiftLeft = 59, ShiftRight = 60,
    CtrlLeft = 113, CtrlRight = 114,
    AltLeft = 57, AltRight = 58,
    MetaLeft = 117, MetaRight = 118, // tecla Windows/Super

    Semicolon = 74, Equals = 70, Comma = 55, Minus = 69, Period = 56,
    Slash = 76, Grave = 68, LeftBracket = 71, Backslash = 73, RightBracket = 72, Apostrophe = 75,
}
