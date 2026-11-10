using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.ViewModels;

namespace NvdaRemoteDroid.ViewModels;

/// <summary>Implementação Android da tela de Configurações.</summary>
public sealed partial class SettingsViewModel : SettingsViewModelBase
{
    private readonly IAppLogService _appLogService;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        IConnectionPreferencesService preferencesService,
        IAppLogService appLogService,
        ILogger<SettingsViewModel> logger) : base(preferencesService)
    {
        _appLogService = appLogService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task ShareLogsAsync()
    {
        if (!_appLogService.HasLogs)
        {
            await Shell.Current.DisplayAlertAsync(AppResources.Common_Warning, AppResources.Settings_ShareLogs_Empty, AppResources.Common_Ok);
            return;
        }

        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = AppResources.Settings_ShareLogs_Title,
                File = new ShareFile(_appLogService.LogFilePath),
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao compartilhar logs");
            await Shell.Current.DisplayAlertAsync(AppResources.Common_Error, AppResources.Settings_ShareLogs_Error, AppResources.Common_Ok);
        }
    }
}
