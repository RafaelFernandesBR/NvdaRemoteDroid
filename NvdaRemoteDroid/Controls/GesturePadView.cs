using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Gestures;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.ViewModels;
using PadGestureRecognizer = NvdaRemoteDroid.Core.Gestures.GestureRecognizer;
using AppRes = NvdaRemoteDroid.Core.Resources.Strings.AppResources;
#if ANDROID
using AndroidContext = global::Android.Content.Context;
using AndroidMotionEvent = global::Android.Views.MotionEvent;
using AndroidMotionEventActions = global::Android.Views.MotionEventActions;
using AndroidView = global::Android.Views.View;
using AccessibilityManager = global::Android.Views.Accessibility.AccessibilityManager;
#endif

namespace NvdaRemoteDroid.Controls;

/// <summary>
/// Área de gestos da tela de Controle: os gestos de um dedo viram comandos do NVDA (scripts via
/// braille_input) ou teclas, conforme o modo (Objetos, Texto, Setas). O reconhecimento em si é
/// GestureRecognizer (Core); aqui só há a ponte com o toque do Android.
/// Com o TalkBack ligado o toque de um dedo não chega ao app (o TalkBack o consome): a área
/// avisa e o usuário pausa o TalkBack; a troca de modo é falada pelo TTS do app, porque com o
/// TalkBack pausado o SemanticScreenReader.Announce fica mudo.
/// </summary>
public sealed class GesturePadView : ContentView
{
    private const double MinTouchSize = 48;

    private readonly PadGestureRecognizer _recognizer = new();
    private readonly Button _modeButton;
    private readonly Label _warningLabel;
    private readonly Border _pad;
    private readonly Label _padLabel;

    private RemoteControlViewModel? _viewModel;
    private IConnectionPreferencesService? _preferencesService;
    private ISpeechOutputService? _speech;
    private GesturePadMode _mode;
    private IDispatcherTimer? _timer;

#if ANDROID
    private AndroidView? _platformView;
    private AccessibilityManager? _accessibilityManager;
    private TouchExplorationListener? _touchExplorationListener;
#endif

    public GesturePadView()
    {
        _modeButton = new Button
        {
            MinimumHeightRequest = MinTouchSize,
            MinimumWidthRequest = MinTouchSize,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        _modeButton.Clicked += (_, _) => CycleMode();

        _warningLabel = new Label { IsVisible = false, Text = AppRes.GesturePad_TalkBackWarning };

        _padLabel = new Label
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 22,
        };
        // O rótulo é só visual: o nome acessível é o da própria área.
        AutomationProperties.SetIsInAccessibleTree(_padLabel, false);

        _pad = new Border
        {
            HeightRequest = 300,
            MinimumHeightRequest = MinTouchSize,
            Padding = new Thickness(12),
            Content = _padLabel,
        };
        ApplyPadBackground();
        _pad.HandlerChanged += (_, _) => AttachPlatformTouch();

        Content = new VerticalStackLayout
        {
            Spacing = 12,
            Children = { _modeButton, _warningLabel, _pad },
        };

        Unloaded += (_, _) => StopTracking();
    }

    /// <summary>Chamado a cada OnAppearing da página; só relê o modo salvo e os textos.</summary>
    public void Initialize(
        RemoteControlViewModel viewModel,
        IConnectionPreferencesService preferencesService,
        ISpeechOutputService? speech)
    {
        _viewModel = viewModel;
        _preferencesService = preferencesService;
        _speech = speech;
        _mode = preferencesService.GesturePadMode;

        RefreshTexts();
        UpdateTalkBackWarning();
        StartTracking();
    }

    /// <summary>Texto lido ao abrir o grupo Gestos pelo seletor de grupos.</summary>
    public string GroupAnnouncement => string.Format(AppRes.GesturePad_GroupAnnounce, ModeName(_mode));

    // ---- Modo ----------------------------------------------------------------------------

