using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Resources.Strings;

namespace NvdaRemoteDroid.Core.Keyboard;

/// <summary>Identifica um grupo de teclas do teclado virtual (mostrado um por vez na tela).</summary>
public enum VirtualKeyboardGroupId
{
    /// <summary>Teclas/atalhos escolhidos pelo usuário em Configurações (primeiro grupo da lista).</summary>
    Favorites,
    Shortcuts,
    Navigation,
    Letters,
    NumbersAndSymbols,
    Function,
    Numpad,

    /// <summary>Área de gestos (não é uma grade de teclas: a tela mostra o GesturePadView).</summary>
    Gestures,
}

/// <summary>
/// Uma tecla (ou atalho pronto) do teclado virtual. Label é o texto literal mostrado no botão
/// (ex. "A", "F5", "↑"); NameKey é a chave de recurso do nome acessível quando o literal não
/// basta para um leitor de tela (ex. "↑" -> "Seta para cima"). Modifiers são pressionados em
/// volta da tecla — é o que transforma "N" em "NVDA+N".
/// </summary>
public sealed record VirtualKey(
    string? Label,
    string? NameKey,
    VirtualKeyMapping Mapping,
    IReadOnlyList<VirtualKeyMapping> Modifiers,
    string? NameArgument = null)
{
    /// <summary>
    /// Identidade estável da tecla (grupo + chave de nome + rótulo), usada pelos favoritos.
    /// Não depende da tecla NVDA escolhida, para o favorito sobreviver à troca Insert/CapsLock.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nome/rótulo prontos de um favorito com modificadores extras ("Ctrl + C").</summary>
    public string? CustomName { get; init; }

    public string? CustomLabel { get; init; }

    /// <summary>Nome lido pelo leitor de tela, já localizado.</summary>
    public string Name => CustomName ?? (NameKey is null
        ? Label ?? string.Empty
        : string.Format(AppResources.GetString(NameKey), NameArgument));

    /// <summary>Texto visível no botão: o literal, ou o nome localizado quando não há literal.</summary>
    public string DisplayLabel => CustomLabel ?? Label ?? Name;
}

/// <summary>Grupo de teclas com título localizado e a largura mínima que cada botão precisa.</summary>
public sealed record VirtualKeyGroup(
    VirtualKeyboardGroupId Id,
    string TitleKey,
    string? HintKey,
    double MinKeyWidth,
    IReadOnlyList<VirtualKey> Keys)
{
    public string Title => AppResources.GetString(TitleKey);

    public string? Hint => HintKey is null ? null : AppResources.GetString(HintKey);
}

/// <summary>
/// Tabela pura (sem MAUI) de todas as teclas oferecidas pelo teclado virtual, agrupadas por
/// categoria. Os flags "extended" seguem o mesmo critério de AndroidKeyToVirtualKeyMap (setas,
/// Insert/Delete/Home/End/PgUp/PgDn, Windows, Aplicações, NumLock, Enter e barra do numpad).
/// </summary>
public static class VirtualKeyboardLayout
{
    public static readonly VirtualKeyMapping Ctrl = new(VirtualKeyCode.Control, false);
    public static readonly VirtualKeyMapping Alt = new(VirtualKeyCode.Menu, false);
    public static readonly VirtualKeyMapping Shift = new(VirtualKeyCode.Shift, false);
    public static readonly VirtualKeyMapping Win = new(VirtualKeyCode.LeftWindows, true);

    private static readonly IReadOnlyList<VirtualKeyMapping> NoModifiers = [];

    /// <summary>Tecla física enviada como "tecla NVDA", conforme a preferência do usuário.</summary>
    public static VirtualKeyMapping NvdaKeyMapping(VirtualKeyNvdaChoice choice) => choice == VirtualKeyNvdaChoice.CapsLock
        ? new VirtualKeyMapping(VirtualKeyCode.CapsLock, false)
        : new VirtualKeyMapping(VirtualKeyCode.Insert, true);

    /// <summary>Todos os grupos, com Favoritos primeiro (vazio quando <paramref name="favorites"/> é null/vazio).</summary>
    public static IReadOnlyList<VirtualKeyGroup> Build(VirtualKeyMapping nvdaKey, IReadOnlyList<FavoriteKey>? favorites = null)
    {
        var sources = BuildSourceGroups(nvdaKey);
        var favoriteKeys = VirtualKeyFavorites.Resolve(sources, favorites ?? [], nvdaKey);

        var favoritesGroup = new VirtualKeyGroup(
            VirtualKeyboardGroupId.Favorites,
            "KeyGroup_Favorites",
            favoriteKeys.Count == 0 ? "KeyGroup_Favorites_Empty" : null,
            150,
            favoriteKeys);

        return [favoritesGroup, .. sources, new(VirtualKeyboardGroupId.Gestures, "KeyGroup_Gestures", null, 0, [])];
    }

