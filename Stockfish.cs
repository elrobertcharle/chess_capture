using System.Diagnostics;
using System.Globalization;

sealed record SearchResult(string Move, string? Evaluation)
{
    public string Display => Evaluation is null ? Move : $"{Move} · Evaluación {Evaluation}";
}

sealed class Stockfish : IDisposable
{
    private readonly Process process;

    public Stockfish(string path)
    {
        process = Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("No se pudo iniciar Stockfish.");
    }

    public async Task Initialize()
    {
        Send("uci");
        await ReadUntil("uciok");
        Send("isready");
        await ReadUntil("readyok");
    }

    public async Task<List<string>> LegalMoves(Position position)
    {
        Send(position.Uci);
        Send("go perft 1");
        var moves = new List<string>();
        await ReadUntil("Nodes searched:", line =>
        {
            int separator = line.IndexOf(':');
            if (separator > 0 && Position.IsMove(line[..separator]))
                moves.Add(line[..separator]);
        });
        return moves;
    }

    public async Task<SearchResult?> BestMove(Position position, int milliseconds)
    {
        Send(position.Uci);
        Send($"go movetime {milliseconds}");
        var evaluations = new Dictionary<string, string>();
        var response = await ReadUntil("bestmove ", line =>
        {
            if (!line.StartsWith("info depth ", StringComparison.Ordinal))
                return;

            string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int score = Array.IndexOf(fields, "score");
            int pv = Array.IndexOf(fields, "pv");
            if (score < 0 || pv < score + 3 || pv + 1 >= fields.Length ||
                !int.TryParse(fields[score + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return;

            long whiteScore = position.WhiteToMove ? value : -(long)value;
            string? evaluation = fields[score + 1] switch
            {
                "cp" => (whiteScore / 100m).ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture),
                "mate" => $"mate para {(whiteScore > 0 ? "blancas" : "negras")} en {Math.Abs(whiteScore)}",
                _ => null
            };
            if (evaluation is not null)
            {
                string bound = fields[score + 3] switch
                {
                    "lowerbound" => position.WhiteToMove ? "≥ " : "≤ ",
                    "upperbound" => position.WhiteToMove ? "≤ " : "≥ ",
                    _ => ""
                };
                evaluations[fields[pv + 1]] = bound + evaluation;
            }
        });
        string move = response.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
        return Position.IsMove(move) ? new SearchResult(move, evaluations.GetValueOrDefault(move)) : null;
    }

    private void Send(string command) => process.StandardInput.WriteLine(command);

    private async Task<string> ReadUntil(string prefix, Action<string>? observe = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line;
            observe?.Invoke(line);
        }
        throw new InvalidOperationException("Stockfish cerró la conexión.");
    }

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                Send("quit");
                if (!process.WaitForExit(2000))
                    process.Kill();
            }
        }
        finally
        {
            process.Dispose();
        }
    }
}
