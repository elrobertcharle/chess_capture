# ChessCapture

ChessCapture reads a digital chessboard and uses Stockfish to play your side.

## Setup

- Use Windows with the .NET 10 SDK. Run the commands below from the project folder.
- Place the Stockfish executable at `stockfish/stockfish-windows-x86-64-universal.exe`. Git ignores the `stockfish/` folder.
- Keep the board visible at the calibrated screen position `(753, 187)` and size `1008 × 1008` pixels. Keep the console outside that area.

## Reaction delay

Edit `reaction.json` in the project folder before running `dotnet run`, or beside the executable for a published copy. The app loads the file at startup and chooses a random delay after Stockfish returns a move and before playing it. `MinDelayMs` and `MaxDelayMs` are inclusive and default to 200 and 2000 milliseconds. Set both to the same value for a fixed delay.

## Play as White

Start with the initial position visible and White at the bottom. Run:

```powershell
dotnet run
```

The app starts scanning immediately and plays White's opening move automatically.

## Play as Black

```powershell
dotnet run -- --negras
```

Move the console away from the board, then press **Enter**. The app detects the board orientation and reads White's first move from the screen. If White has already moved once, the app can start from that position. If White has yet to move, it waits.

After startup, the app checks for the opponent's moves automatically and plays your replies. To stop it, focus the console and press **Ctrl+C**.
