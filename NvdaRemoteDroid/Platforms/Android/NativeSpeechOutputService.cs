using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Speech.Tts;
using Microsoft.Extensions.Logging;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Speech;
using Application = Android.App.Application;
using AndroidTts = Android.Speech.Tts.TextToSpeech;

namespace NvdaRemoteDroid.Services;

/// <summary>
/// TTS nativo Android (Android.Speech.Tts.TextToSpeech, não o Essentials) para poder honrar
/// QUEUE_FLUSH (priority=Now) e um pause/resume best-effort — o Essentials não expõe nenhum
/// dos dois. A "voz" é o MOTOR de TTS instalado (Google, RHVoice, Eloquence…): fala com a voz
/// padrão configurada nele. Inicialização é lazy: falas anteriores ao OnInit ficam numa fila
/// local e são despejadas quando o motor fica pronto. Se o motor escolhido falhar, cai no
/// motor padrão do dispositivo para nunca deixar o app mudo.
/// </summary>
public sealed class NativeSpeechOutputService : ISpeechOutputService
{
    private readonly ILogger<NativeSpeechOutputService> _logger;
    private readonly Queue<(string Utterance, SpeechPriority Priority)> _pendingBeforeReady = new();
    private readonly object _gate = new();
    private const int MaxPendingUtterances = 50;

    // Motores cuja init falhou ou que foram trocados: mantidos vivos (não dispostos) para que um
    // OnInit tardio numa thread nativa não toque um peer Java já descartado.
    private readonly List<EngineHandle> _retired = new();

    private EngineHandle? _current;
    private string _enginePackage;
    private float _rate;
    private bool _ready;
    private bool _paused;

    public NativeSpeechOutputService(IConnectionPreferencesService preferences, ILogger<NativeSpeechOutputService> logger)
    {
        _logger = logger;
        _enginePackage = preferences.TtsEnginePackage ?? string.Empty;
        _rate = SpeechRateLimits.Clamp(preferences.SpeechRate);

        lock (_gate)
            StartEngine(_enginePackage);
    }

