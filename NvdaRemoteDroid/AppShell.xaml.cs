namespace NvdaRemoteDroid;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(TrustedCertificatesPage), typeof(TrustedCertificatesPage));
    }
}
