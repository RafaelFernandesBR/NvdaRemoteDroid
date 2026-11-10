using NvdaRemoteDroid.Core.Gateway;
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
        // é reconferida aqui (o teclado só se reconstrói se ela mudou) — senão mudar nas
        // Configurações exigiria reiniciar o app.
        Keyboard.Initialize(_viewModel, _preferencesService);
    }
}
