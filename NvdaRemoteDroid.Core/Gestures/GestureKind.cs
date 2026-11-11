namespace NvdaRemoteDroid.Core.Gestures;

/// <summary>Gestos reconhecidos pela área de gestos (1 dedo, exceto TwoFingerTap).</summary>
public enum GestureKind
{
    Tap,
    DoubleTap,
    LongPress,
    SwipeLeft,
    SwipeRight,
    SwipeUp,
    SwipeDown,
    TwoFingerTap,
}

/// <summary>Modo da área de gestos: o que cada gesto faz no NVDA. Local ao app (como os modos de toque do NVDA).</summary>
public enum GesturePadMode
{
    Object,
    Text,
    Arrows,
}

public static class GesturePadModes
{
    /// <summary>Próximo modo, em ciclo (Objeto -> Texto -> Setas -> Objeto).</summary>
    public static GesturePadMode Next(this GesturePadMode mode) => mode switch
    {
        GesturePadMode.Object => GesturePadMode.Text,
        GesturePadMode.Text => GesturePadMode.Arrows,
        _ => GesturePadMode.Object,
    };

    /// <summary>Chave de recurso do nome localizado do modo.</summary>
    public static string NameKey(this GesturePadMode mode) => mode switch
    {
        GesturePadMode.Object => "GesturePad_Mode_Object",
        GesturePadMode.Text => "GesturePad_Mode_Text",
        _ => "GesturePad_Mode_Arrows",
    };
}
