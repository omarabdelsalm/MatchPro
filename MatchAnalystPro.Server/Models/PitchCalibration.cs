namespace MatchAnalystPro.Server.Models;

public class PitchCalibration
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match? Match { get; set; }
    public int CameraId { get; set; } = 0;
    public string CameraName { get; set; } = "Main Broadcast";

    // 4 نقاط في الفيديو (بكسل)
    public double TlVideoX { get; set; }
    public double TlVideoY { get; set; }
    public double TrVideoX { get; set; }
    public double TrVideoY { get; set; }
    public double BrVideoX { get; set; }
    public double BrVideoY { get; set; }
    public double BlVideoX { get; set; }
    public double BlVideoY { get; set; }

    // أبعاد الملعب
    public double PitchLengthM { get; set; } = 105.0;
    public double PitchWidthM { get; set; } = 68.0;

    // الإطار المرجعي (المعايرة صالحة من هذا الإطار)
    public long ValidFromFrame { get; set; } = 0;
    public long? ValidToFrame { get; set; }
}
