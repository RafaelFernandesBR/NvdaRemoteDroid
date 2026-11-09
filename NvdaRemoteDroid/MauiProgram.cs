using CommunityToolkit.Maui;
using LocalizationResourceManager.Maui;
using Microsoft.Extensions.Logging;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.Core.Transport;
using NvdaRemoteDroid.Services;
using NvdaRemoteDroid.ViewModels;

namespace NvdaRemoteDroid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            // Internacionalização (PT-BR/EN) com troca em runtime, mesmo padrão do AcessGallery.
            .UseLocalizationResourceManager(settings =>
            {
                settings.AddResource(AppResources.ResourceManager);
                settings.RestoreLatestCulture(true);
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Services de estado: uma única instância compartilhada por toda a sessão do app.
        builder.Services.AddSingleton<ICertificateTrustStore, CertificateTrustStoreService>();
        builder.Services.AddSingleton<ITransportService, TcpTlsTransportService>();
        builder.Services.AddSingleton<IClipboardService, ClipboardService>();
        builder.Services.AddSingleton<IConnectionPreferencesService, ConnectionPreferencesService>();
        builder.Services.AddSingleton<IMotdSeenStore, MotdSeenStore>();

#if ANDROID
        builder.Services.AddSingleton<ISpeechOutputService, NativeSpeechOutputService>();
#endif

        // ViewModels e Pages: sempre transient, resolvidos pelo DI a cada navegação.
        builder.Services.AddTransient<ViewModels.ConnectionViewModel>();
        builder.Services.AddTransient<ViewModels.RemoteControlViewModel>();
        builder.Services.AddTransient<ViewModels.SettingsViewModel>();
        builder.Services.AddTransient<ViewModels.TrustedCertificatesViewModel>();

        builder.Services.AddTransient<ConnectionPage>();
        builder.Services.AddTransient<RemoteControlPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<TrustedCertificatesPage>();

        return builder.Build();
    }
}
