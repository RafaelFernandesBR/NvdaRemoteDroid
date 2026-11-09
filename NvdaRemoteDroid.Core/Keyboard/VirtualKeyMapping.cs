namespace NvdaRemoteDroid.Core.Keyboard;

/// <summary>Resultado de mapear uma tecla Android (física ou virtual) para o VK do Windows a enviar.</summary>
public readonly record struct VirtualKeyMapping(VirtualKeyCode VkCode, bool Extended);
