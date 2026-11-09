using NvdaRemoteDroid.ViewModels;

namespace NvdaRemoteDroid;

public partial class TrustedCertificatesPage : ContentPage
{
    public TrustedCertificatesPage(TrustedCertificatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
