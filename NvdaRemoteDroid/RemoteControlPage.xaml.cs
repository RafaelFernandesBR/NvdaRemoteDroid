using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.ViewModels;

namespace NvdaRemoteDroid;

public partial class RemoteControlPage : ContentPage
{
    private readonly IConnectionPreferencesService _preferencesService;

    private readonly RemoteControlViewModel _viewModel;

    public RemoteControlPage(RemoteControlViewModel viewModel, IConnectionPreferencesService preferencesService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _preferencesService = preferencesService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // O Shell mantém esta Page viva entre trocas de aba, então a preferência de tecla NVDA
        // só é reaplicada aqui — senão mudar nas Configurações exigiria reiniciar o app.
        var nvdaKeyMapping = _preferencesService.NvdaKey == VirtualKeyNvdaChoice.Insert
            ? new VirtualKeyMapping(VirtualKeyCode.Insert, Extended: true)
            : new VirtualKeyMapping(VirtualKeyCode.CapsLock, Extended: false);

        Keyboard.Initialize(_viewModel, nvdaKeyMapping);
    }
}
