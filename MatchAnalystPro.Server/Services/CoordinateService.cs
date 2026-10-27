using MatchAnalystPro.Server.Models;
using MathNet.Numerics.LinearAlgebra;

namespace MatchAnalystPro.Server.Services;

public class CoordinateService
{
    // Homography matrix 3x3
    public Matrix<double> BuildHomography(PitchCalibration c)
    {
        var src = new (double, double)[]
        {
            (c.TlVideoX, c.TlVideoY),
            (c.TrVideoX, c.TrVideoY),
            (c.BrVideoX, c.BrVideoY),
            (c.BlVideoX, c.BlVideoY)
        };
        var dst = new (double, double)[]
        {
            (0, 0),
            (c.PitchLengthM, 0),
            (c.PitchLengthM, c.PitchWidthM),
            (0, c.PitchWidthM)
        };
        return ComputeHomography(src, dst);
    }

    public (double X, double Y) VideoToPitch(
        double videoX, double videoY, Matrix<double> H)
    {
        var w = H[2, 0] * videoX + H[2, 1] * videoY + H[2, 2];
        if (Math.Abs(w) < 1e-10) return (0, 0);
        var x = (H[0, 0] * videoX + H[0, 1] * videoY + H[0, 2]) / w;
        var y = (H[1, 0] * videoX + H[1, 1] * videoY + H[1, 2]) / w;
        return (x, y);
    }

    /// <summary>تحويل إحداثي المتر إلى نسبة مئوية 0-100 (متوافق مع StatsBomb)</summary>
    public (double X, double Y) Normalize(double xMeters, double yMeters, double L, double W)
        => (Math.Clamp(xMeters / L * 100.0, 0, 100),
            Math.Clamp(yMeters / W * 100.0, 0, 100));

    public (double X, double Y) Denormalize(double xPct, double yPct, double L, double W)
        => (xPct / 100.0 * L, yPct / 100.0 * W);

    private Matrix<double> ComputeHomography(
        (double x, double y)[] src, (double x, double y)[] dst)
    {
        var A = Matrix<double>.Build.Dense(8, 9);
        for (int i = 0; i < 4; i++)
        {
            var (sx, sy) = src[i];
            var (dx, dy) = dst[i];
            A[i * 2, 0] = sx; A[i * 2, 1] = sy; A[i * 2, 2] = 1;
            A[i * 2, 3] = 0;  A[i * 2, 4] = 0;  A[i * 2, 5] = 0;
            A[i * 2, 6] = -dx * sx; A[i * 2, 7] = -dx * sy; A[i * 2, 8] = dx;

            A[i * 2 + 1, 0] = 0; A[i * 2 + 1, 1] = 0; A[i * 2 + 1, 2] = 0;
            A[i * 2 + 1, 3] = sx; A[i * 2 + 1, 4] = sy; A[i * 2 + 1, 5] = 1;
            A[i * 2 + 1, 6] = -dy * sx; A[i * 2 + 1, 7] = -dy * sy; A[i * 2 + 1, 8] = dy;
        }

        // SVD: A·h = 0 → آخر صف في V
        var svd = A.Svd(true);
        var h = svd.VT.Row(8);

        return Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { h[0], h[1], h[2] },
            { h[3], h[4], h[5] },
            { h[6], h[7], h[8] }
        });
    }
}
