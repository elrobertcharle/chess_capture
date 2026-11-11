using System.ComponentModel;
using System.Windows;

namespace ChessCapture.Wpf;

public partial class MainWindow : Window
{
    private readonly string reactionPath = Path.Combine(AppContext.BaseDirectory, "reaction.json");
    private ReactionSettings reaction = ReactionSettings.Defaults;
    private GameSession? game;
    private Stockfish? engine;
    private CancellationTokenSource? runCancellation;
    private Task? run;
    private bool busy;
    private bool closeRequested;
    private bool readyToClose;

    public MainWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Left + 16;
        Top = SystemParameters.WorkArea.Top + 16;
        try
        {
            if (File.Exists(reactionPath))
                reaction = ReactionSettings.Load(reactionPath);
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Report($"Se cargó el rango predeterminado. {error.Message}");
        }
        ShowReactionSettings();
    }

    private void SaveReaction(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MinReactionInput.Text, out int minimum) ||
            !int.TryParse(MaxReactionInput.Text, out int maximum))
        {
            Report("Introduce dos tiempos enteros en milisegundos.");
            return;
        }

        var settings = new ReactionSettings { MinDelayMs = minimum, MaxDelayMs = maximum };
        try
        {
            settings.Save(reactionPath);
            reaction = settings;
            game?.SetReactionSettings(settings);
            ShowReactionSettings();
            Report($"Tiempo de reacción guardado: {minimum}–{maximum} ms.");
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Report(error.Message);
        }
    }

    private void ShowReactionSettings()
    {
        MinReactionInput.Text = reaction.MinDelayMs.ToString();
        MaxReactionInput.Text = reaction.MaxDelayMs.ToString();
    }

    private async void StartGame(object sender, RoutedEventArgs e) => await Start(false);

    private async void StartFromBoard(object sender, RoutedEventArgs e) => await Start(true);

    private async Task Start(bool fromBoard)
    {
        bool whitePlayer = WhiteOption.IsChecked == true;
        string[] history = fromBoard ? [] :
            HistoryInput.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        SetBusy(true);
        Status.Text = fromBoard ? "Leyendo el tablero…" : "Iniciando Stockfish…";
        try
        {
            await ReleaseGame();
            Activity.Items.Clear();
            EvaluationValue.Text = "—";
            var setup = await Task.Run(() => GameAssets.Open(history, fromBoard ? whitePlayer : null));
            engine = setup.Engine;
            game = new GameSession(setup.Vision, engine, setup.Position, reaction, whitePlayer);
            if (setup.Position.StartingFen is { } fen)
                Report($"FEN: {fen}");
            StartMonitoring(whitePlayer ? "Jugando con blancas." : "Jugando con negras.");
        }
        catch (Exception error)
        {
            ReportError(error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void TogglePause(object sender, RoutedEventArgs e)
    {
        if (run is null)
        {
            StartMonitoring("Partida reanudada.");
            UpdateControls();
            return;
        }

        SetBusy(true);
        await StopMonitoring();
        if (game is not null)
            Report("Partida pausada.");
        SetBusy(false);
    }

    private void StartMonitoring(string message)
    {
        if (game is null || closeRequested)
            return;

        Report(message);
        runCancellation = new CancellationTokenSource();
        run = Monitor(game, runCancellation);
    }

    private async Task Monitor(GameSession session, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Run(() => session.Run(
                message => Dispatcher.BeginInvoke(() => Report(message)),
                cancellation.Token,
                evaluation => Dispatcher.BeginInvoke(() => EvaluationValue.Text = evaluation ?? "—")));
            if (!cancellation.IsCancellationRequested)
                game = null;
        }
        catch (Exception error)
        {
            game = null;
            ReportError(error);
        }
        finally
        {
            run = null;
            runCancellation = null;
            cancellation.Dispose();
            UpdateControls();
        }
    }

    private async Task StopMonitoring()
    {
        if (run is { } activeRun)
        {
            runCancellation?.Cancel();
            await activeRun;
        }
    }

    private async Task ReleaseGame()
    {
        await StopMonitoring();
        game = null;
        if (engine is { } activeEngine)
        {
            engine = null;
            await Task.Run(activeEngine.Dispose);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (readyToClose)
            return;

        e.Cancel = true;
        closeRequested = true;
        if (busy)
            return;

        SetBusy(true);
        try
        {
            await ReleaseGame();
        }
        catch (Exception error)
        {
            ReportError(error);
        }
        finally
        {
            readyToClose = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        UpdateControls();
        if (!busy && closeRequested)
            Close();
    }

    private void UpdateControls()
    {
        GameOptions.IsEnabled = !busy;
        StartButton.IsEnabled = !busy;
        CaptureButton.IsEnabled = !busy;
        SaveReactionButton.IsEnabled = !busy;
        StartButton.Content = engine is null ? "_Iniciar" : "_Reiniciar";
        PauseButton.IsEnabled = !busy && game is not null;
        PauseButton.Content = run is null && game is not null ? "_Reanudar" : "_Pausar";
    }

    private void Report(string message)
    {
        Status.Text = message;
        if (Activity.Items.Count >= 150)
            Activity.Items.RemoveAt(0);
        Activity.Items.Add(message);
        Activity.ScrollIntoView(message);
    }

    private void ReportError(Exception error) =>
        Report(error is OperationCanceledException ? "Stockfish no respondió a tiempo." : error.Message);
}
