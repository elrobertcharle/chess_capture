using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

static class BoardMouse
{
    public static async Task Play(string move, bool blackAtBottom)
    {
        Point from = BoardVision.SquareCenter(move[0], move[1], blackAtBottom);
        Point to = BoardVision.SquareCenter(move[2], move[3], blackAtBottom);
        Click(from);
        await Task.Delay(200);
        Click(to);
    }

    private static void Click(Point point)
    {
        if (!SetCursorPos(point.X, point.Y))
            throw new Win32Exception(Marshal.GetLastWin32Error());

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
