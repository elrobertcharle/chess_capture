using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;

try
{
    bool selfCheck = args is ["--verificar"];
    bool whitePlayer = true;
    bool blackAtBottom = false;
    var history = new List<string>();
    foreach (string arg in args)
    {
        switch (arg)
        {
            case "--verificar" when selfCheck:
                history.Add("e2e4");
                break;
            case "--negras":
                whitePlayer = false;
                break;
            case "--negras-abajo":
                blackAtBottom = true;
                break;
            default:
                if (!Position.IsMove(arg))
                    throw new ArgumentException($"Argumento desconocido: {arg}");
                history.Add(arg);
                break;
        }
    }

    var reaction = ReactionSettings.Load(FindAsset("reaction.json"));
    string referencePath = FindAsset("board_position.bmp");
    var vision = new BoardVision(referencePath);
    using var engine = new Stockfish(FindAsset(Path.Combine("stockfish",
        "stockfish-windows-x86-64-universal.exe")));
    await engine.Initialize();
    var position = new Position();
    foreach (string move in history)
    {
        if (!(await engine.LegalMoves(position)).Contains(move))
            throw new ArgumentException($"Jugada ilegal en el historial: {move}");
        position.Play(move);
    }

    if (selfCheck)
    {
        using var reference = new Bitmap(referencePath);
        using var board = reference.Clone(BoardVision.BoardArea, PixelFormat.Format24bppRgb);
        var observed = vision.Recognize(board, false);
        string? rivalMove = position.MatchMove(observed, await engine.LegalMoves(position));
        if (rivalMove != "c7c5")
            throw new InvalidOperationException($"Se esperaba c7c5 y se detectó {rivalMove ?? "ninguna jugada"}.");
        position.Play(rivalMove);
        string? best = await engine.BestMove(position, 100);
        if (best is null || !(await engine.LegalMoves(position)).Contains(best))
            throw new InvalidOperationException("Stockfish no devolvió una jugada legal.");
        Console.WriteLine($"Jugada detectada: {rivalMove}. Respuesta de Stockfish: {best}.");
        return 0;
    }

    Console.WriteLine("Mantén el tablero visible en las coordenadas calibradas.");
    Console.WriteLine(whitePlayer ? "Juegas con blancas." : "Juegas con negras.");
    if (!whitePlayer)
    {
        Console.WriteLine("Mueve la consola fuera del tablero y pulsa Enter para iniciar, o escribe q para salir.");
        while (true)
        {
            string? input = Console.ReadLine();
            if (input is null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (input.Length == 0)
                break;
            Console.WriteLine("Pulsa Enter para iniciar, o escribe q para salir.");
        }
    }

    Console.WriteLine("Revisión automática cada 200 ms. Para salir, pulsa Ctrl+C en la consola.");
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        stop.Cancel();
    };

    string? pending = null;
    string? lastStatus = null;
    List<string>? legalMoves = null;
    bool orientationKnown = whitePlayer || blackAtBottom;
    while (!stop.IsCancellationRequested && await timer.WaitForNextTickAsync())
    {
        if (stop.IsCancellationRequested)
            break;

        using var board = BoardVision.Capture();
        char[] observed = vision.Recognize(board, blackAtBottom);
        if (Array.IndexOf(observed, '?') >= 0)
        {
            ShowStatus("Hay casillas que no pude reconocer. Esperando una vista clara del tablero.");
            continue;
        }

        if (!orientationKnown)
        {
            bool waitingForWhite = position.WhiteToMove;
            if (waitingForWhite)
                legalMoves ??= await engine.LegalMoves(position);

            if (!position.TryOrient(observed, legalMoves, out blackAtBottom))
            {
                ShowStatus("Esperando la posición inicial o la primera jugada de blancas.");
                continue;
            }
            orientationKnown = true;
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

        string? recommendation = await engine.BestMove(observedPosition, 100);
        if (stop.IsCancellationRequested)
            break;
        try
        {
            int delay = (int)Random.Shared.NextInt64(reaction.MinDelayMs, (long)reaction.MaxDelayMs + 1);
            await Task.Delay(delay, stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            break;
        }

        using var currentBoard = BoardVision.Capture();
        if (!vision.Recognize(currentBoard, blackAtBottom).AsSpan().SequenceEqual(observedPosition.Pieces))
        {
            ShowStatus("La vista cambió durante la búsqueda. Esperando que se estabilice.");
            continue;
        }

        if (stop.IsCancellationRequested)
            break;

        if (rival is not null)
        {
            position = observedPosition;
            legalMoves = null;
            pending = null;
            Console.WriteLine($"Jugada del rival: {rival}");
        }
        if (recommendation is null)
        {
            Console.WriteLine("La posición no tiene jugadas legales.");
            break;
        }

        Console.WriteLine($"Jugada de Stockfish: {recommendation}");
        try
        {
            if (!await BoardMouse.Play(recommendation, blackAtBottom))
                Console.WriteLine($"No pude detectar el selector. Si sigue abierto, completa {recommendation} manualmente (q dama, n caballo, r torre, b alfil).");
        }
        catch (Win32Exception error)
        {
            Console.WriteLine($"Windows no completó los clics: {error.Message}");
            Console.WriteLine($"Comprueba el tablero y completa manualmente {recommendation} si falta.");
        }
        position.Play(recommendation);
        pending = recommendation;
        lastStatus = null;
    }

    return 0;

    void ShowStatus(string status)
    {
        if (status == lastStatus)
            return;
        Console.WriteLine(status);
        lastStatus = status;
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error is OperationCanceledException
        ? "Stockfish no respondió a tiempo."
        : error.Message);
    return 1;
}

static string FindAsset(string relativePath)
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
         directory is not null;
         directory = directory.Parent)
    {
        string candidate = Path.Combine(directory.FullName, relativePath);
        if (File.Exists(candidate))
            return candidate;
    }
    throw new FileNotFoundException($"No se encontró {relativePath}.");
}
