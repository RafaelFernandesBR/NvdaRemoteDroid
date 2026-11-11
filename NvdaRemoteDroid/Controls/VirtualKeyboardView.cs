using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.ViewModels;
using AppRes = NvdaRemoteDroid.Core.Resources.Strings.AppResources;

namespace NvdaRemoteDroid.Controls;

/// <summary>
/// Teclado virtual na tela, construído em código a partir de VirtualKeyboardLayout (Core).
/// Três blocos, de cima para baixo: modificadores (Ctrl/Alt/Shift/Win/NVDA), seletor de grupo
/// e as teclas do grupo escolhido — um grupo por vez, para a tela nunca ter dezenas de botões
/// de uma vez para o leitor de tela percorrer. Tudo em Grid de colunas iguais: nada de
/// FlexLayout/rolagem horizontal, que cortavam as teclas para fora da largura da tela.
/// </summary>
public sealed class VirtualKeyboardView : ContentView
{
    // 48dp: o mínimo do app é 44, mas o Android (e o verificador de acessibilidade) recomenda 48.
    private const double MinTouchSize = 48;
    private const double KeySpacing = 6;

    private enum ModifierState
    {
        Off,
        Once,   // vale só para a próxima tecla
        Locked, // vale até ser solto
    }

    private sealed record ModifierButton(string Label, VirtualKeyMapping Mapping, Button Button);

    // VirtualKeyMapping completo (não só o VkCode): o bit Extended da tecla Windows precisa
    // sobreviver até o down/up ser enviado, senão o follower recebe Win sem KEYEVENTF_EXTENDEDKEY.
    private readonly HashSet<VirtualKeyMapping> _activeModifiers = [];
    private readonly HashSet<VirtualKeyMapping> _lockedModifiers = [];
    private readonly List<ModifierButton> _modifierButtons = [];
    private readonly Dictionary<VirtualKeyboardGroupId, Button> _groupButtons = [];

    private RemoteControlViewModel? _viewModel;
    private IConnectionPreferencesService? _preferencesService;
    private IReadOnlyList<VirtualKeyGroup> _groups = [];
    private VirtualKeyMapping? _builtForNvdaKey;
    private string? _builtForFavorites;
    private VirtualKeyboardGroupId _currentGroupId;

    private ISpeechOutputService? _speech;
    private GesturePadView? _gesturePad;
    private Grid? _keysGrid;
    private Label? _groupHintLabel;
    private int _keyColumns;
    private bool _sending;

    public VirtualKeyboardView()
    {
        SizeChanged += (_, _) => RebuildKeysIfColumnCountChanged();
    }

    /// <summary>
    /// Monta o teclado. Chamado a cada OnAppearing da página, mas só reconstrói quando a tecla
    /// NVDA configurada mudou — reconstruir sempre jogaria fora o grupo aberto e os
    /// modificadores travados toda vez que o usuário troca de aba.
    /// </summary>
    public void Initialize(
        RemoteControlViewModel viewModel,
        IConnectionPreferencesService preferencesService,
        ISpeechOutputService? speech = null)
    {
        _viewModel = viewModel;
        _preferencesService = preferencesService;
        _speech = speech;

        var nvdaKey = VirtualKeyboardLayout.NvdaKeyMapping(preferencesService.NvdaKey);
        var favorites = preferencesService.FavoriteKeys;
        var favoritesSnapshot = VirtualKeyFavorites.Serialize(favorites);
        if (_builtForNvdaKey == nvdaKey)
        {
            // Só os favoritos mudaram (editados em Configurações): refaz os grupos e, se o grupo
            // aberto é o Favoritos, só as teclas — sem perder modificadores travados nem o grupo.
            if (_builtForFavorites != favoritesSnapshot)
            {
                _builtForFavorites = favoritesSnapshot;
                _groups = VirtualKeyboardLayout.Build(nvdaKey, favorites);
                if (_currentGroupId == VirtualKeyboardGroupId.Favorites)
                    ShowGroup(_currentGroupId, announce: false);
            }

            _gesturePad?.Initialize(viewModel, preferencesService, speech);
            return;
        }

        _builtForNvdaKey = nvdaKey;
        _builtForFavorites = favoritesSnapshot;
        _groups = VirtualKeyboardLayout.Build(nvdaKey, favorites);
        _currentGroupId = preferencesService.KeyboardGroup;
        _activeModifiers.Clear();
        _lockedModifiers.Clear();
        _modifierButtons.Clear();
        _groupButtons.Clear();

        Content = BuildLayout(nvdaKey);
        _gesturePad?.Initialize(viewModel, preferencesService, speech);
        ShowGroup(_currentGroupId, announce: false);
    }

