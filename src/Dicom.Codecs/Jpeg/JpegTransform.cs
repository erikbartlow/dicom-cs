namespace Dicom.Codecs.Jpeg;

/// <summary>
/// The discrete cosine transform of an eight by eight block, in both directions, and the
/// colour conversion a baseline stream uses.
/// </summary>
internal static class JpegTransform
{
    private static readonly double[] Basis = BuildBasis();

    /// <summary>The frequency coefficients of a block of samples, in natural order.</summary>
    public static void Forward(double[] block, double[] scratch)
    {
        for (int row = 0; row < 8; row++)
        {
            for (int u = 0; u < 8; u++)
            {
                double sum = 0;

                for (int x = 0; x < 8; x++)
                    sum += block[(row * 8) + x] * Basis[(u * 8) + x];

                scratch[(row * 8) + u] = sum;
            }
        }

        for (int column = 0; column < 8; column++)
        {
            for (int v = 0; v < 8; v++)
            {
                double sum = 0;

                for (int y = 0; y < 8; y++)
                    sum += scratch[(y * 8) + column] * Basis[(v * 8) + y];

                block[(v * 8) + column] = sum;
            }
        }
    }

    /// <summary>The samples of a block of frequency coefficients, in natural order.</summary>
    public static void Inverse(double[] block, double[] scratch)
    {
        for (int row = 0; row < 8; row++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0;

                for (int u = 0; u < 8; u++)
                    sum += block[(row * 8) + u] * Basis[(u * 8) + x];

                scratch[(row * 8) + x] = sum;
            }
        }

        for (int column = 0; column < 8; column++)
        {
            for (int y = 0; y < 8; y++)
            {
                double sum = 0;

                for (int v = 0; v < 8; v++)
                    sum += scratch[(v * 8) + column] * Basis[(v * 8) + y];

                block[(y * 8) + column] = sum;
            }
        }
    }

    /// <summary>The luminance of a colour, as the full range conversion defines it.</summary>
    public static double Luminance(int red, int green, int blue) =>
        (0.299 * red) + (0.587 * green) + (0.114 * blue);

    /// <summary>The blue difference of a colour, centred on 128.</summary>
    public static double BlueDifference(int red, int green, int blue) =>
        128.0 - (0.168736 * red) - (0.331264 * green) + (0.5 * blue);

    /// <summary>The red difference of a colour, centred on 128.</summary>
    public static double RedDifference(int red, int green, int blue) =>
        128.0 + (0.5 * red) - (0.418688 * green) - (0.081312 * blue);

    private static double[] BuildBasis()
    {
        double[] basis = new double[64];

        for (int u = 0; u < 8; u++)
        {
            double scale = (u == 0 ? Math.Sqrt(0.125) : 0.5) ;

            for (int x = 0; x < 8; x++)
                basis[(u * 8) + x] = scale * Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
        }

        return basis;
    }
}
