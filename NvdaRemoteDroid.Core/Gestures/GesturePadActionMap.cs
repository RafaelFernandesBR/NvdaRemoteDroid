using NvdaRemoteDroid.Core.Keyboard;

namespace NvdaRemoteDroid.Core.Gestures;

/// <summary>O que um gesto faz: roda um script do NVDA, manda uma tecla, ou troca o modo da área.</summary>
public abstract record GesturePadAction
{
    /// <summary>Script de <c>globalCommands.GlobalCommands</c> (via braille_input/scriptPath).</summary>
    public sealed record NvdaScript(string ScriptName, string GestureId) : GesturePadAction;

    /// <summary>Toque de tecla (down+up), pelo mesmo caminho do teclado virtual.</summary>
    public sealed record Keys(VirtualKeyMapping Key) : GesturePadAction;

    /// <summary>Troca para o próximo modo (tratado pela tela, que guarda o modo).</summary>
    public sealed record CycleMode : GesturePadAction;
}

/// <summary>
/// Tabela pura (modo, gesto) -> ação. Os scripts são só de GlobalCommands, conferidos no código do
/// NVDA: nenhum lê coordenadas do gesto (gesture.x/y) nem depende de touchHandler. O NVDA procura o
/// script pelo nome no foco e no tree interceptor antes de GlobalCommands, então a lista é fechada
/// (<see cref="AllowedScripts"/>) e um nome novo só entra depois de validado contra um NVDA real.
/// </summary>
public static class GesturePadActionMap
{
    public static readonly IReadOnlySet<string> AllowedScripts = new HashSet<string>
    {
        "navigatorObject_nextInFlow",
        "navigatorObject_previousInFlow",
        "navigatorObject_parent",
        "navigatorObject_firstChild",
        "navigatorObject_current",
        "review_nextCharacter",
        "review_previousCharacter",
        "review_previousLine",
        "review_nextLine",
        "review_currentLine",
        "review_activate",
    };

    public static GesturePadAction? Resolve(GesturePadMode mode, GestureKind gesture)
    {
        if (gesture == GestureKind.TwoFingerTap)
            return new GesturePadAction.CycleMode();

        return mode switch
        {
            GesturePadMode.Object => gesture switch
            {
                GestureKind.SwipeRight => Script("navigatorObject_nextInFlow", gesture),
                GestureKind.SwipeLeft => Script("navigatorObject_previousInFlow", gesture),
                GestureKind.SwipeUp => Script("navigatorObject_parent", gesture),
                GestureKind.SwipeDown => Script("navigatorObject_firstChild", gesture),
                GestureKind.Tap => Script("navigatorObject_current", gesture),
                GestureKind.DoubleTap => Script("review_activate", gesture),
                _ => null,
            },
            GesturePadMode.Text => gesture switch
            {
                GestureKind.SwipeRight => Script("review_nextCharacter", gesture),
                GestureKind.SwipeLeft => Script("review_previousCharacter", gesture),
                GestureKind.SwipeUp => Script("review_previousLine", gesture),
                GestureKind.SwipeDown => Script("review_nextLine", gesture),
                GestureKind.Tap => Script("review_currentLine", gesture),
                GestureKind.DoubleTap => Script("review_activate", gesture),
                _ => null,
            },
            _ => gesture switch
            {
                GestureKind.SwipeRight => Key(VirtualKeyCode.Right, extended: true),
                GestureKind.SwipeLeft => Key(VirtualKeyCode.Left, extended: true),
                GestureKind.SwipeUp => Key(VirtualKeyCode.Up, extended: true),
                GestureKind.SwipeDown => Key(VirtualKeyCode.Down, extended: true),
                GestureKind.DoubleTap => Key(VirtualKeyCode.Enter, extended: false),
                GestureKind.LongPress => Key(VirtualKeyCode.Escape, extended: false),
                _ => null,
            },
        };
    }

    private static GesturePadAction.NvdaScript Script(string name, GestureKind gesture) =>
        new(name, GestureId(gesture));

    private static GesturePadAction.Keys Key(VirtualKeyCode code, bool extended) =>
        new(new VirtualKeyMapping(code, extended));

    /// <summary>Identificador estável do gesto no campo <c>id</c> da mensagem (ex. "swipeRight").</summary>
    public static string GestureId(GestureKind gesture)
    {
        var name = gesture.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