    private static string ModeName(GesturePadMode mode) => AppRes.GetString(mode.NameKey());

    private void CycleMode()
    {
        _mode = _mode.Next();
        if (_preferencesService is not null)
            _preferencesService.GesturePadMode = _mode;

        RefreshTexts();
        Say(string.Format(AppRes.GesturePad_ModeSpoken, ModeName(_mode)));
    }

    private void RefreshTexts()
    {
        var modeName = ModeName(_mode);
        var buttonText = string.Format(AppRes.GesturePad_ModeButton, modeName);
        _modeButton.Text = buttonText;
        SemanticProperties.SetDescription(_modeButton, buttonText);
        AutomationProperties.SetName(_modeButton, buttonText);
        SemanticProperties.SetHint(_modeButton, AppRes.GesturePad_ModeButton_Hint);
        AutomationProperties.SetHelpText(_modeButton, AppRes.GesturePad_ModeButton_Hint);

        _padLabel.Text = modeName;
        _warningLabel.Text = AppRes.GesturePad_TalkBackWarning;

        var areaName = string.Format(AppRes.GesturePad_Area_Name, modeName);
        SemanticProperties.SetDescription(_pad, areaName);
        SemanticProperties.SetHint(_pad, AppRes.GesturePad_Area_Hint);
        AutomationProperties.SetName(_pad, areaName);
        AutomationProperties.SetHelpText(_pad, AppRes.GesturePad_Area_Hint);
    }

    /// <summary>
    /// Feedback local (troca de modo, gestos ativos). Com o TalkBack ligado ele fala; sem ele,
    /// o Announce ficaria mudo, então fala pelo TTS do app.
    /// </summary>
    private void Say(string text)
    {
        if (IsTouchExplorationOn())
            SemanticScreenReader.Default.Announce(text);
        else
            _speech?.SpeakSample(text);
    }

    private void ApplyPadBackground()
    {
        if (TryGetColor("Gray100", out var light) && TryGetColor("Gray900", out var dark))
            _pad.SetAppThemeColor(VisualElement.BackgroundColorProperty, light, dark);
    }

    private static bool TryGetColor(string key, out Color color)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color found)
        {
            color = found;
            return true;
        }

        color = Colors.Transparent;
        return false;
    }

    // ---- Gestos reconhecidos -------------------------------------------------------------

    private void OnGesture(GestureKind gesture)
    {
        var action = GesturePadActionMap.Resolve(_mode, gesture);
        switch (action)
        {
            case null:
                return;
            case GesturePadAction.CycleMode:
                CycleMode();
                return;
            default:
                _ = RunActionAsync(action);
                return;
        }
    }

    private async Task RunActionAsync(GesturePadAction action)
    {
        // Fire-and-forget a partir do toque: nada pode escapar daqui.
        if (_viewModel is null)
            return;

        try
        {
            await _viewModel.ExecuteGestureActionAsync(action);
        }
        catch (Exception)
        {
            // Falha ao enfileirar não pode derrubar o app; a conexão trata a queda.
        }
    }

    // ---- Timer (toque simples após a janela do duplo, toque longo) -----------------------

    private void EnsureTimerRunning()
    {
        _timer ??= CreateTimer();
        if (!_timer.IsRunning)
            _timer.Start();
    }

    private IDispatcherTimer CreateTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(40);
        timer.IsRepeating = true;
        timer.Tick += (_, _) =>
        {
            var gesture = _recognizer.Tick(Environment.TickCount64);
            if (gesture is { } found)
                OnGesture(found);

            if (!_recognizer.NeedsTick)
                timer.Stop();
        };
        return timer;
    }

