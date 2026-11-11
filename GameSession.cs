using System.ComponentModel;

sealed class GameSession(
    BoardVision vision, Stockfish engine, Position position, ReactionSettings reaction,
    bool whitePlayer)
{
    private readonly bool blackAtBottom = !whitePlayer;
    private ReactionSettings reactionSettings = reaction;
    private string? pending;
    private string? lastStatus;
    private List<string>? legalMoves;

    public void SetReactionSettings(ReactionSettings settings) => Volatile.Write(ref reactionSettings, settings);

    public async Task Run(Action<string> report, CancellationToken cancellationToken,
        Action<string?>? reportEvaluation = null)
    {
        lastStatus = null;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        try
        {
            await Monitor();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        async Task Monitor()
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                using var board = BoardVision.Capture();
                char[] observed = vision.Recognize(board, blackAtBottom);
                if (Array.IndexOf(observed, '?') >= 0)
                {
                    ShowStatus("Hay casillas que no pude reconocer. Esperando una vista clara del tablero.");
                    continue;
                }

                string? rival = null;
                var observedPosition = position;
                if (position.WhiteToMove != whitePlayer)
                {
                    if (observed.AsSpan().SequenceEqual(position.Pieces))
                    {
                        ShowStatus("Esperando la jugada del rival.");
                        continue;
                    }

                    legalMoves ??= await engine.LegalMoves(position);
                    rival = position.MatchMove(observed, legalMoves);
                    if (rival is null)
                    {
                        ShowStatus(pending is null
                            ? "Esperando una jugada rival que coincida con el historial."
                            : $"Esperando una posición compatible con tu jugada {pending} y la respuesta del rival.");
                        continue;
                    }
                    observedPosition = position.After(rival);
                }
                else if (!observed.AsSpan().SequenceEqual(position.Pieces))
                {
                    ShowStatus("Esperando que el tablero coincida con la posición guardada.");
                    continue;
                }

                var result = await engine.BestMove(observedPosition, 100);
                if (cancellationToken.IsCancellationRequested)
                    return;
                var settings = Volatile.Read(ref reactionSettings);
                int delay = (int)Random.Shared.NextInt64(settings.MinDelayMs, (long)settings.MaxDelayMs + 1);
                await Task.Delay(delay, cancellationToken);

                using var currentBoard = BoardVision.Capture();
                if (!vision.Recognize(currentBoard, blackAtBottom).AsSpan().SequenceEqual(observedPosition.Pieces))
                {
                    ShowStatus("La vista cambió durante la búsqueda. Esperando que se estabilice.");
                    continue;
                }

                if (cancellationToken.IsCancellationRequested)
                    return;

                if (rival is not null)
                {
                    position = observedPosition;
                    legalMoves = null;
                    pending = null;
                    report($"Jugada del rival: {rival}");
                }
                if (result is null)
                {
                    report("La posición no tiene jugadas legales.");
                    return;
                }

                // Una vez iniciada, la secuencia de clics se completa antes de pausar.
                string recommendation = result.Move;
                reportEvaluation?.Invoke(result.Evaluation);
                report($"Jugada de Stockfish: {result.Display}");
                try
                {
                    if (!await BoardMouse.Play(recommendation, blackAtBottom))
                        report($"No pude detectar el selector. Si sigue abierto, completa {recommendation} manualmente (q dama, n caballo, r torre, b alfil).");
                }
                catch (Win32Exception error)
                {
                    report($"Windows no completó los clics: {error.Message}");
                    report($"Comprueba el tablero y completa manualmente {recommendation} si falta.");
                }
                position.Play(recommendation);
                pending = recommendation;
                lastStatus = null;
            }
        }

        void ShowStatus(string status)
        {
            if (status == lastStatus)
                return;
            report(status);
            lastStatus = status;
        }
    }
}
