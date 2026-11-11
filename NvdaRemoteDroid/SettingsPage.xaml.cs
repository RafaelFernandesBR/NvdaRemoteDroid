using NvdaRemoteDroid.ViewModels;

namespace NvdaRemoteDroid;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnFavoriteKeysClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(FavoriteKeysPage));

    private async void OnTrustedCertificatesClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(TrustedCertificatesPage));
}
