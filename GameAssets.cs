static class GameAssets
{
    public static async Task<(BoardVision Vision, Stockfish Engine, Position Position)>
        Open(IReadOnlyList<string> history, bool? captureWhiteToMove = null)
    {
        var vision = new BoardVision(Find("board_position.bmp"));
        var position = new Position();
        if (captureWhiteToMove is bool whiteToMove)
        {
            using var firstFrame = BoardVision.Capture();
            char[] pieces = vision.Recognize(firstFrame, !whiteToMove);
            position = Position.FromBoard(pieces, whiteToMove);

            await Task.Delay(200);
            using var secondFrame = BoardVision.Capture();
            if (!vision.Recognize(secondFrame, !whiteToMove).AsSpan().SequenceEqual(pieces))
                throw new InvalidOperationException("El tablero cambió durante la captura. Espera a que se estabilice y vuelve a intentar.");
        }

        var engine = new Stockfish(Find(Path.Combine("stockfish", "stockfish-windows-x86-64-universal.exe")));
        try
        {
            await engine.Initialize();
            foreach (string move in history)
            {
                if (!Position.IsMove(move) || !(await engine.LegalMoves(position)).Contains(move))
                    throw new ArgumentException($"Jugada ilegal en el historial: {move}");
                position.Play(move);
            }
            return (vision, engine, position);
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    public static string Find(string relativePath)
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
}
