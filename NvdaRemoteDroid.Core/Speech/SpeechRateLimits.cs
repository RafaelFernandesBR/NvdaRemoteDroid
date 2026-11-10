namespace NvdaRemoteDroid.Core.Speech;

/// <summary>Limites da velocidade da fala (multiplicador; 1.0 = normal).</summary>
public static class SpeechRateLimits
{
    public const float Min = 0.5f;
    public const float Max = 2.0f;
    public const float Default = 1.0f;

    public static float Clamp(float value) =>
        float.IsNaN(value) ? Default : Math.Clamp(value, Min, Max);
}
