using MatchAnalystPro.Server.Models;
using MatchAnalystPro.Server.Taxonomy;

namespace MatchAnalystPro.Server.Services;

public record QcIssue(string Code, long? EventId, string Description, string Severity);

public class QualityControlService
{
    public List<QcIssue> ValidateMatch(List<Event> events)
    {
        var issues = new List<QcIssue>();
        if (events.Count == 0) return issues;

        // ① فحص الترتيب الزمني مقارنة بترتيب الإدخال داخل كل شوط
        var byInsertion = events.OrderBy(e => e.Period).ThenBy(e => e.Id).ToList();
        for (int i = 1; i < byInsertion.Count; i++)
        {
            if (byInsertion[i].Period == byInsertion[i - 1].Period &&
                byInsertion[i].FrameNumber < byInsertion[i - 1].FrameNumber)
            {
                issues.Add(new("TIME_ORDER", byInsertion[i].Id,
                    $"حدث مسجل بترتيب زمني عكسي (الإطار {byInsertion[i].FrameNumber} بعد الإطار {byInsertion[i - 1].FrameNumber})",
                    "high"));
            }
        }

        var sorted = events.OrderBy(e => e.Period).ThenBy(e => e.FrameNumber).ToList();

        // ② التسديدات لها إحداثيات وصورة مجمّدة
        foreach (var e in sorted.Where(x => x.Category == "shot"))
        {
            if (e.X == null || e.Y == null)
                issues.Add(new("SHOT_NO_COORD", e.Id, "تسديدة بدون إحداثيات (X, Y)", "high"));
            if (string.IsNullOrEmpty(e.FreezeFramePath))
                issues.Add(new("SHOT_NO_FREEZE", e.Id, "تسديدة بدون صورة مجمّدة (Freeze Frame)", "medium"));
        }

        // ③ الأحداث التي تتطلب نقطة نهاية وفقاً لقاموس التصنيف EventTaxonomy
        foreach (var e in sorted)
        {
            var sub = EventTaxonomy.GetSubType(e.Category, e.SubType);
            if (sub?.RequiresEndPoint == true && (e.EndX == null || e.EndY == null))
            {
                issues.Add(new("PASS_NO_END", e.Id, $"حدث ({sub.Ar}) يتطلب نقطة نهاية (EndX, EndY)", "high"));
            }
        }

        // ④ المدة بين الأحداث
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Period != sorted[i - 1].Period) continue;
            var dt = sorted[i].TimestampMs - sorted[i - 1].TimestampMs;
            if (dt < 100 && dt > 0)
                issues.Add(new("TOO_FAST", sorted[i].Id, $"حدثان بفاصل زمني قصير جداً ({dt}ms)", "medium"));
        }

        // ⑤ لا حدثين بنفس الإطار لنفس اللاعب
        var duplicates = sorted.GroupBy(e => new { e.Period, e.FrameNumber }).Where(g => g.Count() > 1);
        foreach (var dup in duplicates)
        {
            var firstId = dup.First().Id;
            issues.Add(new("DUPLICATE_FRAME", firstId, $"إطار مكرر #{dup.Key.FrameNumber} ({dup.Count()} أحداث في نفس الإطار)", "low"));
        }

        // ⑥ كل تسديدة يجب أن تتبعها أحداث منطقية (في نافذة 30 ثانية)
        foreach (var shot in sorted.Where(x => x.Category == "shot"))
        {
            var after = sorted.FirstOrDefault(e => e.Period == shot.Period &&
                                                   e.FrameNumber > shot.FrameNumber &&
                                                   e.FrameNumber < shot.FrameNumber + 750);
            if (after == null && shot.Outcome != "failed")
                issues.Add(new("SHOT_NO_FOLLOWUP", shot.Id, "تسديدة بدون أحداث متابعة لاحقة خلال 30 ثانية", "low"));
        }

        return issues;
    }
}
