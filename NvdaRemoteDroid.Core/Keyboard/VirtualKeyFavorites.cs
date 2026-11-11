using System.Text.Json;
using NvdaRemoteDroid.Core.Resources.Strings;

namespace NvdaRemoteDroid.Core.Keyboard;

/// <summary>
/// Modificadores que um favorito pode carregar. O NVDA é um símbolo (não um VK fixo) para que o
/// favorito siga a tecla NVDA configurada (Insert/CapsLock) mesmo se o usuário trocá-la depois.
/// A ordem dos bits é a ordem em que o nome é montado ("Ctrl + Alt + Shift + Win + NVDA + tecla").
/// </summary>
[Flags]
public enum FavoriteModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
    Nvda = 16,
}

/// <summary>
/// Uma tecla/atalho favorito: o Id estável da tecla de origem (VirtualKey.Id) mais os
/// modificadores extras escolhidos pelo usuário.
/// </summary>
public sealed record FavoriteKey(string KeyId, FavoriteModifiers Modifiers);

/// <summary>Lógica pura (sem MAUI) dos favoritos: persistência em JSON, resolução em teclas e edição da lista.</summary>
public static class VirtualKeyFavorites
{
    private static readonly FavoriteModifiers[] ModifierOrder =
    [
        FavoriteModifiers.Ctrl,
        FavoriteModifiers.Alt,
        FavoriteModifiers.Shift,
        FavoriteModifiers.Win,
        FavoriteModifiers.Nvda,
    ];

    // ---- Persistência ---------------------------------------------------------------------

    public static string Serialize(IReadOnlyList<FavoriteKey> favorites) => JsonSerializer.Serialize(favorites);

    /// <summary>Nunca lança: JSON vazio/corrompido vira lista vazia; entradas inválidas são descartadas.</summary>
    public static IReadOnlyList<FavoriteKey> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var stored = JsonSerializer.Deserialize<List<FavoriteKey?>>(json) ?? [];
            var result = new List<FavoriteKey>(stored.Count);
            foreach (var favorite in stored)
            {
                if (favorite is null || string.IsNullOrWhiteSpace(favorite.KeyId))
                    continue;

                if (!result.Contains(favorite))
                    result.Add(favorite);
            }

            return result;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return [];
        }
    }

    // ---- Resolução ------------------------------------------------------------------------

    /// <summary>
    /// Transforma os favoritos em teclas prontas para o teclado. Ids que não existem mais são
    /// ignorados. Os modificadores do favorito são somados aos da própria tecla, sem repetir.
    /// </summary>
    public static IReadOnlyList<VirtualKey> Resolve(
        IEnumerable<VirtualKeyGroup> sourceGroups,
        IReadOnlyList<FavoriteKey> favorites,
        VirtualKeyMapping nvdaKey)
    {
        var byId = new Dictionary<string, VirtualKey>();
        foreach (var key in sourceGroups.SelectMany(g => g.Keys))
            byId.TryAdd(key.Id, key);

        var resolved = new List<VirtualKey>(favorites.Count);
        foreach (var favorite in favorites)
        {
            if (byId.TryGetValue(favorite.KeyId, out var key))
                resolved.Add(Compose(key, favorite.Modifiers, nvdaKey));
        }

        return resolved;
    }

    /// <summary>Tecla de origem + modificadores extras, com nome e rótulo "Ctrl + C".</summary>
    public static VirtualKey Compose(VirtualKey key, FavoriteModifiers extra, VirtualKeyMapping nvdaKey)
    {
        if (extra == FavoriteModifiers.None)
            return key;

        var modifiers = new List<VirtualKeyMapping>(key.Modifiers);
        foreach (var flag in ModifierOrder)
        {
            if (!extra.HasFlag(flag))
                continue;

            var mapping = ToMapping(flag, nvdaKey);
            if (!modifiers.Contains(mapping))
                modifiers.Add(mapping);
        }

        var prefix = ModifiersText(extra);
        return key with
        {
            Modifiers = modifiers,
            CustomName = $"{prefix} + {key.Name}",
            CustomLabel = $"{prefix} + {key.DisplayLabel}",
        };
    }

    public static VirtualKeyMapping ToMapping(FavoriteModifiers modifier, VirtualKeyMapping nvdaKey) => modifier switch
    {
        FavoriteModifiers.Ctrl => VirtualKeyboardLayout.Ctrl,
        FavoriteModifiers.Alt => VirtualKeyboardLayout.Alt,
        FavoriteModifiers.Shift => VirtualKeyboardLayout.Shift,
        FavoriteModifiers.Win => VirtualKeyboardLayout.Win,
        FavoriteModifiers.Nvda => nvdaKey,
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Use um único modificador."),
    };

    /// <summary>"Ctrl + Shift" (localizado), na ordem fixa dos modificadores.</summary>
    public static string ModifiersText(FavoriteModifiers modifiers) => string.Join(
        " + ",
        ModifierOrder.Where(m => modifiers.HasFlag(m)).Select(m => m switch
        {
            FavoriteModifiers.Ctrl => AppResources.RemoteControl_ModifierCtrl,
            FavoriteModifiers.Alt => AppResources.RemoteControl_ModifierAlt,
            FavoriteModifiers.Shift => AppResources.RemoteControl_ModifierShift,
            FavoriteModifiers.Win => AppResources.RemoteControl_ModifierWin,
            _ => AppResources.RemoteControl_NvdaKey,
        }));

    // ---- Edição da lista (imutável: devolve uma lista nova) -------------------------------

    public static IReadOnlyList<FavoriteKey> Add(IReadOnlyList<FavoriteKey> favorites, FavoriteKey favorite) =>
        favorites.Contains(favorite) ? favorites : [.. favorites, favorite];

    public static IReadOnlyList<FavoriteKey> Remove(IReadOnlyList<FavoriteKey> favorites, FavoriteKey favorite) =>
        favorites.Where(f => f != favorite).ToList();

    /// <summary>Move o favorito <paramref name="delta"/> posições (-1 sobe, +1 desce); nas bordas não muda nada.</summary>
    public static IReadOnlyList<FavoriteKey> Move(IReadOnlyList<FavoriteKey> favorites, FavoriteKey favorite, int delta)
    {
        var list = favorites.ToList();
        var index = list.IndexOf(favorite);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= list.Count)
            return favorites;

        list.RemoveAt(index);
        list.Insert(target, favorite);
        return list;
    }
}