    public IReadOnlyList<TtsEngineInfo> GetInstalledEngines()
    {
        // Lista via PackageManager (sem inicializar motor algum): o <queries> do TTS_SERVICE no
        // manifest torna esses serviços visíveis no Android 11+.
        var list = new List<TtsEngineInfo>();
        try
        {
            var pm = Application.Context.PackageManager;
            if (pm != null)
            {
                var intent = new Intent(AndroidTts.Engine.IntentActionTtsService);
                var services = pm.QueryIntentServices(intent, (PackageInfoFlags)0);
                foreach (var ri in services)
                {
                    var package = ri.ServiceInfo?.PackageName;
                    if (string.IsNullOrEmpty(package))
                        continue;

                    var label = ri.LoadLabel(pm)?.ToString();
                    list.Add(new TtsEngineInfo(package, string.IsNullOrWhiteSpace(label) ? package : label!));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao listar os motores de TTS.");
        }

        return list.DistinctBy(e => e.Id).OrderBy(e => e.Display, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void Configure(string? enginePackage, float rate)
    {
        var package = enginePackage ?? string.Empty;
        lock (_gate)
        {
            _rate = SpeechRateLimits.Clamp(rate);

            if (package != _enginePackage || _current is null)
            {
                _enginePackage = package;
                if (_current is { } old)
                    Retire(old);
                StartEngine(package);
                return;
            }

            if (_ready)
                _current?.Tts?.SetSpeechRate(_rate);
        }
    }

    public void Speak(string utterance, SpeechPriority priority)
    {
        lock (_gate)
        {
            if (_paused)
                return;

            SpeakOrQueue(utterance, priority);
        }
    }

    public void SpeakSample(string text)
    {
        lock (_gate)
            SpeakOrQueue(text, SpeechPriority.Now);
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _pendingBeforeReady.Clear();
            _current?.Tts?.Stop();
        }
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            _paused = paused;
            if (paused)
                _current?.Tts?.Stop(); // sem fila real a retomar: best-effort
        }
    }

    private void SpeakOrQueue(string utterance, SpeechPriority priority)
    {
        if (!_ready || _current?.Tts is null)
        {
            _pendingBeforeReady.Enqueue((utterance, priority));
            while (_pendingBeforeReady.Count > MaxPendingUtterances)
                _pendingBeforeReady.Dequeue(); // motor sem pronto: descarta as mais antigas
            return;
        }

        SpeakInternal(utterance, priority);
    }

    private void SpeakInternal(string utterance, SpeechPriority priority)
    {
        var queueMode = priority == SpeechPriority.Now ? QueueMode.Flush : QueueMode.Add;
        _current?.Tts?.Speak(utterance, queueMode, null, Guid.NewGuid().ToString("N"));
    }

    // ---- Gestão do motor (sempre sob _gate) ----

    private void StartEngine(string package)
    {
        var handle = new EngineHandle(package, OnEngineInit);
        _current = handle;
        _ready = false;

        try
        {
            handle.Tts = string.IsNullOrEmpty(package)
                ? new AndroidTts(Application.Context, handle)
                : new AndroidTts(Application.Context, handle, package);

            // OnInit pode ter chegado de forma síncrona, dentro do construtor, antes de Tts existir.
            if (handle.InitSucceeded)
                FinishInit(handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao criar o motor de TTS {Engine}.", package);
            Retire(handle);
            if (!string.IsNullOrEmpty(package))
                StartEngine(string.Empty); // fallback: motor padrão (a preferência do usuário é mantida)
        }
    }

    private void OnEngineInit(EngineHandle handle, bool success)
    {
        lock (_gate)
        {
            // OnInit tardio de um motor já trocado/aposentado: ignora.
            if (handle != _current)
                return;

            if (!success)
            {
                // Vale também para falha síncrona (Tts ainda nulo): Retire tolera Tts nulo.
                _logger.LogWarning("Falha ao iniciar o motor de TTS {Engine}.", handle.Package);
                Retire(handle);
                if (!string.IsNullOrEmpty(handle.Package))
                    StartEngine(string.Empty); // fallback: motor padrão
                return;
            }

            handle.InitSucceeded = true;
            if (handle.Tts is not null)
                FinishInit(handle); // senão, StartEngine finaliza ao atribuir Tts
        }
    }

    private void FinishInit(EngineHandle handle)
    {
        if (handle != _current || handle.Tts is null)
            return;

        handle.Tts.SetSpeechRate(_rate);
        _ready = true;

        while (_pendingBeforeReady.Count > 0)
        {
            var (utterance, priority) = _pendingBeforeReady.Dequeue();
            SpeakInternal(utterance, priority);
        }
    }

    private void Retire(EngineHandle handle)
    {
        if (ReferenceEquals(handle, _current))
        {
            _current = null;
            _ready = false;
        }

        try { handle.Tts?.Stop(); } catch { /* melhor esforço */ }
        try { handle.Tts?.Shutdown(); } catch { /* melhor esforço */ }
        _retired.Add(handle); // mantém vivo: evita callback nativo em peer coletado
    }

    /// <summary>
    /// Um motor de TTS e seu listener de init. Precisa derivar de Java.Lang.Object para satisfazer
    /// a interface Java-bound IOnInitListener (classes C# puras não implementam IJavaPeerable).
    /// </summary>
    private sealed class EngineHandle(string package, Action<EngineHandle, bool> onInit)
        : Java.Lang.Object, AndroidTts.IOnInitListener
    {
        public string Package { get; } = package;
        public AndroidTts? Tts { get; set; }
        public bool InitSucceeded { get; set; }

        public void OnInit([GeneratedEnum] OperationResult status) =>
            onInit(this, status == OperationResult.Success);
    }
}
