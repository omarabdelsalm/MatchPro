using System.ComponentModel.DataAnnotations;

namespace MatchAnalystPro.Server.Models;

public class Event
{
    public long Id { get; set; }

    // العلاقات
    public int MatchId { get; set; }
    public Match? Match { get; set; }
    public int? PossessionId { get; set; }
    public Possession? Possession { get; set; }
    public int? SequenceId { get; set; }
    public int TeamId { get; set; }
    public Team? Team { get; set; }
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }
    public int? RelatedPlayerId { get; set; }

    // التوقيت الدقيق
    public long FrameNumber { get; set; }
    public long TimestampMs { get; set; }
    public int Period { get; set; } = 1;  // 1,2,3,4,5
    public int Minute { get; set; }
    public int Second { get; set; }

    // التصنيف
    [MaxLength(30)] public string Category { get; set; } = "";
    [MaxLength(40)] public string SubType { get; set; } = "";
    [MaxLength(20)] public string Outcome { get; set; } = "successful";
    public string QualifiersJson { get; set; } = "[]";

    // الإحداثيات (0-100)
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? EndX { get; set; }
    public double? EndY { get; set; }

    // Freeze Frame
    public string? FreezeFramePath { get; set; }

    // الملاحظات
    public string? Note { get; set; }
    public string? Tags { get; set; }

    // التدقيق
    public string TaggedBy { get; set; } = "analyst";
    public DateTime TaggedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastEditedAt { get; set; }

    // الجودة
    public int QualityScore { get; set; } = 100;
    public bool FlaggedForReview { get; set; }
    public string? ReviewNotes { get; set; }

    // مقطع الفيديو
    public long ClipStartFrame { get; set; }
    public long ClipEndFrame { get; set; }
}
