using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Resources.Strings;

namespace NvdaRemoteDroid.Core.ViewModels;

/// <summary>
/// Uma linha da lista "Seus favoritos". Os nomes acessíveis dos botões já vêm montados e
/// localizados (o DataTemplate só liga a propriedade), e IsFirst/IsLast desabilitam Subir/Descer nas bordas.
/// </summary>
public sealed record FavoriteEntry(
    FavoriteKey Favorite,
    string Name,
    string RemoveName,
    string MoveUpName,
    string MoveDownName,
    bool IsFirst,
    bool IsLast);

/// <summary>Lógica da tela "Teclas favoritas": escolher tecla + modificadores, remover e reordenar.</summary>
public abstract partial class FavoriteKeysViewModelBase : ObservableObject
{
    private readonly IConnectionPreferencesService _preferencesService;
    private readonly VirtualKeyMapping _nvdaKey;
    private readonly IReadOnlyList<VirtualKeyGroup> _sourceGroups;

    protected FavoriteKeysViewModelBase(IConnectionPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _nvdaKey = VirtualKeyboardLayout.NvdaKeyMapping(preferencesService.NvdaKey);
        _sourceGroups = VirtualKeyboardLayout.BuildSourceGroups(_nvdaKey);

        foreach (var group in _sourceGroups)
            Groups.Add(group);

        Reload();
    }

    /// <summary>Grupos com teclas reais (sem Favoritos nem Gestos).</summary>
    public ObservableCollection<VirtualKeyGroup> Groups { get; } = new();

    /// <summary>Teclas do grupo escolhido.</summary>
    public ObservableCollection<VirtualKey> Keys { get; } = new();

    public ObservableCollection<FavoriteEntry> Entries { get; } = new();

    [ObservableProperty]
    private VirtualKeyGroup? selectedGroup;

    [ObservableProperty]
    private VirtualKey? selectedKey;

    [ObservableProperty]
    private bool useCtrl;

    [ObservableProperty]
    private bool useAlt;

    [ObservableProperty]
    private bool useShift;

    [ObservableProperty]
    private bool useWin;

    [ObservableProperty]
    private bool useNvda;

    /// <summary>Anuncia uma mudança de estado ao leitor de tela (implementado na plataforma).</summary>
    protected virtual void Announce(string message) { }

    partial void OnSelectedGroupChanged(VirtualKeyGroup? value)
    {
        Keys.Clear();
        if (value is not null)
        {
            foreach (var key in value.Keys)
                Keys.Add(key);
        }

        // Sem pré-seleção: o usuário escolhe a tecla de propósito, para não favoritar a errada.
        SelectedKey = null;
    }

    [RelayCommand]
    private void Add()
    {
        if (SelectedKey is null)
        {
            Announce(AppResources.FavoriteKeys_NoKeySelected);
            return;
        }

        var favorite = new FavoriteKey(SelectedKey.Id, CurrentModifiers());
        var name = VirtualKeyFavorites.Compose(SelectedKey, favorite.Modifiers, _nvdaKey).Name;

        var current = _preferencesService.FavoriteKeys;
        var updated = VirtualKeyFavorites.Add(current, favorite);
        if (ReferenceEquals(updated, current))
        {
            Announce(string.Format(AppResources.FavoriteKeys_AlreadyExists, name));
            return;
        }

        _preferencesService.FavoriteKeys = updated;
        UseCtrl = UseAlt = UseShift = UseWin = UseNvda = false;
        Reload();
        Announce(string.Format(AppResources.FavoriteKeys_Added, name));
    }

    [RelayCommand]
    private void Remove(FavoriteEntry? entry)
    {
        if (entry is null)
            return;

        _preferencesService.FavoriteKeys = VirtualKeyFavorites.Remove(_preferencesService.FavoriteKeys, entry.Favorite);
        Reload();
        Announce(string.Format(AppResources.FavoriteKeys_Removed, entry.Name));
    }

    [RelayCommand]
    private void MoveUp(FavoriteEntry? entry) => MoveBy(entry, -1);

    [RelayCommand]
    private void MoveDown(FavoriteEntry? entry) => MoveBy(entry, +1);

    private void MoveBy(FavoriteEntry? entry, int delta)
    {
        if (entry is null)
            return;

        var current = _preferencesService.FavoriteKeys;
        var updated = VirtualKeyFavorites.Move(current, entry.Favorite, delta);
        if (ReferenceEquals(updated, current))
            return;

        _preferencesService.FavoriteKeys = updated;
        Reload();

        var position = updated.ToList().IndexOf(entry.Favorite) + 1;
        Announce(string.Format(AppResources.FavoriteKeys_Moved, entry.Name, position, updated.Count));
    }

    private FavoriteModifiers CurrentModifiers()
    {
        var modifiers = FavoriteModifiers.None;
        if (UseCtrl) modifiers |= FavoriteModifiers.Ctrl;
        if (UseAlt) modifiers |= FavoriteModifiers.Alt;
        if (UseShift) modifiers |= FavoriteModifiers.Shift;
        if (UseWin) modifiers |= FavoriteModifiers.Win;
        if (UseNvda) modifiers |= FavoriteModifiers.Nvda;
        return modifiers;
    }

    private void Reload()
    {
        var favorites = _preferencesService.FavoriteKeys;
        var byId = _sourceGroups.SelectMany(g => g.Keys).GroupBy(k => k.Id).ToDictionary(g => g.Key, g => g.First());

        // Só vale favorito cuja tecla ainda existe (o mesmo critério do grupo Favoritos). Órfãos
        // são removidos da preferência, senão Subir/Descer e "posição X de N" operariam sobre
        // entradas invisíveis.
        var visible = favorites.Where(f => byId.ContainsKey(f.KeyId)).ToList();
        if (visible.Count != favorites.Count)
            _preferencesService.FavoriteKeys = visible;

        Entries.Clear();
        for (var i = 0; i < visible.Count; i++)
        {
            var favorite = visible[i];
            var name = VirtualKeyFavorites.Compose(byId[favorite.KeyId], favorite.Modifiers, _nvdaKey).Name;
            Entries.Add(new FavoriteEntry(
                favorite,
                name,
                string.Format(AppResources.FavoriteKeys_Remove_Format, name),
                string.Format(AppResources.FavoriteKeys_MoveUp_Format, name),
                string.Format(AppResources.FavoriteKeys_MoveDown_Format, name),
                IsFirst: i == 0,
                IsLast: i == visible.Count - 1));
        }
    }
}
