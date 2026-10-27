using MatchAnalystPro.Server.Data;
using MatchAnalystPro.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchAnalystPro.Server.Services;

public record TacticalKpi(string NameAr, string NameEn, string TeamValue, string OpponentValue, string Benchmark, string Status, string Description);

public record SwotItem(string Title, string Description, string Category, string Severity, List<string> EvidenceEvents);

public record PlayerProfile(
    int PlayerId, 
    string NameAr, 
    string Position, 
    int ShirtNumber, 
    string RoleAr, 
    string KeyStrength, 
    string Vulnerability,
    int TotalActions, 
    double SuccessRate,
    int KeyPasses,
    int Recoveries,
    double GeneratedXt = 0.0);

public record CoachingDirective(string Phase, string Directive, string Rationale, string Priority);

public record PassNetworkNode(int PlayerId, string Name, int ShirtNumber, string Position, double AvgX, double AvgY, int PassCount);

public record PassNetworkLink(int FromPlayerId, int ToPlayerId, string FromName, string ToName, double FromX, double FromY, double ToX, double ToY, int Count);

public record ShotMapItem(long EventId, string Label, double X, double Y, double? EndX, double? EndY, double Xg, bool IsGoal, bool IsOnTarget);

public class TacticalReportModel
{
    public Match Match { get; set; } = null!;
    public Team FocusTeam { get; set; } = null!;
    public Team OpponentTeam { get; set; } = null!;

    public double FocusXg { get; set; }
    public double OpponentXg { get; set; }
    public double FocusXt { get; set; }
    public double OpponentXt { get; set; }
    public double FocusPpda { get; set; }
    public double OpponentPpda { get; set; }
    public double FocusFieldTilt { get; set; }
    public double OpponentFieldTilt { get; set; }

    public List<TacticalKpi> Kpis { get; set; } = new();
    public List<SwotItem> Strengths { get; set; } = new();
    public List<SwotItem> Weaknesses { get; set; } = new();
    public List<SwotItem> Opportunities { get; set; } = new();
    public List<SwotItem> Threats { get; set; } = new();
    public List<PlayerProfile> KeyPlayers { get; set; } = new();
    public List<CoachingDirective> CoachingPlan { get; set; } = new();

    // بيانات الخرائط التكتيكية المتقدمة (Pass Network, Heatmap, Shot Map)
    public List<PassNetworkNode> PassNodes { get; set; } = new();
    public List<PassNetworkLink> PassLinks { get; set; } = new();
    public List<ShotMapItem> ShotMap { get; set; } = new();
    public List<(double X, double Y)> HeatmapPoints { get; set; } = new();
}

public class TacticalIntelligenceService
{
    private readonly AppDbContext _db;

    // مصفوفة التهديد المتوقع القياسية (12x8 Expected Threat Grid - Karun Singh Standard)
    private static readonly double[,] XtGrid = new double[8, 12]
    {
        { 0.006, 0.007, 0.008, 0.009, 0.011, 0.012, 0.014, 0.017, 0.021, 0.027, 0.034, 0.039 },
        { 0.007, 0.008, 0.009, 0.010, 0.012, 0.014, 0.017, 0.020, 0.026, 0.035, 0.048, 0.062 },
        { 0.008, 0.009, 0.010, 0.011, 0.013, 0.016, 0.019, 0.024, 0.033, 0.049, 0.088, 0.145 },
        { 0.008, 0.009, 0.010, 0.012, 0.014, 0.017, 0.021, 0.027, 0.039, 0.064, 0.142, 0.285 },
        { 0.008, 0.009, 0.010, 0.012, 0.014, 0.017, 0.021, 0.027, 0.039, 0.064, 0.142, 0.285 },
        { 0.008, 0.009, 0.010, 0.011, 0.013, 0.016, 0.019, 0.024, 0.033, 0.049, 0.088, 0.145 },
        { 0.007, 0.008, 0.009, 0.010, 0.012, 0.014, 0.017, 0.020, 0.026, 0.035, 0.048, 0.062 },
        { 0.006, 0.007, 0.008, 0.009, 0.011, 0.012, 0.014, 0.017, 0.021, 0.027, 0.034, 0.039 }
    };

    public TacticalIntelligenceService(AppDbContext db)
    {
        _db = db;
    }

    private static bool IsDefensiveRecoveryOrTackle(Event e) =>
        e.Category is "interception" or "recovery" or "tackle" ||
        (e.Category == "duel" && e.SubType is "tackle" or "sliding_tackle" or "pressing_duel" or "loose_ball") ||
        (e.Category == "press" && e.Outcome == "successful");

    private static bool IsTurnoverOrLoss(Event e) =>
        e.Category is "miscontrol" or "turnover" || e.Outcome == "failed";

