sealed class Position
{
    private readonly List<string> moves = new();
    public char[] Pieces { get; } = Initial();
    public bool WhiteToMove => moves.Count % 2 == 0;
    public string Uci => moves.Count == 0 ? "position startpos" :
        "position startpos moves " + string.Join(' ', moves);

    public static char[] Initial() => (
        "rnbqkbnr" + "pppppppp" + new string('.', 32) + "PPPPPPPP" + "RNBQKBNR").ToCharArray();

    public void Play(string move)
    {
        Apply(Pieces, move);
        moves.Add(move);
    }

    public static bool IsMove(string move) =>
        move.Length is 4 or 5 && IsSquare(move[0], move[1]) && IsSquare(move[2], move[3]) &&
        (move.Length == 4 || "qrbn".Contains(move[4]));

    private static bool IsSquare(char file, char rank) => file is >= 'a' and <= 'h' && rank is >= '1' and <= '8';

    private static int Index(char file, char rank) => ('8' - rank) * 8 + file - 'a';

    // Solo se aplica a jugadas que Stockfish ya validó como legales.
    public static void Apply(char[] pieces, string move)
    {
        int from = Index(move[0], move[1]);
        int to = Index(move[2], move[3]);
        char piece = pieces[from];
        switch (char.ToLowerInvariant(piece))
        {
            case 'p' when from % 8 != to % 8 && pieces[to] == '.':
                pieces[from / 8 * 8 + to % 8] = '.';
                break;
            case 'k' when Math.Abs(to - from) == 2:
                int rook = to > from ? from + 3 : from - 4;
                pieces[(from + to) / 2] = pieces[rook];
                pieces[rook] = '.';
                break;
        }
        pieces[from] = '.';
        pieces[to] = move.Length == 5
            ? (char.IsUpper(piece) ? char.ToUpperInvariant(move[4]) : move[4])
            : piece;
    }

    public string? MatchMove(char[] observed, IReadOnlyList<string> legalMoves)
    {
        string? match = null;
        foreach (string move in legalMoves)
        {
            var expected = (char[])Pieces.Clone();
            Apply(expected, move);
            if (!expected.AsSpan().SequenceEqual(observed))
                continue;
            if (match is not null)
                return null;
            match = move;
        }
        return match;
    }
}