#if ANDROID
    // ---- Toque (Android) -----------------------------------------------------------------

    private void AttachPlatformTouch()
    {
        DetachPlatformTouch();

        if (_pad.Handler?.PlatformView is AndroidView view)
        {
            _platformView = view;
            view.Touch += OnPlatformTouch;
        }
    }

    private void DetachPlatformTouch()
    {
        if (_platformView is not null)
            _platformView.Touch -= OnPlatformTouch;

        _platformView = null;
    }

    private void OnPlatformTouch(object? sender, AndroidView.TouchEventArgs e)
    {
        var motion = e.Event;
        if (motion is null)
            return;

        e.Handled = true;

        var density = DeviceDisplay.Current.MainDisplayInfo.Density;
        if (density <= 0)
            density = 1;

        var now = Environment.TickCount64;
        var x = motion.GetX() / density;
        var y = motion.GetY() / density;

        switch (motion.ActionMasked)
        {
            case AndroidMotionEventActions.Down:
                // A página rola: sem isto o ScrollView roubaria os deslizes verticais.
                _platformView?.Parent?.RequestDisallowInterceptTouchEvent(true);
                _recognizer.Down(now, x, y);
                EnsureTimerRunning();
                break;

            case AndroidMotionEventActions.PointerDown:
                _recognizer.SecondFingerDown();
                break;

            case AndroidMotionEventActions.Move:
                _recognizer.Move(x, y);
                break;

            case AndroidMotionEventActions.Up:
                _platformView?.Parent?.RequestDisallowInterceptTouchEvent(false);
                var gesture = _recognizer.Up(now, x, y);
                if (gesture is { } found)
                    OnGesture(found);

                if (_recognizer.NeedsTick)
                    EnsureTimerRunning();
                break;

            case AndroidMotionEventActions.Cancel:
                _platformView?.Parent?.RequestDisallowInterceptTouchEvent(false);
                _recognizer.Cancel();
                break;
        }
    }

    // ---- TalkBack ------------------------------------------------------------------------

    private bool IsTouchExplorationOn() => GetAccessibilityManager()?.IsTouchExplorationEnabled == true;

    private static AccessibilityManager? GetAccessibilityManager() =>
        global::Android.App.Application.Context.GetSystemService(AndroidContext.AccessibilityService) as AccessibilityManager;

    private void StartTracking()
    {
        if (_touchExplorationListener is not null)
            return;

        _accessibilityManager = GetAccessibilityManager();
        if (_accessibilityManager is null)
            return;

        _touchExplorationListener = new TouchExplorationListener(OnTouchExplorationChanged);
        _accessibilityManager.AddTouchExplorationStateChangeListener(_touchExplorationListener);
    }

    private void StopTracking()
    {
        _timer?.Stop();

        if (_accessibilityManager is not null && _touchExplorationListener is not null)
            _accessibilityManager.RemoveTouchExplorationStateChangeListener(_touchExplorationListener);

        _touchExplorationListener?.Dispose();
        _touchExplorationListener = null;
        _accessibilityManager = null;
        _recognizer.Cancel();
    }

    private void OnTouchExplorationChanged(bool enabled)
    {
        UpdateTalkBackWarning();

        // TalkBack pausado: confirma com a voz do app que os gestos já valem.
        if (!enabled && IsGroupVisible())
            _speech?.SpeakSample(string.Format(AppRes.GesturePad_Active, ModeName(_mode)));
    }

    private bool IsGroupVisible() => IsVisible && Handler is not null;

    private void UpdateTalkBackWarning() => _warningLabel.IsVisible = IsTouchExplorationOn();

    private sealed class TouchExplorationListener : Java.Lang.Object, AccessibilityManager.ITouchExplorationStateChangeListener
    {
        private readonly Action<bool> _onChanged;

        public TouchExplorationListener(Action<bool> onChanged) => _onChanged = onChanged;

        public void OnTouchExplorationStateChanged(bool enabled) => _onChanged(enabled);
    }
#else
    private void AttachPlatformTouch() { }

    private static bool IsTouchExplorationOn() => false;

    private void StartTracking() { }

    private void StopTracking() => _recognizer.Cancel();

    private void UpdateTalkBackWarning() => _warningLabel.IsVisible = false;
#endif
}
