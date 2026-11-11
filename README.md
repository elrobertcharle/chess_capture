# ChessCapture

ChessCapture reads a digital chessboard and uses Stockfish to play your side.

## WPF controls

Run `dotnet run --project ChessCapture.Wpf` on Windows. Move the window clear of the calibrated board, choose **Blancas** or **Negras**, and select **Iniciar**. Use **Pausar** and **Reanudar** to keep the current game. If a move's clicks have already started, they finish before the pause takes effect.

**Reiniciar** starts with the selected options. Reset the visible board to the starting position, or enter its full UCI move history (for example, `e2e4 e7e5`) in **Historial UCI**. Keep your chosen color at the bottom of the board.

Use **Empezar desde aquí** when it is your selected color's turn to play on the visible board. The app starts from the captured position and logs its FEN. The capture starts with no castling rights or en passant target and with fresh move counters. Use **Historial UCI** if you need to preserve those details.

## Setup

- Use Windows with the .NET 10 SDK. Run the commands below from the project folder.
- Place the Stockfish executable at `stockfish/stockfish-windows-x86-64-universal.exe`. Git ignores the `stockfish/` folder.
- Keep the board visible at the calibrated screen position `(753, 187)` and size `1008 × 1008` pixels. Keep the ChessCapture window outside that area.

## Reaction delay

In **Tiempo de reacción (ms)**, enter a minimum and maximum and select **Guardar**. The inclusive range defaults to 600–2900 ms. Set both values to the same number for a fixed delay. The app uses the saved range the next time it chooses a delay and stores it in `reaction.json` beside the executable.
