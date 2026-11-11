namespace NvdaRemoteDroid.Core.Gestures;

/// <summary>
/// Reconhecedor puro de gestos (sem MAUI/Android, sem timers): a tela alimenta com eventos de
/// toque (posição em dp, tempo em ms) e chama <see cref="Tick"/> enquanto <see cref="NeedsTick"/>
/// for true — é o Tick que libera o toque longo e o toque simples (que só vale depois que a
/// janela do toque duplo expira). Só acompanha o primeiro dedo; um segundo dedo apenas marca o
/// gesto como multitoque (toque com 2 dedos).
/// </summary>
public sealed class GestureRecognizer
{
    public const double SwipeMinDistanceDp = 48;
    public const double TapMaxMovementDp = 16;
    public const long LongPressMs = 500;
    public const long DoubleTapWindowMs = 300;

    private bool _down;
    private bool _multiTouch;
    private bool _consumed;
    private long _downAtMs;
    private double _startX;
    private double _startY;
    private double _maxMovement;

    // Primeiro toque de um possível toque duplo, esperando o segundo ou a janela expirar.
    private long? _pendingTapDeadlineMs;
    private bool _secondTapCandidate;

    /// <summary>True enquanto há dedo na tela ou um toque simples pendente: a tela deve chamar Tick.</summary>
    public bool NeedsTick => _down || _pendingTapDeadlineMs is not null;

    public void Down(long nowMs, double x, double y)
    {
        _down = true;
        _multiTouch = false;
        _consumed = false;
        _downAtMs = nowMs;
        _startX = x;
        _startY = y;
        _maxMovement = 0;

        // Um novo toque dentro da janela vira candidato a segundo toque do toque duplo. Se ele
        // acabar sendo outro gesto (deslize, toque longo...), o toque simples pendente é
        // descartado: decisão aceita, perder um toque é melhor que falar dois comandos seguidos.
        _secondTapCandidate = _pendingTapDeadlineMs is { } deadline && nowMs < deadline;
        _pendingTapDeadlineMs = null;
    }

    /// <summary>Um segundo dedo encostou: o gesto passa a ser um toque com 2 dedos (se não houver movimento).</summary>
    public void SecondFingerDown()
    {
        if (_down)
            _multiTouch = true;
    }

    public void Move(double x, double y)
    {
        // Com 2+ dedos as coordenadas recebidas (índice 0 do Android) podem ser do outro dedo,
        // quando o primeiro sai antes: contar esse "movimento" quebraria o toque com 2 dedos.
        if (!_down || _multiTouch)
            return;

        var distance = Distance(x, y);
        if (distance > _maxMovement)
            _maxMovement = distance;
    }

    /// <summary>Último dedo saiu. Devolve o gesto reconhecido, ou null (toque simples fica pendente até o Tick).</summary>
    public GestureKind? Up(long nowMs, double x, double y)
    {
        if (!_down)
            return null;

        Move(x, y);
        _down = false;

        if (_consumed)
            return null;

        if (_multiTouch)
        {
            _secondTapCandidate = false;
            return _maxMovement < TapMaxMovementDp && nowMs - _downAtMs < LongPressMs
                ? GestureKind.TwoFingerTap
                : null;
        }

        var dx = x - _startX;
        var dy = y - _startY;
        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) >= SwipeMinDistanceDp)
        {
            _secondTapCandidate = false;
            return Math.Abs(dx) >= Math.Abs(dy)
                ? (dx > 0 ? GestureKind.SwipeRight : GestureKind.SwipeLeft)
                : (dy > 0 ? GestureKind.SwipeDown : GestureKind.SwipeUp);
        }

        if (_maxMovement >= TapMaxMovementDp)
        {
            // Movimento ambíguo (nem toque nem deslize): descarta.
            _secondTapCandidate = false;
            return null;
        }

        if (_secondTapCandidate)
        {
            _secondTapCandidate = false;
            return GestureKind.DoubleTap;
        }

        _pendingTapDeadlineMs = nowMs + DoubleTapWindowMs;
        return null;
    }

    /// <summary>O sistema cancelou o toque (ACTION_CANCEL): descarta tudo sem emitir gesto.</summary>
    public void Cancel()
    {
        _down = false;
        _consumed = false;
        _secondTapCandidate = false;
        _pendingTapDeadlineMs = null;
    }

    /// <summary>Avança o relógio: libera o toque longo (dedo parado) ou o toque simples cuja janela expirou.</summary>
    public GestureKind? Tick(long nowMs)
    {
        if (_down)
        {
            if (!_consumed && !_multiTouch && _maxMovement < TapMaxMovementDp && nowMs - _downAtMs >= LongPressMs)
            {
                _consumed = true;
                _secondTapCandidate = false;
                return GestureKind.LongPress;
            }

            return null;
        }

        if (_pendingTapDeadlineMs is { } deadline && nowMs >= deadline)
        {
            _pendingTapDeadlineMs = null;
            return GestureKind.Tap;
        }

        return null;
    }

    private double Distance(double x, double y) =>
        Math.Sqrt(((x - _startX) * (x - _startX)) + ((y - _startY) * (y - _startY)));
}