    public static double CalculateActionXt(Event e)
    {
        if (e.Category is not ("pass" or "carry")) return 0.0;
        if (e.Outcome == "failed") return 0.0;
        if (!e.X.HasValue || !e.Y.HasValue || !e.EndX.HasValue || !e.EndY.HasValue) return 0.0;

        int startCol = Math.Clamp((int)(e.X.Value / 100.0 * 12), 0, 11);
        int startRow = Math.Clamp((int)(e.Y.Value / 100.0 * 8), 0, 7);
        int endCol = Math.Clamp((int)(e.EndX.Value / 100.0 * 12), 0, 11);
        int endRow = Math.Clamp((int)(e.EndY.Value / 100.0 * 8), 0, 7);

        double diff = XtGrid[endRow, endCol] - XtGrid[startRow, startCol];
        return diff > 0 ? diff : 0.0;
    }

    public static double CalculateShotXg(Event s)
    {
        if (s.SubType == "penalty") return 0.76;

        // تحويل إحداثيات (0-100) إلى أمتار الملعب الحقيقية (105x68)
        double xM = (s.X ?? 85.0) / 100.0 * 105.0;
        double yM = (s.Y ?? 50.0) / 100.0 * 68.0;

        double dx = Math.Max(1.0, 105.0 - xM);
        double dy = Math.Abs(34.0 - yM);
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // حساب زاوية رؤية المرمى (عرض المرمى 7.32 متر)
        double goalWidth = 7.32;
        double angle = Math.Atan2(goalWidth * dx, dx * dx + dy * dy - (goalWidth / 2.0) * (goalWidth / 2.0));
        if (angle < 0) angle = 0.05;

        // نموذج اللوجستي للـ xG المعتمد على المسافة والزاوية
        double logit = -1.35 - 0.11 * distance + 1.85 * angle;
        if (s.SubType == "header") logit -= 0.55;
        if (s.SubType == "counter" || (s.QualifiersJson != null && s.QualifiersJson.Contains("counter"))) logit += 0.30;
        if (s.SubType == "free_kick") logit -= 0.40;

        double xg = 1.0 / (1.0 + Math.Exp(-logit));
        return Math.Round(Math.Clamp(xg, 0.02, 0.92), 2);
    }

