using System.Text;

sealed class Position
{
    private readonly List<string> moves = new();
    private readonly bool initialWhiteToMove = true;
    public char[] Pieces { get; } = Initial();
    public string? StartingFen { get; }
    public bool WhiteToMove => (moves.Count % 2 == 0) == initialWhiteToMove;
    public string Uci => (StartingFen is null ? "position startpos" : $"position fen {StartingFen}") +
        (moves.Count == 0 ? "" : " moves " + string.Join(' ', moves));

    public Position()
    {
    }

    private Position(char[] pieces, bool whiteToMove, string? fen)
    {
        Pieces = (char[])pieces.Clone();
        initialWhiteToMove = whiteToMove;
        StartingFen = fen;
    }

    public static char[] Initial() => (
        "rnbqkbnr" + "pppppppp" + new string('.', 32) + "PPPPPPPP" + "RNBQKBNR").ToCharArray();

    public void Play(string move)
    {
        Apply(Pieces, move);
        moves.Add(move);
    }

    public Position After(string move)
    {
        var next = new Position(Pieces, initialWhiteToMove, StartingFen);
        next.moves.AddRange(moves);
        next.Play(move);
        return next;
    }

    public static Position FromBoard(char[] pieces, bool whiteToMove)
    {
        if (pieces.Length != 64 || pieces.Any(piece => !".PNBRQKpnbrqk".Contains(piece)))
            throw new ArgumentException("No pude reconocer todas las casillas del tablero.");

        if (pieces.Count(piece => piece == 'K') != 1 || pieces.Count(piece => piece == 'k') != 1 ||
            pieces.Count(char.IsUpper) > 16 || pieces.Count(char.IsLower) > 16 ||
            pieces.Count(piece => piece == 'P') > 8 || pieces.Count(piece => piece == 'p') > 8 ||
            pieces.Take(8).Concat(pieces.Skip(56)).Any(piece => piece is 'P' or 'p'))
            throw new ArgumentException("Las piezas detectadas no forman una posición válida.");

        int waitingKing = Array.IndexOf(pieces, whiteToMove ? 'k' : 'K');
        if (IsAttacked(pieces, waitingKing, whiteToMove))
            throw new ArgumentException("El rey del otro color está en jaque. Comprueba el tablero y el turno seleccionado.");

        var fen = new StringBuilder();
        for (int row = 0; row < 8; row++)
        {
            if (row > 0)
                fen.Append('/');
            int empty = 0;
            foreach (char piece in pieces.AsSpan(row * 8, 8))
            {
                if (piece == '.')
                {
                    empty++;
                    continue;
                }
                if (empty > 0)
                    fen.Append(empty);
                fen.Append(piece);
                empty = 0;
            }
            if (empty > 0)
                fen.Append(empty);
        }

        fen.Append(whiteToMove ? " w - - 0 1" : " b - - 0 1");
        return new Position(pieces, whiteToMove, fen.ToString());
    }

    private static bool IsAttacked(char[] pieces, int target, bool byWhite)
    {
        int targetRow = target / 8;
        int targetColumn = target % 8;
        for (int from = 0; from < pieces.Length; from++)
        {
            char piece = pieces[from];
            if (piece == '.' || char.IsUpper(piece) != byWhite)
                continue;

            int row = targetRow - from / 8;
            int column = targetColumn - from % 8;
            int height = Math.Abs(row);
            int width = Math.Abs(column);
            bool diagonal = height == width;
            bool straight = row == 0 || column == 0;
            bool attacks = char.ToLowerInvariant(piece) switch
            {
                'p' => width == 1 && row == (byWhite ? -1 : 1),
                'n' => height * width == 2,
                'k' => Math.Max(height, width) == 1,
                'b' => diagonal && ClearPath(from, row, column),
                'r' => straight && ClearPath(from, row, column),
                'q' => (straight || diagonal) && ClearPath(from, row, column),
                _ => false
            };
            if (attacks)
                return true;
        }
        return false;

        bool ClearPath(int from, int row, int column)
        {
            int step = Math.Sign(row) * 8 + Math.Sign(column);
            for (int square = from + step; square != target; square += step)
            {
                if (pieces[square] != '.')
                    return false;
            }
            return true;
        }
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
