using NvdaRemoteDroid.ViewModels;

namespace NvdaRemoteDroid;

public partial class FavoriteKeysPage : ContentPage
{
    public FavoriteKeysPage(FavoriteKeysViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
