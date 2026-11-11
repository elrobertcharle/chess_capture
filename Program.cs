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

    Console.WriteLine("Pulsa Enter para capturar cuando el tablero esté quieto, o q para salir.");
    Console.WriteLine("Mantén el tablero visible en las coordenadas calibradas.");
    string? pending = null;
    while (true)
    {
        Console.Write("> ");
        string? input = Console.ReadLine();
        if (input is null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
            break;
        if (input.Length != 0)
            continue;

        using var board = BoardVision.Capture();
        char[] observed = vision.Recognize(board, blackAtBottom);
        if (Array.IndexOf(observed, '?') >= 0)
        {
            Console.WriteLine("Hay casillas que no pude reconocer. Comprueba la vista y vuelve a capturar.");
            continue;
        }

        if (pending is not null && observed.AsSpan().SequenceEqual(position.Pieces))
        {
            Console.WriteLine($"Pendiente tu jugada: {pending}.");
            continue;
        }

        if (position.WhiteToMove != whitePlayer)
        {
            if (observed.AsSpan().SequenceEqual(position.Pieces))
            {
                Console.WriteLine("Aún no se ve una jugada del rival.");
                continue;
            }

            string? rival = position.MatchMove(observed, await engine.LegalMoves(position));
            if (rival is null)
            {
                Console.WriteLine("La captura no coincide con una jugada legal del rival. El historial se conserva.");
                if (pending is not null)
                    Console.WriteLine($"Comprueba que jugaste exactamente {pending}.");
                continue;
            }
            position.Play(rival);
            pending = null;
            Console.WriteLine($"Jugada del rival: {rival}");
        }
        else if (!observed.AsSpan().SequenceEqual(position.Pieces))
        {
            Console.WriteLine("La captura no coincide con el historial inicial.");
            continue;
        }

        string? recommendation = await engine.BestMove(position, 100);
        if (recommendation is null)
        {
            Console.WriteLine("La posición no tiene jugadas legales.");
            break;
        }
        using var currentBoard = BoardVision.Capture();
        if (!vision.Recognize(currentBoard, blackAtBottom).AsSpan().SequenceEqual(position.Pieces))
        {
            Console.WriteLine("La vista del tablero cambió durante la búsqueda. Vuelve a capturar.");
            continue;
        }

        Console.WriteLine($"Jugada de Stockfish: {recommendation}");
        try
        {
            await BoardMouse.Play(recommendation, blackAtBottom);
            if (recommendation.Length == 5)
                Console.WriteLine($"Completa la promoción eligiendo la pieza indicada por «{recommendation[4]}» (q dama, r torre, b alfil, n caballo).");
        }
        catch (Win32Exception error)
        {
            Console.WriteLine($"Windows no completó los clics: {error.Message}");
            Console.WriteLine($"Comprueba el tablero y completa manualmente {recommendation} si falta.");
        }
        position.Play(recommendation);
        pending = recommendation;
        Console.WriteLine("Vuelve a la consola y pulsa Enter cuando responda el rival.");
    }

    return 0;
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