    /// <summary>Grupos com teclas reais (sem Favoritos nem Gestos): de onde os favoritos são escolhidos.</summary>
    public static IReadOnlyList<VirtualKeyGroup> BuildSourceGroups(VirtualKeyMapping nvdaKey) =>
    [
        WithIds(new(VirtualKeyboardGroupId.Shortcuts, "KeyGroup_Shortcuts", null, 150, BuildShortcuts(nvdaKey))),
        WithIds(new(VirtualKeyboardGroupId.Navigation, "KeyGroup_Navigation", null, 76, BuildNavigation())),
        WithIds(new(VirtualKeyboardGroupId.Letters, "KeyGroup_Letters", null, 52, BuildLetters())),
        WithIds(new(VirtualKeyboardGroupId.NumbersAndSymbols, "KeyGroup_Numbers", "KeyGroup_Numbers_Hint", 52, BuildNumbersAndSymbols())),
        WithIds(new(VirtualKeyboardGroupId.Function, "KeyGroup_Function", null, 76, BuildFunction())),
        WithIds(new(VirtualKeyboardGroupId.Numpad, "KeyGroup_Numpad", null, 76, BuildNumpad())),
    ];

    private static VirtualKeyGroup WithIds(VirtualKeyGroup group) =>
        group with { Keys = group.Keys.Select(k => k with { Id = $"{group.Id}:{k.NameKey}:{k.Label}" }).ToList() };

    private static List<VirtualKey> BuildShortcuts(VirtualKeyMapping nvdaKey)
    {
        IReadOnlyList<VirtualKeyMapping> nvda = [nvdaKey];
        IReadOnlyList<VirtualKeyMapping> alt = [Alt];
        IReadOnlyList<VirtualKeyMapping> win = [Win];

        return
        [
            Named("Shortcut_StopSpeech", VirtualKeyCode.Control),
            Named("Shortcut_Title", VirtualKeyCode.T, modifiers: nvda),
            Named("Shortcut_Focus", VirtualKeyCode.Tab, modifiers: nvda),
            Named("Shortcut_SayAll", VirtualKeyCode.Down, extended: true, modifiers: nvda),
            Named("Shortcut_FocusMode", VirtualKeyCode.Space, modifiers: nvda),
            Named("Shortcut_Time", VirtualKeyCode.F12, modifiers: nvda),
            Named("Shortcut_NvdaMenu", VirtualKeyCode.N, modifiers: nvda),
            Named("Shortcut_AltTab", VirtualKeyCode.Tab, modifiers: alt),
            Named("Shortcut_CloseWindow", VirtualKeyCode.F4, modifiers: alt),
            Named("Shortcut_Desktop", VirtualKeyCode.D, modifiers: win),
            Named("Shortcut_StartMenu", VirtualKeyCode.LeftWindows, extended: true),
            Named("Shortcut_ContextMenu", VirtualKeyCode.Apps, extended: true),
        ];
    }

    private static List<VirtualKey> BuildNavigation() =>
    [
        Named("Key_Up", VirtualKeyCode.Up, extended: true, label: "↑"),
        Named("Key_Down", VirtualKeyCode.Down, extended: true, label: "↓"),
        Named("Key_Left", VirtualKeyCode.Left, extended: true, label: "←"),
        Named("Key_Right", VirtualKeyCode.Right, extended: true, label: "→"),
        Named("Key_Tab", VirtualKeyCode.Tab, label: "Tab"),
        Named("Key_ShiftTab", VirtualKeyCode.Tab, label: "⇧Tab", modifiers: [Shift]),
        Named("Key_Enter", VirtualKeyCode.Enter, label: "Enter"),
        Named("Key_Escape", VirtualKeyCode.Escape, label: "Esc"),
        Named("Key_Space", VirtualKeyCode.Space),
        Named("Key_Backspace", VirtualKeyCode.Back, label: "⌫"),
        Named("Key_Delete", VirtualKeyCode.Delete, extended: true, label: "Del"),
        Named("Key_Insert", VirtualKeyCode.Insert, extended: true, label: "Ins"),
        Named("Key_Home", VirtualKeyCode.Home, extended: true, label: "Home"),
        Named("Key_End", VirtualKeyCode.End, extended: true, label: "End"),
        Named("Key_PageUp", VirtualKeyCode.PageUp, extended: true, label: "PgUp"),
        Named("Key_PageDown", VirtualKeyCode.PageDown, extended: true, label: "PgDn"),
    ];

