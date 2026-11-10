using Android.Content;
using AndroidX.Core.Content;
using Microsoft.Extensions.Logging;
using NvdaRemoteDroid.Core.Transport;
using Application = Android.App.Application;

namespace NvdaRemoteDroid;

/// <summary>
/// Liga/desliga o RemoteConnectionForegroundService conforme o estado do transporte: de pé
/// enquanto há conexão ativa ou em tentativa (inclui o backoff de reconexão), parado em
/// Disconnected/AuthFailed/VersionMismatch. Singleton criado na inicialização do app.
/// </summary>
public sealed class ForegroundConnectionController
{
    private readonly ILogger<ForegroundConnectionController> _logger;
    private bool _running;

    public ForegroundConnectionController(ITransportService transportService, ILogger<ForegroundConnectionController> logger)
    {
        _logger = logger;
        transportService.StateChanged += OnTransportStateChanged;
    }

    private void OnTransportStateChanged(object? sender, ConnectionState state)
    {
        var shouldRun = ConnectionStateDescriber.IsConnectedOrConnecting(state);
        if (shouldRun == _running)
            return;

        var context = Application.Context;
        var intent = new Intent(context, typeof(RemoteConnectionForegroundService));

        try
        {
            // Parar também é um "start" com ActionStop: o próprio serviço se encerra depois de
            // cumprir o StartForeground obrigatório (ver RemoteConnectionForegroundService).
            if (!shouldRun)
                intent.SetAction(RemoteConnectionForegroundService.ActionStop);

            ContextCompat.StartForegroundService(context, intent);

            _running = shouldRun;
        }
        catch (Java.Lang.Exception ex)
        {
            // Ex.: ForegroundServiceStartNotAllowedException se o sistema considerar o app em
            // segundo plano neste instante. A conexão segue funcionando com o app aberto.
            _logger.LogWarning("Não foi possível {Action} o serviço em primeiro plano: {Error}",
                shouldRun ? "iniciar" : "parar", ex.GetType().Name);

            if (!shouldRun)
            {
                // Último recurso para não deixar a notificação "conectado" presa.
                try { context.StopService(new Intent(context, typeof(RemoteConnectionForegroundService))); }
                catch (Java.Lang.Exception) { }
                _running = false;
            }
        }
    }
}