    public async Task<TacticalReportModel?> GenerateTacticalReportAsync(int matchId, int? focusTeamId = null)
    {
        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Events)
                .ThenInclude(e => e.Player)
            .Include(m => m.Possessions)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null || match.HomeTeam == null || match.AwayTeam == null)
            return null;

        var focusTeam = (focusTeamId.HasValue && focusTeamId.Value == match.AwayTeamId)
            ? match.AwayTeam
            : match.HomeTeam;

        var opponentTeam = focusTeam.Id == match.HomeTeamId ? match.AwayTeam : match.HomeTeam;

        var report = new TacticalReportModel
        {
            Match = match,
            FocusTeam = focusTeam,
            OpponentTeam = opponentTeam
        };

        var allEvents = match.Events.OrderBy(e => e.Period).ThenBy(e => e.FrameNumber).ToList();
        var focusEvents = allEvents.Where(e => e.TeamId == focusTeam.Id).ToList();
        var oppEvents = allEvents.Where(e => e.TeamId == opponentTeam.Id).ToList();

        // 1) حساب الـ xG الهندسي والـ xT (التهديد المتوقع)
        report.FocusXg = Math.Round(focusEvents.Where(e => e.Category == "shot").Sum(CalculateShotXg), 2);
        report.OpponentXg = Math.Round(oppEvents.Where(e => e.Category == "shot").Sum(CalculateShotXg), 2);
        report.FocusXt = Math.Round(focusEvents.Sum(CalculateActionXt), 2);
        report.OpponentXt = Math.Round(oppEvents.Sum(CalculateActionXt), 2);

        // 2) حساب الـ Field Tilt (% التمريرات والتحركات في الثلث الأخير)
        var focusFinalThirdPasses = focusEvents.Count(e => (e.Category == "pass" || e.Category == "carry") && e.X >= 66.0);
        var oppFinalThirdPasses = oppEvents.Count(e => (e.Category == "pass" || e.Category == "carry") && e.X >= 66.0);
        int totalFinalThird = focusFinalThirdPasses + oppFinalThirdPasses;
        if (totalFinalThird > 0)
        {
            report.FocusFieldTilt = Math.Round((double)focusFinalThirdPasses * 100.0 / totalFinalThird, 1);
            report.OpponentFieldTilt = Math.Round(100.0 - report.FocusFieldTilt, 1);
        }

        // 3) حساب الـ PPDA
        report.FocusPpda = CalculatePpda(oppEvents, focusEvents);
        report.OpponentPpda = CalculatePpda(focusEvents, oppEvents);

        // 4) توليد الـ KPIs بناء على البيانات الفعلية
        BuildKpiDashboard(report, focusEvents, oppEvents);

        // 5) استخراج نقاط القوة والضعف (SWOT Engine) ديناميكياً 100%
        BuildSwotAnalysis(report, focusEvents, oppEvents, match);

        // 6) تحليل ملفات اللاعبين الفردية بأرقامهم ومؤشراتهم الواقعية + xT
        BuildPlayerProfiles(report, focusEvents, focusTeam.Id);

        // 7) توصيات وتوجيهات المدرب المخصصة لمعطيات اللقاء
        BuildCoachingDirectives(report, focusEvents, oppEvents);

        // 8) بناء بيانات الخرائط التكتيكية (Pass Network + Heatmap + Shot Map)
        await BuildTacticalMapsDataAsync(report, focusEvents, focusTeam.Id);

        return report;
    }

    private async Task BuildTacticalMapsDataAsync(TacticalReportModel report, List<Event> focusEvents, int teamId)
    {
        var teamPlayers = await _db.Players.AsNoTracking().Where(p => p.TeamId == teamId).ToListAsync();
        var playerDict = teamPlayers.ToDictionary(p => p.Id);

        // 1. نقاط الخريطة الحرارية
        report.HeatmapPoints = focusEvents
            .Where(e => e.X.HasValue && e.Y.HasValue)
            .Select(e => (e.X!.Value, e.Y!.Value))
            .ToList();

        // 2. خريطة التسديدات
        foreach (var s in focusEvents.Where(e => e.Category == "shot" && e.X.HasValue && e.Y.HasValue))
        {
            double xg = CalculateShotXg(s);
            bool isGoal = s.Outcome == "goal" || (s.QualifiersJson != null && s.QualifiersJson.Contains("goal"));
            bool isOnTarget = isGoal || s.Outcome == "successful" || s.Outcome == "key";
            string pName = s.Player?.NameAr ?? "تسديدة";
            report.ShotMap.Add(new ShotMapItem(s.Id, pName, s.X!.Value, s.Y!.Value, s.EndX, s.EndY, xg, isGoal, isOnTarget));
        }

        // 3. عقد شبكة التمريرات (متوسط تمركز كل لاعب)
        var playerEventsWithCoords = focusEvents
            .Where(e => e.PlayerId.HasValue && e.X.HasValue && e.Y.HasValue)
            .GroupBy(e => e.PlayerId!.Value)
            .ToList();

        var nodeDict = new Dictionary<int, PassNetworkNode>();
        foreach (var grp in playerEventsWithCoords)
        {
            if (!playerDict.TryGetValue(grp.Key, out var pl))
                pl = grp.First().Player;
            if (pl == null) continue;

            double avgX = Math.Round(grp.Average(e => e.X!.Value), 1);
            double avgY = Math.Round(grp.Average(e => e.Y!.Value), 1);
            int passCount = grp.Count(e => e.Category == "pass");

            var node = new PassNetworkNode(pl.Id, pl.NameAr, pl.ShirtNumber, pl.Position, avgX, avgY, Math.Max(1, passCount));
            nodeDict[pl.Id] = node;
            report.PassNodes.Add(node);
        }

        // 4. خطوط التمرير بين اللاعبين (باستخدام RelatedPlayerId أو استنتاج المستقبِل من الحدث التالي لنفس الفريق)
        var pairCounts = new Dictionary<(int From, int To), int>();
        for (int i = 0; i < focusEvents.Count; i++)
        {
            var ev = focusEvents[i];
            if (ev.Category != "pass" || ev.Outcome == "failed" || !ev.PlayerId.HasValue) continue;

            int? receiverId = ev.RelatedPlayerId;
            if (!receiverId.HasValue && i + 1 < focusEvents.Count)
            {
                var nextEv = focusEvents[i + 1];
                if (nextEv.Period == ev.Period &&
                    nextEv.PlayerId.HasValue &&
                    nextEv.PlayerId.Value != ev.PlayerId.Value &&
                    (nextEv.TimestampMs - ev.TimestampMs) <= 8000)
                {
                    receiverId = nextEv.PlayerId.Value;
                }
            }

            if (receiverId.HasValue && receiverId.Value != ev.PlayerId.Value &&
                nodeDict.ContainsKey(ev.PlayerId.Value) && nodeDict.ContainsKey(receiverId.Value))
            {
                var key = (ev.PlayerId.Value, receiverId.Value);
                pairCounts[key] = pairCounts.GetValueOrDefault(key) + 1;
            }
        }

        foreach (var kv in pairCounts.OrderByDescending(x => x.Value))
        {
            var fromNode = nodeDict[kv.Key.From];
            var toNode = nodeDict[kv.Key.To];
            report.PassLinks.Add(new PassNetworkLink(
                fromNode.PlayerId,
                toNode.PlayerId,
                fromNode.Name,
                toNode.Name,
                fromNode.AvgX,
                fromNode.AvgY,
                toNode.AvgX,
                toNode.AvgY,
                kv.Value));
        }
    }

    private double CalculatePpda(List<Event> oppPassesEvents, List<Event> defendingActionsEvents)
    {
        int oppPasses = oppPassesEvents.Count(e => e.Category == "pass" && (e.X == null || e.X <= 60.0));
        int defActions = defendingActionsEvents.Count(e =>
            (IsDefensiveRecoveryOrTackle(e) || e.Category is "foul" or "press") && (e.X == null || e.X >= 40.0));

        if (defActions == 0) return oppPasses > 0 ? Math.Round((double)oppPasses, 1) : 0.0;
        return Math.Round((double)oppPasses / defActions, 1);
    }

    private void BuildKpiDashboard(TacticalReportModel report, List<Event> focusEvents, List<Event> oppEvents)
    {
        int focusPasses = focusEvents.Count(e => e.Category == "pass");
        int focusPassSuccess = focusEvents.Count(e => e.Category == "pass" && (e.Outcome == "successful" || e.Outcome == "key"));
        double focusPassAcc = focusPasses > 0 ? Math.Round((double)focusPassSuccess * 100.0 / focusPasses, 1) : 0.0;

        int oppPasses = oppEvents.Count(e => e.Category == "pass");
        int oppPassSuccess = oppEvents.Count(e => e.Category == "pass" && (e.Outcome == "successful" || e.Outcome == "key"));
        double oppPassAcc = oppPasses > 0 ? Math.Round((double)oppPassSuccess * 100.0 / oppPasses, 1) : 0.0;

        int focusBoxEntries = focusEvents.Count(e => (e.Category == "pass" || e.Category == "carry") && e.EndX >= 83.0 && e.EndY >= 21.0 && e.EndY <= 79.0);
        int oppBoxEntries = oppEvents.Count(e => (e.Category == "pass" || e.Category == "carry") && e.EndX >= 83.0 && e.EndY >= 21.0 && e.EndY <= 79.0);

        int focusRecoveries = focusEvents.Count(IsDefensiveRecoveryOrTackle);
        int oppRecoveries = oppEvents.Count(IsDefensiveRecoveryOrTackle);

        int focusProgressive = focusEvents.Count(e => e.Category == "pass" && e.EndX.HasValue && e.X.HasValue && (e.EndX.Value - e.X.Value) >= 15.0);
        int oppProgressive = oppEvents.Count(e => e.Category == "pass" && e.EndX.HasValue && e.X.HasValue && (e.EndX.Value - e.X.Value) >= 15.0);

        report.Kpis.Add(new TacticalKpi(
            "الأهداف المتوقعة (xG)", "Expected Goals", 
            report.FocusXg.ToString("F2"), 
            report.OpponentXg.ToString("F2"), 
            "1.40", 
            report.FocusXg >= report.OpponentXg && report.FocusXg > 0 ? "متفوق هجومياً" : (report.FocusXg == 0 ? "بانتظار تسديدات" : "أقل من المنافس"), 
            "نموذج هندسي يحسب احتمالية التسجيل بناءً على المسافة وزاوية رؤية المرمى"));

        report.Kpis.Add(new TacticalKpi(
            "التهديد المتوقع من التمرير والتحرك (xT)", "Expected Threat", 
            report.FocusXt.ToString("F2"), 
            report.OpponentXt.ToString("F2"), 
            "1.10", 
            report.FocusXt >= report.OpponentXt && report.FocusXt > 0 ? "تدرج خطير بالكرة" : "تدوير سلبي", 
            "يقيس القيمة الهجومية المضافة عند نقل الكرة إلى مناطق الخطورة (شبكة 12×8)"));

        report.Kpis.Add(new TacticalKpi(
            "مؤشر ضغط الخصم (PPDA)", "Pressing Intensity", 
            report.FocusPpda > 0 ? report.FocusPpda.ToString("F1") : "-", 
            report.OpponentPpda > 0 ? report.OpponentPpda.ToString("F1") : "-", 
            "9.5", 
            report.FocusPpda > 0 && report.FocusPpda <= 10.5 ? "ضغط عالي شرس" : (report.FocusPpda == 0 ? "لا توجد تدخلات كافية" : "كتلة متوسطة/منخفضة"), 
            "كلما انخفض الرقم، زادت سرعة وشراسة الفريق في استرجاع الكرة"));

        report.Kpis.Add(new TacticalKpi(
            "ميل السيطرة الميدانية (Field Tilt)", "Final Third Dominance", 
            report.FocusFieldTilt > 0 ? $"{report.FocusFieldTilt}%" : "0%", 
            report.OpponentFieldTilt > 0 ? $"{report.OpponentFieldTilt}%" : "0%", 
            "50.0%", 
            report.FocusFieldTilt >= 50 ? "مسيطر هجومياً" : (report.FocusFieldTilt == 0 ? "بانتظار أحداث الهجوم" : "تراجع للخلف"), 
            "نسبة الاستحواذ والتمريرات التكتيكية داخل الثلث الهجومي الأخير"));

        report.Kpis.Add(new TacticalKpi(
            "اختراقات منطقة الجزاء (Box Entries)", "Deep Box Entries", 
            focusBoxEntries.ToString(), oppBoxEntries.ToString(), "18", 
            focusBoxEntries >= oppBoxEntries && focusBoxEntries > 0 ? "متفوق" : (focusBoxEntries == 0 ? "0 اختراقات" : "متراجع"), 
            "التمريرات والتوغلات الناجحة داخل منطقة الـ 18 ياردة"));

        report.Kpis.Add(new TacticalKpi(
            "التمريرات التقدمية (Progressive Passes)", "Progressive Passing", 
            focusProgressive.ToString(), oppProgressive.ToString(), "35", 
            focusProgressive >= oppProgressive && focusProgressive > 0 ? "إيجابي" : "بحاجة لتعزيز", 
            "التمريرات التي تنقل الفريق للأمام بأكثر من 15% من طول الملعب نحو مرمى الخصم"));

        report.Kpis.Add(new TacticalKpi(
            "دقة التمرير الإجمالية", "Pass Completion %", 
            focusPasses > 0 ? $"{focusPassAcc}%" : "0%", 
            oppPasses > 0 ? $"{oppPassAcc}%" : "0%", 
            "82.0%", 
            focusPassAcc >= 80 ? "دقيق" : (focusPasses == 0 ? "لا توجد تمريرات" : "بحاجة لتحسين"), 
            "نسبة نجاح التمريرات المكتملة في كافة خطوط اللعب"));

        report.Kpis.Add(new TacticalKpi(
            "استرجاعات الكرة (Recoveries)", "Ball Recoveries", 
            focusRecoveries.ToString(), oppRecoveries.ToString(), "45", 
            focusRecoveries >= oppRecoveries && focusRecoveries > 0 ? "شراسة دفاعية" : (focusRecoveries == 0 ? "0 استرجاعات" : "بطء ردة فعل"), 
            "عدد مرات قطع الكرة واستعادة الاستحواذ بعد فقدانه"));
    }

    private void BuildSwotAnalysis(TacticalReportModel report, List<Event> focusEvents, List<Event> oppEvents, Match match)
    {
        if (focusEvents.Count < 2)
        {
            report.Strengths.Add(new SwotItem(
                "بانتظار تسجيل المزيد من أحداث الفريق",
                $"تم تسجيل ({focusEvents.Count}) أحداث فقط لفريق {report.FocusTeam.NameAr}. يرجى توثيق التمريرات والهجمات عبر شاشة 'تصنيف الأحداث' لتفعيل المؤشرات الديناميكية الكاملة.",
                "جمع البيانات", "تنبيه تحليلي", new() { "insufficient_events" }
            ));

            report.Weaknesses.Add(new SwotItem(
                "لم تُرصد ثغرات إحصائية كافية بعد",
                "يتطلب استنتاج نقاط الضعف رصد حالات فقدان الكرة والتسديدات المستقبلة والأخطاء المرتكبة أثناء سير المباراة.",
                "التحليل الدفاعي", "تنبيه تحليلي", new() { "no_weakness_data" }
            ));

            report.Opportunities.Add(new SwotItem(
                $"استكشاف نقاط ضعف {report.OpponentTeam.NameAr}",
                $"قم بتسجيل أخطاء وتدخلات فريق {report.OpponentTeam.NameAr} لتحديد المساحات المتاحة بدقة.",
                "دراسة الخصم", "فرصة استكشافية", new() { "scout_opponent" }
            ));

            report.Threats.Add(new SwotItem(
                $"رصد مفاتيح خطورة {report.OpponentTeam.NameAr}",
                $"سجّل هجمات وتسديدات فريق {report.OpponentTeam.NameAr} لتحديد مصادر الخطورة التكتيكية الرئيسية.",
                "تقييم المخاطر", "مراقبة", new() { "monitor_threats" }
            ));
            return;
        }

        int leftAttacks = focusEvents.Count(e => e.Y >= 66.0 && e.X >= 50.0);
        int rightAttacks = focusEvents.Count(e => e.Y <= 34.0 && e.X >= 50.0);
        int centralAttacks = focusEvents.Count(e => e.Y > 34.0 && e.Y < 66.0 && e.X >= 50.0);
        int totalAttacks = leftAttacks + rightAttacks + centralAttacks;

        if (totalAttacks > 0)
        {
            if (leftAttacks >= rightAttacks && leftAttacks >= centralAttacks && leftAttacks > 0)
            {
                int pct = (int)Math.Round((double)leftAttacks * 100.0 / totalAttacks);
                report.Strengths.Add(new SwotItem(
                    $"فاعلية هجومية مركزة عبر الجبهة اليسرى ({pct}%)",
                    $"ركّز فريق {report.FocusTeam.NameAr} على توجيه غالبية هجماته في نصف ملعب الخصم عبر الممر الأيسر بواقع {leftAttacks} محاولات من إجمالي {totalAttacks}.",
                    "الانتشار الهجومي", "عالية", new() { "left_flank_dominance" }
                ));
            }
            else if (rightAttacks >= leftAttacks && rightAttacks >= centralAttacks && rightAttacks > 0)
            {
                int pct = (int)Math.Round((double)rightAttacks * 100.0 / totalAttacks);
                report.Strengths.Add(new SwotItem(
                    $"فاعلية هجومية مركزة عبر الجبهة اليمنى ({pct}%)",
                    $"شكّل الرواق الأيمن المنفذ الرئيسي للعمليات الهجومية لفريق {report.FocusTeam.NameAr} بواقع {rightAttacks} محاولات هجومية متقدمة ({pct}%).",
                    "الانتشار الهجومي", "عالية", new() { "right_flank_dominance" }
                ));
            }
            else if (centralAttacks > 0)
            {
                int pct = (int)Math.Round((double)centralAttacks * 100.0 / totalAttacks);
                report.Strengths.Add(new SwotItem(
                    $"اختراق عمودي مباشر عبر العمق ومنطقة 14 ({pct}%)",
                    $"اعتمد فريق {report.FocusTeam.NameAr} على التغلغل المباشر بين خطوط دفاع الخصم في عمق الملعب بواقع {centralAttacks} هجمات من العمق.",
                    "بناء اللعب", "عالية", new() { "central_penetration" }
                ));
            }
        }

        int passes = focusEvents.Count(e => e.Category == "pass");
        int successPasses = focusEvents.Count(e => e.Category == "pass" && (e.Outcome == "successful" || e.Outcome == "key"));
        if (passes >= 3)
        {
            double acc = Math.Round((double)successPasses * 100.0 / passes, 1);
            if (acc >= 75.0)
            {
                report.Strengths.Add(new SwotItem(
                    $"انضباط ودقة في تدوير الكرة ({acc}%)",
                    $"أظهر الفريق قدرة عالية على الحفاظ على الاستحواذ بنجاح {successPasses} تمريرة من أصل {passes} تمريرة مكتملة.",
                    "الاستحواذ", "متوسطة", new() { "passing_efficiency" }
                ));
            }
        }

        int boxEntries = focusEvents.Count(e => (e.Category == "pass" || e.Category == "carry") && e.EndX >= 83.0);
        if (boxEntries > 0)
        {
            report.Strengths.Add(new SwotItem(
                $"الوصول المتكرر لمنطقة جزاء الخصم ({boxEntries} مرات)",
                $"نجح لاعبو الفريق في إيصال الكرة داخل منطقة الـ 18 ياردة للمنافس {boxEntries} مرات مما شكّل خطورة مباشرة.",
                "الإنهاء", "عالية", new() { "box_entries" }
            ));
        }

        int recoveries = focusEvents.Count(IsDefensiveRecoveryOrTackle);
        if (recoveries >= 2)
        {
            report.Strengths.Add(new SwotItem(
                $"استعادة سريعة للكرة والكرات الثانية ({recoveries} استرجاعات)",
                $"أظهر الفريق يقظة دفاعية في قطع الكرات وإعادة امتلاك الاستحواذ بعد فقدانه في {recoveries} مناسبات.",
                "الاسترجاع الدفاعي", "عالية", new() { "recoveries_high" }
            ));
        }

        if (report.Strengths.Count == 0)
        {
            report.Strengths.Add(new SwotItem(
                "استقرار خطوط اللعب وتوازن التمركز",
                "الفريق يحافظ على هيكله التكتيكي الأساسي في انتظار زيادة نسق المبادرة الهجومية.",
                "التنظيم الميداني", "متوسطة", new() { "tactical_shape" }
            ));
        }

        int defTurnovers = focusEvents.Count(e => IsTurnoverOrLoss(e) && e.X <= 38.0);
        if (defTurnovers > 0)
        {
            report.Weaknesses.Add(new SwotItem(
                $"فقدان خطير للكرة في الثلث الدفاعي الأول ({defTurnovers} حالات)",
                $"رُصدت {defTurnovers} حالات تمرير غير مكتمل أو فقدان للاستحواذ في مناطق بناء اللعب الخلفية، مما يعرض المرمى للخطورة المباشرة.",
                "التنظيم الدفاعي", "حرجة", new() { "defensive_turnovers" }
            ));
        }

        if (passes >= 4 && (double)successPasses / passes < 0.70)
        {
            double acc = Math.Round((double)successPasses * 100.0 / passes, 1);
            report.Weaknesses.Add(new SwotItem(
                $"تراجع دقة التمرير الإجمالية ({acc}%)",
                $"الفريق يعاني من فقدان الاستحواذ بمعدل تشتيت ملحوظ ({passes - successPasses} تمريرات خاطئة من أصل {passes}).",
                "بناء اللعب", "عالية", new() { "low_pass_accuracy" }
            ));
        }

        int fouls = focusEvents.Count(e => e.Category == "foul");
        if (fouls >= 1)
        {
            report.Weaknesses.Add(new SwotItem(
                $"ارتكاب المخالفات الدفاعية ({fouls} أخطاء)",
                $"منح المنافس {fouls} كرات ثابتة قد تشكل تهديداً في الكرات الهوائية والعرضيات المقوسة.",
                "الانضباط التكتيكي", "متوسطة", new() { "fouls_conceded" }
            ));
        }

        int shots = focusEvents.Count(e => e.Category == "shot");
        int goals = focusEvents.Count(e => e.Outcome == "goal" || (e.QualifiersJson != null && e.QualifiersJson.Contains("goal")));
        if (shots >= 2 && goals == 0)
        {
            report.Weaknesses.Add(new SwotItem(
                "إهدار الفرص وغياب الفاعلية الحاسمة أمام المرمى",
                $"سدد الفريق {shots} مرات على مرمى الخصم دون ترجمتها إلى أهداف محققة رغم الاقتراب من مناطق الخطورة.",
                "الإنهاء", "عالية", new() { "missed_chances" }
            ));
        }

        if (report.Weaknesses.Count == 0)
        {
            report.Weaknesses.Add(new SwotItem(
                "الحاجة لزيادة الكثافة العددية داخل صندوق الخصم",
                "الفريق بحاجة للمزيد من الجرأة الهجومية وإرسال مساندة عددية من لاعبي الوسط أثناء الكرات العرضية.",
                "الهجوم", "متوسطة", new() { "box_presence" }
            ));
        }

        int oppDefTurnovers = oppEvents.Count(e => IsTurnoverOrLoss(e) && e.X <= 42.0);
        if (oppDefTurnovers > 0)
        {
            report.Opportunities.Add(new SwotItem(
                $"استغلال ارتباك دفاع {report.OpponentTeam.NameAr} تحت الضغط العالي",
                $"فقد فريق {report.OpponentTeam.NameAr} الكرة {oppDefTurnovers} مرات في مناطقه الدفاعية، مما يجعل الضغط العالي سلاحاً قاتلاً لانتزاع انفرادات.",
                "الضغط العالي", "فرصة ذهبية", new() { "press_opponent_backline" }
            ));
        }

        int oppFouls = oppEvents.Count(e => e.Category == "foul");
        if (oppFouls > 0)
        {
            report.Opportunities.Add(new SwotItem(
                $"استثمار الكرات الثابتة الناتجة عن أخطاء {report.OpponentTeam.NameAr} ({oppFouls} مخالفات)",
                $"الخصم يرتكب أخطاء تكتيكية متكررة يمكن ترجمتها لأهداف عبر الركلات الحرة المباشرة والعرضيات المركزة.",
                "الكرات الثابتة", "عالية", new() { "exploit_set_pieces" }
            ));
        }

        if (report.Opportunities.Count == 0)
        {
            report.Opportunities.Add(new SwotItem(
                $"عزل دفاع {report.OpponentTeam.NameAr} ونقل اللعب للمساحات النصفية",
                $"التمرير السريع خلف خط وسط {report.OpponentTeam.NameAr} يمنح المهاجمين مساحات انطلاق حرة.",
                "التحول الهجومي", "فرصة واعدة", new() { "half_spaces" }
            ));
        }

        int oppShots = oppEvents.Count(e => e.Category == "shot");
        if (oppShots > 0)
        {
            report.Threats.Add(new SwotItem(
                $"خطورة محاولات {report.OpponentTeam.NameAr} على المرمى ({oppShots} تسديدات)",
                $"وصل المنافس إلى مناطق التسديد {oppShots} مرات بإجمالي xG متوقع {report.OpponentXg:F2}، مما يستدعي إغلاق زوايا التسديد فوراً.",
                "الهجوم المباشر للمنافس", "تهديد رئيسي", new() { "opp_shooting_threat" }
            ));
        }

        int oppBoxEntries = oppEvents.Count(e => (e.Category == "pass" || e.Category == "carry") && e.EndX >= 83.0);
        if (oppBoxEntries > 0)
        {
            report.Threats.Add(new SwotItem(
                $"توغلات {report.OpponentTeam.NameAr} داخل منطقة الجزاء ({oppBoxEntries} اختراقات)",
                $"أظهر فريق {report.OpponentTeam.NameAr} قدرة على اختراق العمق الدفاعي وإرسال كرات داخل منطقة الـ 18 ياردة.",
                "العمق الدفاعي", "عالية", new() { "opp_box_entries" }
            ));
        }

        if (report.Threats.Count == 0)
        {
            report.Threats.Add(new SwotItem(
                $"التحولات الهجومية المباغتة لفريق {report.OpponentTeam.NameAr}",
                $"الحذر من الهجمات المرتدة السريعة خلف أظهرتنا عند فقدان الكرة في نصف ملعب الخصم.",
                "الارتداد الدفاعي", "متوسطة", new() { "counter_risk" }
            ));
        }
    }

    private void BuildPlayerProfiles(TacticalReportModel report, List<Event> focusEvents, int teamId)
    {
        var playerGroups = focusEvents
            .Where(e => e.PlayerId.HasValue && e.Player != null)
            .GroupBy(e => e.Player!)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .ToList();

        foreach (var grp in playerGroups)
        {
            var p = grp.Key;
            var actions = grp.ToList();
            int total = actions.Count;
            int successful = actions.Count(a => a.Outcome == "successful" || a.Outcome == "key" || a.Outcome == "goal");
            double successRate = total > 0 ? Math.Round((double)successful * 100.0 / total, 1) : 0;
            int keyPasses = actions.Count(a => a.Category == "pass" && ((a.QualifiersJson != null && (a.QualifiersJson.Contains("leads_to_shot") || a.QualifiersJson.Contains("key_pass"))) || a.Outcome == "key"));
            int recoveries = actions.Count(IsDefensiveRecoveryOrTackle);
            int shots = actions.Count(a => a.Category == "shot");
            int failed = actions.Count(IsTurnoverOrLoss);
            double playerXt = Math.Round(actions.Sum(CalculateActionXt), 2);

            string role = p.Position switch
            {
                "ST" => "المهاجم الهداف ومحطة الإنهاء",
                "AM" => "صانع الألعاب ومحرك الثلث الأخير",
                "LW" => "الجناح الأيسر المهاجم",
                "RW" => "الجناح الأيمن المهاجم",
                "CM" => "دينامو خط الوسط وموزع اللعب",
                "DM" => "لاعب الارتكاز الدفاعي وحامي العمق",
                "LB" => "الظهير الأيسر",
                "RB" => "الظهير الأيمن",
                "CB" => "قلب الدفاع وصمام الأمان",
                "GK" => "حارس المرمى وباني الهجمة الأول",
                _ => $"عنصر خط {p.Position}"
            };

            string dynamicStrength;
            if (shots > 0)
                dynamicStrength = $"الجرأة على المرمى وتنفيذ {shots} تسديدات خطيرة";
            else if (keyPasses > 0 || playerXt >= 0.08)
                dynamicStrength = $"صناعة الخطورة (xT: {playerXt:F2}) وتمرير {keyPasses} كرات مفتاحية";
            else if (recoveries >= 1)
                dynamicStrength = $"شراسة الافتكاك واستعادة الكرة في {recoveries} مناسبات";
            else if (successRate >= 80.0 && total >= 2)
                dynamicStrength = $"دقة عالية في تنفيذ التمريرات والواجبات بنسبة {successRate}%";
            else
                dynamicStrength = $"الالتزام بالتمركز المطلوب وربط الخطوط في مركزه ({p.Position})";

            string dynamicVuln;
            if (failed >= 1)
                dynamicVuln = $"فقدان الكرة تحت الضغط في {failed} مناسبات مما يستدعي سرعة التمرير";
            else if (total < 2)
                dynamicVuln = "قلة الظهور والمشاركة الإيجابية في بناء الهجمات حتى الآن";
            else
                dynamicVuln = "الحاجة لتسريع التحول العكسي والمساندة الدفاعية دون كرة";

            report.KeyPlayers.Add(new PlayerProfile(
                p.Id,
                p.NameAr,
                p.Position,
                p.ShirtNumber,
                role,
                dynamicStrength,
                dynamicVuln,
                total,
                successRate,
                keyPasses,
                recoveries,
                playerXt
            ));
        }
    }

    private void BuildCoachingDirectives(TacticalReportModel report, List<Event> focusEvents, List<Event> oppEvents)
    {
        int defTurnovers = focusEvents.Count(e => IsTurnoverOrLoss(e) && e.X <= 40.0);
        int leftAttacks = focusEvents.Count(e => e.Y >= 66.0 && e.X >= 50.0);
        int rightAttacks = focusEvents.Count(e => e.Y <= 34.0 && e.X >= 50.0);

        string possessionAdvice = (leftAttacks > rightAttacks)
            ? $"مواصلة استثمار التفوق في الجبهة اليسرى لفريق {report.FocusTeam.NameAr} مع إرسال تمريرات عكسية نحو القائم البعيد لإرباك دفاع {report.OpponentTeam.NameAr}."
            : $"استدراج خط وسط {report.OpponentTeam.NameAr} عبر التمرير العرضي السريع ثم اختراق المساحة النصفية بين قلبي الدفاع والظهير.";

        string outPossessionAdvice = defTurnovers > 0
            ? $"تقليل المجازفة في التمرير القصير داخل الثلث الأول، واعتماد التمرير الطولي المباشر نحو المحطة الهجومية لتفادي أخطاء بناء اللعب."
            : $"تطبيق ضغط عكسي مكثف في أول 5 ثوانٍ من فقدان الكرة لمنع {report.OpponentTeam.NameAr} من تشكيل هجمات مرتدة سريعة.";

        report.CoachingPlan.Add(new CoachingDirective(
            "مرحلة الاستحواذ (In-Possession)",
            possessionAdvice,
            $"بناءً على التوزيع الميداني وتمركز هجمات {report.FocusTeam.NameAr} في اللقاء.",
            "أولوية قصوى"
        ));

        report.CoachingPlan.Add(new CoachingDirective(
            "مرحلة فقدان الكرة (Out-of-Possession)",
            outPossessionAdvice,
            "لمنع الخصم من استغلال المساحات الشاغرة وتأمين خط الدفاع الأول.",
            "أولوية قصوى"
        ));

        report.CoachingPlan.Add(new CoachingDirective(
            "التعامل مع التحولات (Defensive Transition)",
            $"إلزام لاعب الارتكاز بالتمركز الصارم أمام قلبي الدفاع لتأمين عمق الملعب ومنع انطلاقات أجنحة {report.OpponentTeam.NameAr}.",
            "لتقليل خطورة الارتداد الهجومي السريع للمنافس.",
            "عالية"
        ));

        report.CoachingPlan.Add(new CoachingDirective(
            "الكرات الثابتة (Set Pieces)",
            "استغلال الركلات الركنية والثابتة بتوجيه العرضيات المقوسة نحو منطقة الـ 6 ياردات مع تحرك المهاجم للقائم القريب.",
            $"استثمار نقاط ضعف ارتداد دفاع {report.OpponentTeam.NameAr} في الكرات العرضية.",
            "متوسطة"
        ));
    }
}