    private static List<VirtualKey> BuildLetters()
    {
        var keys = new List<VirtualKey>(26);
        for (var letter = 'A'; letter <= 'Z'; letter++)
            keys.Add(Literal(letter.ToString(), (VirtualKeyCode)((int)VirtualKeyCode.A + (letter - 'A'))));
        return keys;
    }

    private static List<VirtualKey> BuildNumbersAndSymbols()
    {
        var keys = new List<VirtualKey>(21);
        for (var digit = 1; digit <= 9; digit++)
            keys.Add(Literal(digit.ToString(), (VirtualKeyCode)((int)VirtualKeyCode.D0 + digit)));
        keys.Add(Literal("0", VirtualKeyCode.D0));

        keys.Add(Literal("-", VirtualKeyCode.OemMinus));
        keys.Add(Literal("=", VirtualKeyCode.OemPlus));
        keys.Add(Literal(",", VirtualKeyCode.OemComma));
        keys.Add(Literal(".", VirtualKeyCode.OemPeriod));
        keys.Add(Literal(";", VirtualKeyCode.Oem1));
        keys.Add(Literal("/", VirtualKeyCode.Oem2));
        keys.Add(Literal("`", VirtualKeyCode.Oem3));
        keys.Add(Literal("[", VirtualKeyCode.Oem4));
        keys.Add(Literal("\\", VirtualKeyCode.Oem5));
        keys.Add(Literal("]", VirtualKeyCode.Oem6));
        keys.Add(Literal("'", VirtualKeyCode.Oem7));
        return keys;
    }

    private static List<VirtualKey> BuildFunction()
    {
        var keys = new List<VirtualKey>(17);
        for (var f = 1; f <= 12; f++)
            keys.Add(Literal($"F{f}", (VirtualKeyCode)((int)VirtualKeyCode.F1 + f - 1)));

        keys.Add(Named("Key_PrintScreen", VirtualKeyCode.PrintScreen, extended: true, label: "PrtSc"));
        keys.Add(Named("Key_CapsLock", VirtualKeyCode.CapsLock, label: "Caps"));
        keys.Add(Named("Key_NumLock", VirtualKeyCode.NumLock, extended: true, label: "Num"));
        keys.Add(Named("Key_ScrollLock", VirtualKeyCode.ScrollLock, label: "Scroll"));
        keys.Add(Named("Key_Pause", VirtualKeyCode.Pause, label: "Pause"));
        return keys;
    }

    private static List<VirtualKey> BuildNumpad()
    {
        var keys = new List<VirtualKey>(16);
        for (var digit = 1; digit <= 9; digit++)
            keys.Add(Numpad(digit.ToString(), (VirtualKeyCode)((int)VirtualKeyCode.Numpad0 + digit)));
        keys.Add(Numpad("0", VirtualKeyCode.Numpad0));

        keys.Add(Numpad("/", VirtualKeyCode.Divide, extended: true));
        keys.Add(Numpad("*", VirtualKeyCode.Multiply));
        keys.Add(Numpad("-", VirtualKeyCode.Subtract));
        keys.Add(Numpad("+", VirtualKeyCode.Add));
        keys.Add(Numpad(".", VirtualKeyCode.Decimal));
        keys.Add(Numpad("Enter", VirtualKeyCode.Enter, extended: true));
        return keys;
    }

    private static VirtualKey Literal(string label, VirtualKeyCode vkCode) =>
        new(label, NameKey: null, new VirtualKeyMapping(vkCode, false), NoModifiers);

    private static VirtualKey Named(
        string nameKey,
        VirtualKeyCode vkCode,
        bool extended = false,
        string? label = null,
        IReadOnlyList<VirtualKeyMapping>? modifiers = null) =>
        new(label, nameKey, new VirtualKeyMapping(vkCode, extended), modifiers ?? NoModifiers);

    private static VirtualKey Numpad(string label, VirtualKeyCode vkCode, bool extended = false) =>
        new(label, "Key_Numpad_Format", new VirtualKeyMapping(vkCode, extended), NoModifiers, NameArgument: label);
}
