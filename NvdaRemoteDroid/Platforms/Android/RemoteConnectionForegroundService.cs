using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Microsoft.Extensions.DependencyInjection;
using NvdaRemoteDroid.Core.Transport;
using AppRes = NvdaRemoteDroid.Core.Resources.Strings.AppResources;

namespace NvdaRemoteDroid;

/// <summary>
/// Serviço em primeiro plano que mantém o processo (e portanto o socket do relay e o TTS) vivo
/// com o app em segundo plano ou a tela bloqueada — sem ele o Android corta a rede do app em
/// poucos minutos ("Software caused connection abort"). Não tem lógica de conexão: só mostra a
/// notificação fixa com o status atual e a ação Desconectar. Quem o inicia/para é o
/// ForegroundConnectionController, conforme o estado do transporte.
/// Declarado à mão no AndroidManifest.xml (por isso [Register] com nome fixo, sem [Service]):
/// o tipo "specialUse" exige um elemento property dentro do service.
/// </summary>
[Register(JavaName)]
public sealed class RemoteConnectionForegroundService : Service
{
    public const string JavaName = "com.nvdaremotedroid.app.RemoteConnectionForegroundService";
    public const string ActionDisconnect = "com.nvdaremotedroid.app.action.DISCONNECT";
    public const string ActionStop = "com.nvdaremotedroid.app.action.STOP";

    private const string ChannelId = "remote_connection";
    private const int NotificationId = 6837;

    private ITransportService? _transportService;
    private PowerManager.WakeLock? _wakeLock;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();

        _transportService = IPlatformApplication.Current?.Services.GetService<ITransportService>();
        if (_transportService is not null)
            _transportService.StateChanged += OnTransportStateChanged;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionDisconnect)
        {
            // O controller para o serviço quando o estado virar Disconnected.
            _ = _transportService?.DisconnectAsync();
            return StartCommandResult.NotSticky;
        }

        var state = _transportService?.State ?? ConnectionState.Connecting;
        var notification = BuildNotification(state);

        // O tipo só existe (e só é exigido) a partir do Android 14; em versões anteriores o
        // manifesto não o reconhece e passá-lo aqui lançaria IllegalArgumentException.
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(NotificationId, notification, ForegroundService.TypeSpecialUse);
        else
            StartForeground(NotificationId, notification);

        if (intent?.Action == ActionStop)
        {
            // Parar passa por aqui (em vez de Context.StopService) para que o StartForeground
            // acima sempre aconteça: parar um serviço iniciado com StartForegroundService antes
            // de ele chamar StartForeground derruba o app.
            // StopSelfResult(startId), não StopSelf(): se um start mais novo (reconexão logo
            // após desconectar) já está na fila, o serviço continua de pé para atendê-lo — um
            // start descartado sem StartForeground também derruba o app.
            ReleaseWakeLock();
            if (StopSelfResult(startId))
                StopForeground(StopForegroundFlags.Remove);
            return StartCommandResult.NotSticky;
        }

        UpdateWakeLock(state);

        // NotSticky: se o sistema matar o processo, a conexão morreu junto — recriar o serviço
        // sozinho só deixaria uma notificação "Conectado" mentirosa.
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (_transportService is not null)
            _transportService.StateChanged -= OnTransportStateChanged;

        ReleaseWakeLock();
        base.OnDestroy();
    }

    private void OnTransportStateChanged(object? sender, ConnectionState state)
    {
        // Estados finais são tratados pelo controller (que para o serviço); aqui só atualiza o
        // texto enquanto o serviço continua de pé.
        // O wake lock é sempre reavaliado, mesmo em estado final: se a parada do serviço falhar,
        // a CPU não pode ficar presa acordada.
        UpdateWakeLock(state);

        if (!ConnectionStateDescriber.IsConnectedOrConnecting(state))
            return;

        NotificationManagerCompat.From(this)?.Notify(NotificationId, BuildNotification(state));
    }

    private Notification BuildNotification(ConnectionState state)
    {
        EnsureChannel();

        var openAppIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? string.Empty);
        var openAppPending = openAppIntent is null
            ? null
            : PendingIntent.GetActivity(this, 0, openAppIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var disconnectIntent = new Intent(this, typeof(RemoteConnectionForegroundService)).SetAction(ActionDisconnect);
        var disconnectPending = PendingIntent.GetService(this, 1, disconnectIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var icon = ApplicationInfo?.Icon is int appIcon and not 0 ? appIcon : Android.Resource.Drawable.StatNotifySync;

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(AppRes.Notification_Title)!
            .SetContentText(ConnectionStateDescriber.Describe(state))!
            .SetSmallIcon(icon)!
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .SetCategory(NotificationCompat.CategoryService)!
            .SetPriority(NotificationCompat.PriorityLow)!
            .AddAction(0, AppRes.Connection_Disconnect, disconnectPending)!;

        if (openAppPending is not null)
            builder.SetContentIntent(openAppPending);

        return builder.Build()!;
    }

    private void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null || manager.GetNotificationChannel(ChannelId) is not null)
            return;

        // Importância baixa: notificação fixa e silenciosa, sem som a cada mudança de status.
        manager.CreateNotificationChannel(new NotificationChannel(ChannelId, AppRes.Notification_Channel, NotificationImportance.Low));
    }

    /// <summary>
    /// Wake lock parcial só enquanto autenticado: mantém a CPU processando a fala recebida com
    /// a tela apagada. Em reconexão (Error/Connecting) é liberado, para um relay fora do ar não
    /// segurar a CPU acordada indefinidamente — o laço de reconexão continua nas janelas em que
    /// o sistema acordar o processo.
    /// </summary>
    private void UpdateWakeLock(ConnectionState state)
    {
        if (state != ConnectionState.Authenticated)
        {
            ReleaseWakeLock();
            return;
        }

        if (_wakeLock is { IsHeld: true })
            return;

        try
        {
            var powerManager = (PowerManager?)GetSystemService(PowerService);
            _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "NvdaRemoteDroid:RemoteSession");
            _wakeLock?.SetReferenceCounted(false);
            _wakeLock?.Acquire();
        }
        catch (Java.Lang.Exception)
        {
            // Sem wake lock a sessão ainda funciona com a tela ligada.
        }
    }

    private void ReleaseWakeLock()
    {
        try
        {
            if (_wakeLock is { IsHeld: true })
                _wakeLock.Release();
        }
        catch (Java.Lang.Exception)
        {
        }

        _wakeLock = null;
    }
}
