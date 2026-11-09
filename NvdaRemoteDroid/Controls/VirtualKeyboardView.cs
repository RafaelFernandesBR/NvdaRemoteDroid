using Microsoft.Maui.Layouts;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.ViewModels;
using AppRes = NvdaRemoteDroid.Core.Resources.Strings.AppResources;

namespace NvdaRemoteDroid.Controls;

/// <summary>
/// Teclado virtual na tela, construído em código (não XAML) para manter as ~60 teclas
/// legíveis como uma única tabela de dados em vez de marcação repetitiva. Modificadores
/// Ctrl/Alt/Shift/Win funcionam como "sticky" de um tiro: ativa, afeta a PRÓXIMA tecla tocada,
/// depois desativa sozinho — evita ter que manter o dedo em dois botões ao mesmo tempo.
/// </summary>
public sealed class VirtualKeyboardView : ContentView
{
    private const double MinTouchSize = 44;

    // VirtualKeyMapping completo (não só o VkCode): o bit Extended da tecla Windows precisa
    // sobreviver até o down/up ser enviado, senão o follower recebe Win sem KEYEVENTF_EXTENDEDKEY.
    private readonly HashSet<VirtualKeyMapping> _activeModifiers = [];
    private readonly Dictionary<VirtualKeyMapping, Button> _modifierButtons = [];

    public RemoteControlViewModel? ViewModel { get; set; }

    public void Initialize(RemoteControlViewModel viewModel, VirtualKeyMapping nvdaKeyMapping)
    {
        ViewModel = viewModel;
        Content = BuildLayout(nvdaKeyMapping);
    }

    private ScrollView BuildLayout(VirtualKeyMapping nvdaKeyMapping)
    {
        var root = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 12,
        };

        root.Children.Add(BuildModifierRow(nvdaKeyMapping));
        root.Children.Add(BuildRow(DigitRow()));
        root.Children.Add(BuildRow(LetterRow("QWERTYUIOP")));
        root.Children.Add(BuildRow(LetterRow("ASDFGHJKL")));
        root.Children.Add(BuildRow(LetterRow("ZXCVBNM")));
        root.Children.Add(BuildFunctionRow());
        root.Children.Add(BuildEditingRow());
        root.Children.Add(BuildNavigationRow());
        root.Children.Add(BuildSpaceEnterRow());

