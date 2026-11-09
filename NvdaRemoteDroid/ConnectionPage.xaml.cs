using NvdaRemoteDroid.ViewModels;

namespace NvdaRemoteDroid;

public partial class ConnectionPage : ContentPage
{
    public ConnectionPage(ConnectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