    private VerticalStackLayout BuildLayout(VirtualKeyMapping nvdaKey)
    {
        var root = new VerticalStackLayout { Spacing = 12 };

        root.Children.Add(CreateHeading(AppRes.RemoteControl_Modifiers_Title));
        root.Children.Add(BuildModifierGrid(nvdaKey));

        root.Children.Add(CreateHeading(AppRes.RemoteControl_Groups_Title));
        root.Children.Add(BuildGroupSelector());

        _groupHintLabel = new Label { IsVisible = false };
        root.Children.Add(_groupHintLabel);

        _keysGrid = new Grid { ColumnSpacing = KeySpacing, RowSpacing = KeySpacing };
        root.Children.Add(_keysGrid);

        // A área de gestos ocupa o lugar da grade de teclas quando o grupo Gestos está aberto.
        _gesturePad = new GesturePadView { IsVisible = false };
        root.Children.Add(_gesturePad);

        return root;
    }

    private static Label CreateHeading(string text)
    {
        var label = new Label { Text = text };
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level2);
        return label;
    }

    // ---- Modificadores -------------------------------------------------------------------

    private Grid BuildModifierGrid(VirtualKeyMapping nvdaKey)
    {
        var grid = NewEqualColumnsGrid(columns: 3);
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        grid.Add(CreateModifierButton(AppRes.RemoteControl_ModifierCtrl, VirtualKeyboardLayout.Ctrl), 0, 0);
        grid.Add(CreateModifierButton(AppRes.RemoteControl_ModifierAlt, VirtualKeyboardLayout.Alt), 1, 0);
        grid.Add(CreateModifierButton(AppRes.RemoteControl_ModifierShift, VirtualKeyboardLayout.Shift), 2, 0);
        grid.Add(CreateModifierButton(AppRes.RemoteControl_ModifierWin, VirtualKeyboardLayout.Win), 0, 1);
        grid.Add(CreateModifierButton(AppRes.RemoteControl_NvdaKey, nvdaKey), 1, 1);
        grid.Add(CreateReleaseAllButton(), 2, 1);

        return grid;
    }

    private Button CreateModifierButton(string label, VirtualKeyMapping mapping)
    {
        var button = CreateBaseButton(label);
        SetAccessibleHint(button, AppRes.RemoteControl_Modifier_Hint);

        var modifier = new ModifierButton(label, mapping, button);
        _modifierButtons.Add(modifier);
        ApplyModifierVisual(modifier, announce: false);

        button.Clicked += (_, _) =>
        {
            switch (GetState(mapping))
            {
                case ModifierState.Off:
                    _activeModifiers.Add(mapping);
                    break;
                case ModifierState.Once:
                    _lockedModifiers.Add(mapping);
                    break;
                default:
                    _activeModifiers.Remove(mapping);
                    _lockedModifiers.Remove(mapping);
                    break;
            }

            ApplyModifierVisual(modifier, announce: true);
        };

        return button;
    }

    private Button CreateReleaseAllButton()
    {
        var button = CreateBaseButton(AppRes.RemoteControl_ReleaseAll_Short);
        SetAccessibleName(button, AppRes.RemoteControl_ReleaseAll);
        SetAccessibleHint(button, AppRes.RemoteControl_ReleaseAll_Hint);

        button.Clicked += (_, _) =>
        {
            _activeModifiers.Clear();
            _lockedModifiers.Clear();
            foreach (var modifier in _modifierButtons)
                ApplyModifierVisual(modifier, announce: false);

            // Par VK_NONE: reseta os modificadores no follower sem disparar nenhuma ação.
            _viewModel?.ReleaseAllModifiersCommand.Execute(null);
            SemanticScreenReader.Default.Announce(AppRes.RemoteControl_ModifiersReleased);
        };

        return button;
    }

    private ModifierState GetState(VirtualKeyMapping mapping)
    {
        if (!_activeModifiers.Contains(mapping))
            return ModifierState.Off;

        return _lockedModifiers.Contains(mapping) ? ModifierState.Locked : ModifierState.Once;
    }

    private void ApplyModifierVisual(ModifierButton modifier, bool announce)
    {
        var state = GetState(modifier.Mapping);
        var description = string.Format(state switch
        {
            ModifierState.Once => AppRes.RemoteControl_ModifierState_Once,
            ModifierState.Locked => AppRes.RemoteControl_ModifierState_Locked,
            _ => AppRes.RemoteControl_ModifierState_Off,
        }, modifier.Label);

        // O estado vai no texto do botão também (não só na cor): "Ctrl", "Ctrl •" (uma tecla), "Ctrl ••" (travado).
        modifier.Button.Text = state switch
        {
            ModifierState.Once => modifier.Label + " •",
            ModifierState.Locked => modifier.Label + " ••",
            _ => modifier.Label,
        };
        SetAccessibleName(modifier.Button, description);
        SetHighlighted(modifier.Button, state != ModifierState.Off);

        if (announce)
            SemanticScreenReader.Default.Announce(description);
    }

    // ---- Seletor de grupo -----------------------------------------------------------------

    private Grid BuildGroupSelector()
    {
        const int columns = 2;
        var grid = NewEqualColumnsGrid(columns);

        for (var i = 0; i < _groups.Count; i++)
        {
            if (i % columns == 0)
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var group = _groups[i];
            var button = CreateBaseButton(group.Title);
            SetAccessibleHint(button, AppRes.RemoteControl_Group_Hint);
            button.Clicked += (_, _) => ShowGroup(group.Id, announce: true);

            _groupButtons[group.Id] = button;
            grid.Add(button, i % columns, i / columns);
        }

        return grid;
    }

    private void ShowGroup(VirtualKeyboardGroupId groupId, bool announce)
    {
        var group = _groups.FirstOrDefault(g => g.Id == groupId) ?? _groups[0];
        _currentGroupId = group.Id;

        foreach (var candidate in _groups)
        {
            var button = _groupButtons[candidate.Id];
            var selected = candidate.Id == group.Id;
            SetAccessibleName(button, selected
                ? string.Format(AppRes.RemoteControl_GroupSelected, candidate.Title)
                : candidate.Title);
            SetHighlighted(button, selected);
        }

        if (_groupHintLabel is not null)
        {
            _groupHintLabel.Text = group.Hint ?? string.Empty;
            _groupHintLabel.IsVisible = group.Hint is not null;
        }

        var isGestures = group.Id == VirtualKeyboardGroupId.Gestures;
        if (_gesturePad is not null)
            _gesturePad.IsVisible = isGestures;

        if (_keysGrid is not null)
            _keysGrid.IsVisible = !isGestures;

        var hasKeys = group.Keys.Count > 0;
        if (isGestures || !hasKeys)
        {
            // Sem teclas (Gestos, ou Favoritos vazio): nada de colunas (Math.Clamp com 0 teclas
            // lançaria exceção). No Favoritos vazio o hint explica como adicionar.
            _keysGrid?.Children.Clear();
        }
        else
        {
            _keyColumns = ComputeColumns(group);
            BuildKeys(group, _keyColumns);
        }

        if (announce)
        {
            if (_preferencesService is not null)
                _preferencesService.KeyboardGroup = group.Id;

            SemanticScreenReader.Default.Announce(isGestures && _gesturePad is not null
                ? _gesturePad.GroupAnnouncement
                : !hasKeys && group.Hint is not null
                    ? $"{group.Title}. {group.Hint}"
                    : string.Format(AppRes.RemoteControl_GroupAnnounce, group.Title, group.Keys.Count));
        }
    }

    // ---- Teclas do grupo ------------------------------------------------------------------

    private int ComputeColumns(VirtualKeyGroup group)
    {
        // Antes do primeiro layout a largura ainda é desconhecida (-1): assume um celular em
        // retrato e deixa o SizeChanged corrigir.
        var available = Width > 0 ? Width : 320;
        var columns = (int)Math.Floor((available + KeySpacing) / (group.MinKeyWidth + KeySpacing));
        return Math.Clamp(columns, 1, group.Keys.Count);
    }

    private void RebuildKeysIfColumnCountChanged()
    {
        if (_keysGrid is null || _groups.Count == 0)
            return;

        var group = _groups.FirstOrDefault(g => g.Id == _currentGroupId) ?? _groups[0];
        if (group.Id == VirtualKeyboardGroupId.Gestures || group.Keys.Count == 0)
            return;

        var columns = ComputeColumns(group);
        if (columns == _keyColumns)
            return;

        _keyColumns = columns;
        BuildKeys(group, columns);
    }

    private void BuildKeys(VirtualKeyGroup group, int columns)
    {
        if (_keysGrid is null)
            return;

        _keysGrid.Children.Clear();
        _keysGrid.ColumnDefinitions.Clear();
        _keysGrid.RowDefinitions.Clear();

        for (var column = 0; column < columns; column++)
            _keysGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (var i = 0; i < group.Keys.Count; i++)
        {
            if (i % columns == 0)
                _keysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            _keysGrid.Add(CreateKeyButton(group.Keys[i]), i % columns, i / columns);
        }
    }

    private Button CreateKeyButton(VirtualKey key)
    {
        var button = CreateBaseButton(key.DisplayLabel);
        SetAccessibleName(button, key.Name);
        button.Clicked += async (_, _) => await OnKeyTappedAsync(key);
        return button;
    }

    private async Task OnKeyTappedAsync(VirtualKey key)
    {
        // Handler async void: nada pode escapar daqui. _sending evita intercalar os down/up de
        // dois toques rápidos (o segundo é descartado, não enfileirado).
        if (_viewModel is null || _sending)
            return;

        _sending = true;
        try
        {
            // Modificadores ativos primeiro, depois os do próprio atalho, sem repetir.
            var modifiers = _activeModifiers.ToList();
            foreach (var modifier in key.Modifiers)
            {
                if (!modifiers.Contains(modifier))
                    modifiers.Add(modifier);
            }

            await _viewModel.SendChordAsync(modifiers, key.Mapping);

            // Os de "uma tecla só" se soltam sozinhos; os travados continuam.
            // Sem anúncio: falaria por cima da resposta do NVDA à tecla recém-enviada.
            foreach (var modifier in _modifierButtons)
            {
                if (GetState(modifier.Mapping) != ModifierState.Once)
                    continue;

                _activeModifiers.Remove(modifier.Mapping);
                ApplyModifierVisual(modifier, announce: false);
            }
        }
        catch (Exception)
        {
            // Falha ao enfileirar uma tecla não pode derrubar o app; a conexão trata a queda.
        }
        finally
        {
            _sending = false;
        }
    }

    // ---- Auxiliares -----------------------------------------------------------------------

    /// <summary>
    /// Nome/dica lidos pelo leitor de tela. SemanticProperties é o que o TalkBack de fato usa
    /// (verificado no emulador: só com AutomationProperties.Name ele lia o Text do botão e o
    /// estado "ativado/selecionado" nunca era falado); AutomationProperties é mantido junto por
    /// compatibilidade com o restante do app.
    /// </summary>
    private static void SetAccessibleName(Button button, string name)
    {
        SemanticProperties.SetDescription(button, name);
        AutomationProperties.SetName(button, name);
    }

    private static void SetAccessibleHint(Button button, string hint)
    {
        SemanticProperties.SetHint(button, hint);
        AutomationProperties.SetHelpText(button, hint);
    }

    private static Grid NewEqualColumnsGrid(int columns)
    {
        var grid = new Grid { ColumnSpacing = KeySpacing, RowSpacing = KeySpacing };
        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        return grid;
    }

    private static Button CreateBaseButton(string text)
    {
        var button = new Button
        {
            Text = text,
            MinimumHeightRequest = MinTouchSize,
            MinimumWidthRequest = MinTouchSize,
            // Padding horizontal menor que o do estilo padrão (14): com várias colunas o texto
            // de uma tecla não pode ser cortado; rótulos longos quebram linha.
            Padding = new Thickness(4, 10),
            LineBreakMode = LineBreakMode.WordWrap,
        };
        SetAccessibleName(button, text);
        return button;
    }

    /// <summary>
    /// Destaque de "ativo/selecionado" usando só tokens de Colors.xaml, com par claro/escuro e
    /// borda além da cor de fundo (estado nunca só por cor). ClearValue devolve o botão ao
    /// estilo implícito de Styles.xaml.
    /// </summary>
    private static void SetHighlighted(Button button, bool highlighted)
    {
        if (!highlighted)
        {
            button.ClearValue(VisualElement.BackgroundColorProperty);
            button.ClearValue(Button.TextColorProperty);
            button.ClearValue(Button.BorderColorProperty);
            button.ClearValue(Button.BorderWidthProperty);
            return;
        }

        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        if (TryGetColor(dark ? "White" : "Tertiary", out var background))
            button.BackgroundColor = background;
        if (TryGetColor(dark ? "Black" : "White", out var text))
            button.TextColor = text;
        if (TryGetColor("Magenta", out var border))
            button.BorderColor = border;
        button.BorderWidth = 3;
    }

    private static bool TryGetColor(string key, out Color color)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color found)
        {
            color = found;
            return true;
        }

        color = Colors.Transparent;
        return false;
    }
}