        return new ScrollView { Content = root, Orientation = ScrollOrientation.Both };
    }

    private FlexLayout BuildModifierRow(VirtualKeyMapping nvdaKeyMapping)
    {
        var row = NewFlexRow();

        row.Children.Add(CreateModifierButton(AppRes.RemoteControl_ModifierCtrl, new VirtualKeyMapping(VirtualKeyCode.Control, false)));
        row.Children.Add(CreateModifierButton(AppRes.RemoteControl_ModifierAlt, new VirtualKeyMapping(VirtualKeyCode.Menu, false)));
        row.Children.Add(CreateModifierButton(AppRes.RemoteControl_ModifierShift, new VirtualKeyMapping(VirtualKeyCode.Shift, false)));
        row.Children.Add(CreateModifierButton(AppRes.RemoteControl_ModifierWin, new VirtualKeyMapping(VirtualKeyCode.LeftWindows, true)));
        row.Children.Add(CreateKeyButton(AppRes.RemoteControl_NvdaKey, nvdaKeyMapping));
        row.Children.Add(CreateReleaseAllButton());

        return row;
    }

    private static IEnumerable<(string Label, VirtualKeyMapping Mapping)> DigitRow()
    {
        for (var digit = 0; digit <= 9; digit++)
            yield return (digit.ToString(), new VirtualKeyMapping((VirtualKeyCode)((int)VirtualKeyCode.D0 + digit), false));
    }

    private static IEnumerable<(string Label, VirtualKeyMapping Mapping)> LetterRow(string letters)
    {
        foreach (var letter in letters)
            yield return (letter.ToString(), new VirtualKeyMapping((VirtualKeyCode)((int)VirtualKeyCode.A + (letter - 'A')), false));
    }

    private FlexLayout BuildRow(IEnumerable<(string Label, VirtualKeyMapping Mapping)> keys)
    {
        var row = NewFlexRow();
        foreach (var (label, mapping) in keys)
            row.Children.Add(CreateKeyButton(label, mapping));
        return row;
    }

    private FlexLayout BuildFunctionRow()
    {
        var row = NewFlexRow();
        for (var f = 1; f <= 12; f++)
            row.Children.Add(CreateKeyButton($"F{f}", new VirtualKeyMapping((VirtualKeyCode)((int)VirtualKeyCode.F1 + f - 1), false)));
        return row;
    }

    private FlexLayout BuildEditingRow()
    {
        var row = NewFlexRow();
        row.Children.Add(CreateKeyButton("Esc", new VirtualKeyMapping(VirtualKeyCode.Escape, false)));
        row.Children.Add(CreateKeyButton("Tab", new VirtualKeyMapping(VirtualKeyCode.Tab, false)));
        row.Children.Add(CreateKeyButton("⌫", new VirtualKeyMapping(VirtualKeyCode.Back, false)));
        row.Children.Add(CreateKeyButton("Caps", new VirtualKeyMapping(VirtualKeyCode.CapsLock, false)));
        row.Children.Add(CreateKeyButton("Ins", new VirtualKeyMapping(VirtualKeyCode.Insert, true)));
        row.Children.Add(CreateKeyButton("Del", new VirtualKeyMapping(VirtualKeyCode.Delete, true)));
        return row;
    }

    private FlexLayout BuildNavigationRow()
    {
        var row = NewFlexRow();
        row.Children.Add(CreateKeyButton("Home", new VirtualKeyMapping(VirtualKeyCode.Home, true)));
        row.Children.Add(CreateKeyButton("↑", new VirtualKeyMapping(VirtualKeyCode.Up, true)));
        row.Children.Add(CreateKeyButton("End", new VirtualKeyMapping(VirtualKeyCode.End, true)));
        row.Children.Add(CreateKeyButton("←", new VirtualKeyMapping(VirtualKeyCode.Left, true)));
        row.Children.Add(CreateKeyButton("↓", new VirtualKeyMapping(VirtualKeyCode.Down, true)));
        row.Children.Add(CreateKeyButton("→", new VirtualKeyMapping(VirtualKeyCode.Right, true)));
        row.Children.Add(CreateKeyButton("PgUp", new VirtualKeyMapping(VirtualKeyCode.PageUp, true)));
        row.Children.Add(CreateKeyButton("PgDn", new VirtualKeyMapping(VirtualKeyCode.PageDown, true)));
        return row;
    }

    private FlexLayout BuildSpaceEnterRow()
    {
        var row = NewFlexRow();
        row.Children.Add(CreateKeyButton("Espaço", new VirtualKeyMapping(VirtualKeyCode.Space, false), widthRequest: 160));
        row.Children.Add(CreateKeyButton("Enter", new VirtualKeyMapping(VirtualKeyCode.Enter, false), widthRequest: 120));
        return row;
    }

    private static FlexLayout NewFlexRow() => new()
    {
        Wrap = FlexWrap.Wrap,
        JustifyContent = FlexJustify.Start,
        AlignItems = FlexAlignItems.Center,
    };

    private Button CreateKeyButton(string label, VirtualKeyMapping mapping, double widthRequest = MinTouchSize)
    {
        var button = new Button
        {
            Text = label,
            MinimumHeightRequest = MinTouchSize,
            MinimumWidthRequest = widthRequest,
            Margin = 2,
        };
        AutomationProperties.SetName(button, label);
        button.Clicked += async (_, _) => await OnKeyTappedAsync(mapping);
        return button;
    }

    private Button CreateReleaseAllButton()
    {
        var button = new Button
        {
            Text = AppRes.RemoteControl_ReleaseAll,
            MinimumHeightRequest = MinTouchSize,
            Margin = 2,
        };
        AutomationProperties.SetName(button, AppRes.RemoteControl_ReleaseAll);
        AutomationProperties.SetHelpText(button, AppRes.RemoteControl_ReleaseAll_Hint);
        button.Clicked += (_, _) =>
        {
            foreach (var modifierButton in _modifierButtons.Values)
                SetModifierVisualState(modifierButton, active: false);
            _activeModifiers.Clear();
            ViewModel?.ReleaseAllModifiersCommand.Execute(null);
        };
        return button;
    }

    private Button CreateModifierButton(string label, VirtualKeyMapping mapping)
    {
        var button = new Button
        {
            Text = label,
            MinimumHeightRequest = MinTouchSize,
            MinimumWidthRequest = MinTouchSize,
            Margin = 2,
        };
        AutomationProperties.SetName(button, label);
        _modifierButtons[mapping] = button;

        button.Clicked += (_, _) =>
        {
            var active = !_activeModifiers.Contains(mapping);
            if (active)
                _activeModifiers.Add(mapping);
            else
                _activeModifiers.Remove(mapping);

            SetModifierVisualState(button, active);
        };

        return button;
    }

    private static void SetModifierVisualState(Button button, bool active)
    {
        button.BackgroundColor = active
            ? Application.Current?.Resources["Secondary"] as Color
            : null;
        SemanticScreenReader.Default.Announce($"{button.Text} {(active ? "ativado" : "desativado")}");
    }

    private async Task OnKeyTappedAsync(VirtualKeyMapping mapping)
    {
        if (ViewModel is null)
            return;

        var modifiersSnapshot = _activeModifiers.ToList();

        foreach (var modifier in modifiersSnapshot)
            await ViewModel.SendKeyDownAsync(modifier);

        await ViewModel.SendKeyAsync(mapping);

        foreach (var modifier in modifiersSnapshot)
            await ViewModel.SendKeyUpAsync(modifier);

        if (modifiersSnapshot.Count > 0)
        {
            _activeModifiers.Clear();
            foreach (var modifierButton in _modifierButtons.Values)
                SetModifierVisualState(modifierButton, active: false);
        }
    }
}
