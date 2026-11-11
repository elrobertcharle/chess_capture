using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

sealed class BoardVision
{
    public static readonly Rectangle BoardArea = new(753, 187, 1008, 1008);
    private const int SquareSize = 126;
    private const int SampleStep = 4;
    private const int Margin = 8;
    private readonly Dictionary<char, List<byte[]>> templates = new();

    public BoardVision(string referencePath)
    {
        using var reference = new Bitmap(referencePath);
        using var board = reference.Clone(BoardArea, PixelFormat.Format24bppRgb);
        var pieces = Position.Initial();
        Position.Apply(pieces, "e2e4");
        Position.Apply(pieces, "c7c5");
        var samples = ReadSamples(board);
        for (int i = 0; i < pieces.Length; i++)
        {
            if (!templates.TryGetValue(pieces[i], out var variants))
                templates.Add(pieces[i], variants = new());
            variants.Add(samples[i]);
        }
    }

    public static Bitmap Capture()
    {
        var board = new Bitmap(BoardArea.Width, BoardArea.Height, PixelFormat.Format24bppRgb);
        try
        {
            using var graphics = Graphics.FromImage(board);
            graphics.CopyFromScreen(BoardArea.Location, Point.Empty, BoardArea.Size);
            return board;
        }
        catch
        {
            board.Dispose();
            throw;
        }
    }

    public char[] Recognize(Bitmap board, bool blackAtBottom)
    {
        var samples = ReadSamples(board);
        var pieces = new char[64];
        for (int i = 0; i < samples.Length; i++)
        {
            var sample = samples[i];
            int best = int.MaxValue;
            int second = int.MaxValue;
            char piece = '?';
            foreach (var (label, variants) in templates)
            {
                int distance = variants.Min(variant => Distance(sample, variant));
                if (distance < best)
                {
                    second = best;
                    best = distance;
                    piece = label;
                }
                else
                {
                    second = Math.Min(second, distance);
                }
            }

            pieces[blackAtBottom ? 63 - i : i] =
                best > sample.Length / 12 || second - best < 10 ? '?' : piece;
        }
        return pieces;
    }

    private static int Distance(byte[] sample, byte[] template)
    {
        int difference = 0;
        for (int i = 0; i < sample.Length; i++)
            if (sample[i] != template[i])
                difference++;
        return difference;
    }

    private static byte[][] ReadSamples(Bitmap board)
    {
        var data = board.LockBits(new Rectangle(Point.Empty, board.Size),
            ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = Math.Abs(data.Stride);
            var pixels = new byte[stride * board.Height];
            for (int row = 0; row < board.Height; row++)
                Marshal.Copy(data.Scan0 + row * data.Stride, pixels, row * stride, stride);

            var samples = new byte[64][];
            for (int square = 0; square < 64; square++)
            {
                var sample = new List<byte>();
                int left = square % 8 * SquareSize;
                int top = square / 8 * SquareSize;
                for (int y = Margin; y < SquareSize - Margin; y += SampleStep)
                for (int x = Margin; x < SquareSize - Margin; x += SampleStep)
                {
                    int offset = (top + y) * stride + (left + x) * 3;
                    // Los fondos del tablero son de color. Las piezas son casi acromáticas.
                    int blue = pixels[offset];
                    int green = pixels[offset + 1];
                    int red = pixels[offset + 2];
                    int max = Math.Max(red, Math.Max(green, blue));
                    int min = Math.Min(red, Math.Min(green, blue));
                    sample.Add(max < 80 ? (byte)1 : min > 225 && max - min < 20 ? (byte)2 : (byte)0);
                }
                samples[square] = sample.ToArray();
            }
            return samples;
        }
        finally
        {
            board.UnlockBits(data);
        }
    }
}
