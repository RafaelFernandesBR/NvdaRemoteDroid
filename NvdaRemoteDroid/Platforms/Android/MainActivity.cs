using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Extensions.DependencyInjection;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// Teclado físico/Bluetooth: só intercepta teclas enquanto a RemoteControlPage está em
    /// primeiro plano — nas demais telas (ex. Entry da chave de canal), o evento segue o
    /// fluxo normal do Android, senão digitar ali seria impossível.
    /// </summary>
    /// <remarks>
    /// Também não intercepta sem sessão autenticada (a tecla seria engolida sem ir a lugar
    /// nenhum) nem com um campo de texto em foco — o campo "Digitar texto" fica nesta mesma
    /// página, e Backspace/Enter do teclado na tela chegam por este mesmo caminho.
    /// </remarks>
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is not null &&
            Shell.Current?.CurrentPage is RemoteControlPage &&
            CurrentFocus is not Android.Widget.EditText &&
            (e.Action == KeyEventActions.Down || e.Action == KeyEventActions.Up) &&
            AndroidKeyToVirtualKeyMap.TryMap((int)e.KeyCode, out var mapping))
        {
            var transportService = IPlatformApplication.Current?.Services.GetService<ITransportService>();
            if (transportService is { State: ConnectionState.Authenticated })
            {
                _ = transportService.SendAsync(KeyEventFactory.Create(mapping, pressed: e.Action == KeyEventActions.Down));
                return true;
            }
        }

        return base.DispatchKeyEvent(e);
    }
}
