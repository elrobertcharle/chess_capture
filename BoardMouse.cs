using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

static class BoardMouse
{
    public static async Task<bool> Play(string move, bool blackAtBottom)
    {
        Point from = BoardVision.SquareCenter(move[0], move[1], blackAtBottom);
        Point to = BoardVision.SquareCenter(move[2], move[3], blackAtBottom);
        Click(from);
        await Task.Delay(200);
        Click(to);
        if (move.Length == 4)
            return true;

        // El selector empieza en la casilla de destino y se extiende hacia el centro.
        int pieceIndex = "qnrb".IndexOf(move[4]);
        int rankStep = move[3] == '8' ? -1 : 1;
        var choices = new Point[4];
        for (int i = 0; i < choices.Length; i++)
            choices[i] = BoardVision.SquareCenter(move[2], (char)(move[3] + rankStep * i), blackAtBottom);

        // Aparta el cursor del selector para comprobar los círculos sin resaltado.
        char adjacentFile = move[2] == 'h' ? 'g' : (char)(move[2] + 1);
        MoveCursor(BoardVision.SquareCenter(adjacentFile, move[3], blackAtBottom));

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await Task.Delay(200);
            using var board = BoardVision.Capture();
            if (PromotionMenuVisible(board, choices))
            {
                Click(choices[pieceIndex]);
                return true;
            }
        }
        return false;
    }

    private static bool PromotionMenuVisible(Bitmap board, Point[] choices)
    {
        // A ambos lados de cada icono se ve el fondo gris de su círculo.
        int squareSize = BoardVision.BoardArea.Width / 8;
        int horizontalOffset = squareSize * 2 / 5;
        int verticalOffset = squareSize / 8;
        foreach (Point center in choices)
        {
            int x = center.X - BoardVision.BoardArea.X;
            int y = center.Y - BoardVision.BoardArea.Y;
            if (!HasGrayBackground(board, x - horizontalOffset, y, verticalOffset) ||
                !HasGrayBackground(board, x + horizontalOffset, y, verticalOffset))
                return false;
        }
        return true;
    }

    private static bool HasGrayBackground(Bitmap board, int x, int y, int offset)
    {
        int matches = 0;
        for (int row = y - offset; row <= y + offset; row += offset)
        {
            if (IsMenuGray(board.GetPixel(x, row)))
                matches++;
        }
        return matches >= 2;
    }

    private static bool IsMenuGray(Color color) =>
        color.R is >= 140 and <= 200 &&
        Math.Abs(color.R - color.G) <= 4 &&
        Math.Abs(color.R - color.B) <= 4;

    private static void Click(Point point)
    {
        MoveCursor(point);

        const uint leftDown = 0x0002;
        const uint leftUp = 0x0004;
        Input[] click =
        [
            new() { Mouse = new MouseInput { Flags = leftDown } },
            new() { Mouse = new MouseInput { Flags = leftUp } }
        ];
        if (SendInput((uint)click.Length, click, Marshal.SizeOf<Input>()) != click.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no completó el clic.");
    }

    private static void MoveCursor(Point point)
    {
        if (!SetCursorPos(point.X, point.Y))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    // INPUT con la variante MOUSEINPUT. El campo Type es cero para eventos del ratón.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] Input[] inputs, int size);
}
