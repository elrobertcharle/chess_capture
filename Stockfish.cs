using System.Diagnostics;

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

    public async Task<string?> BestMove(Position position, int milliseconds)
    {
        Send(position.Uci);
        Send($"go movetime {milliseconds}");
        var response = await ReadUntil("bestmove ");
        string move = response.Split(' ')[1];
        return Position.IsMove(move) ? move : null;
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
