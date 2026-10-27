namespace MatchAnalystPro.Server.Taxonomy;

public record SubTypeDef(
    string Key,
    string Ar,
    string? StatsBombName = null,
    string? OptaName = null,
    bool RequiresEndPoint = false,
    bool RequiresFreezeFrame = false);

public record CategoryDef(
    string Key,
    string Ar,
    string Color,
    string StatsBombName,
    List<SubTypeDef> SubTypes);

public static class EventTaxonomy
{
    public static readonly List<CategoryDef> Categories = new()
    {
        // ============ PASS (تمريرات) ============
        new("pass", "تمريرة", "#4A90D9", "Pass", new()
        {
            new("simple", "تمريرة عادية", "Simple", "Pass", RequiresEndPoint: true),
            new("short", "تمريرة قصيرة", "Short", "Pass", RequiresEndPoint: true),
            new("long", "تمريرة طويلة", "Long", "Long Ball", RequiresEndPoint: true),
            new("through_ball", "تمريرة بينية", "Through Ball", "Through Ball", RequiresEndPoint: true),
            new("cross", "عرضية", "Cross", "Cross", RequiresEndPoint: true),
            new("cutback", "عرضية خلفية", "Cut Back", "Cutback", RequiresEndPoint: true),
            new("switch", "تبديل لعب", "Switch", "Switch", RequiresEndPoint: true),
            new("back_pass", "تمريرة للخلف", "Back Pass", "Back Pass", RequiresEndPoint: true),
            new("forward_pass", "تمريرة للأمام", "Forward Pass", "Forward Pass", RequiresEndPoint: true),
            new("lateral", "تمريرة عرضية جانبية", "Lateral", "Lateral", RequiresEndPoint: true),
            new("head_pass", "تمريرة بالرأس", "Head Pass", "Head Pass", RequiresEndPoint: true),
            new("flick_on", "لمسة تحويل", "Flick On", "Flick On", RequiresEndPoint: true),
            new("lofted", "تمريرة مقوسة", "Lofted", "Lofted", RequiresEndPoint: true),
            new("hand_pass", "تمريرة باليد (حارس)", "Hand Pass", "Hand Pass", RequiresEndPoint: true),
            new("corner", "ركنية", "Corner", "Corner", RequiresEndPoint: true),
            new("goal_kick", "ركلة مرمى", "Goal Kick", "Goal Kick", RequiresEndPoint: true),
            new("free_kick", "ركلة حرة", "Free Kick", "Free Kick", RequiresEndPoint: true),
            new("throw_in", "رمية تماس", "Throw In", "Throw In", RequiresEndPoint: true),
            new("kick_off", "ركلة البداية", "Kick Off", "Kick Off", RequiresEndPoint: true),
        }),

        // ============ CARRY (تحرك بالكرة) ============
        new("carry", "تحرك", "#6FBF73", "Carry", new()
        {
            new("simple", "تحرك عادي", "Simple", "Carry", RequiresEndPoint: true),
            new("dribble", "مراوغة", "Dribble", "Dribble", RequiresEndPoint: true),
            new("run", "اندفاع بالكرة", "Run", "Run", RequiresEndPoint: true),
            new("counter", "هجمة مرتدة", "Counter", "Counter", RequiresEndPoint: true),
            new("open_play", "من اللعب المفتوح", "Open Play", "Open Play", RequiresEndPoint: true),
        }),

        // ============ SHOT (تسديدات) ============
        new("shot", "تسديدة", "#E74C3C", "Shot", new()
        {
            new("open_play", "من اللعب المفتوح", "Open Play", "Open Play", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("free_kick", "من ركلة حرة", "Free Kick", "Free Kick", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("corner", "من ركنية", "Corner", "Corner", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("penalty", "ركلة جزاء", "Penalty", "Penalty", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("counter", "من هجمة مرتدة", "Counter", "Counter", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("volley", "تسديدة طائرة", "Volley", "Volley", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("half_volley", "نصف طائرة", "Half Volley", "Half Volley", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("header", "تسديدة بالرأس", "Header", "Header", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("one_touch", "لمسة واحدة", "One Touch", "One Touch", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("first_time", "من أول لمسة", "First Time", "First Time", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("deflected", "مرتدة", "Deflected", "Deflected", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("blocked", "مصدودة", "Blocked", "Blocked", RequiresEndPoint: true, RequiresFreezeFrame: true),
            new("own_goal", "هدف عكسي", "Own Goal", "Own Goal", RequiresEndPoint: true, RequiresFreezeFrame: true),
        }),

        // ============ DUEL (صراعات) ============
        new("duel", "صراع", "#F39C12", "Duel", new()
        {
            new("aerial", "صراع هوائي", "Aerial", "Aerial", RequiresFreezeFrame: true),
            new("ground", "صراع أرضي", "Ground", "Ground Duel", RequiresFreezeFrame: true),
            new("loose_ball", "كرة سائبة", "Loose Ball", "Loose Ball", RequiresFreezeFrame: true),
            new("tackle", "تدخل", "Tackle", "Tackle", RequiresFreezeFrame: true),
            new("sliding_tackle", "تدخل انزلاقي", "Sliding Tackle", "Sliding Tackle", RequiresFreezeFrame: true),
            new("shoulder", "احتكاك كتف", "Shoulder", "Shoulder", RequiresFreezeFrame: true),
            new("pressing_duel", "صراع ضغط", "Pressing", "Pressing Duel", RequiresFreezeFrame: true),
            new("back_to_goal", "ظهر للمرمى", "Back to Goal", "Back to Goal", RequiresFreezeFrame: true),
        }),

        // ============ INTERCEPTION (اعتراض) ============
        new("interception", "اعتراض", "#16A085", "Interception", new()
        {
            new("pass_interception", "اعتراض تمريرة", "Pass Interception", "Interception"),
            new("block", "صد", "Block", "Block"),
            new("recovery", "استعادة", "Recovery", "Ball Recovery"),
            new("clearance", "تشتيت", "Clearance", "Clearance", RequiresEndPoint: true),
        }),

        // ============ PRESS (ضغط) ============
        new("press", "ضغط", "#8E44AD", "Pressure", new()
        {
            new("high_press", "ضغط عالٍ", "High Press", "Pressure"),
            new("mid_press", "ضغط متوسط", "Mid Press", "Pressure"),
            new("low_press", "ضغط منخفض", "Low Press", "Pressure"),
            new("counterpress", "ضغط مضاد", "Counterpress", "Counterpress"),
        }),

        // ============ FOUL (أخطاء) ============
        new("foul", "خطأ", "#C0392B", "Foul Committed", new()
        {
            new("foul_committed", "خطأ مرتكب", "Foul Committed", "Foul"),
            new("foul_won", "خطأ مكتسب", "Foul Won", "Foul Won"),
            new("yellow_card", "بطاقة صفراء", "Yellow Card", "Yellow Card"),
            new("red_card", "بطاقة حمراء", "Red Card", "Red Card"),
            new("penalty_conceded", "جزاء مرتكب", "Penalty Conceded", "Penalty"),
            new("penalty_won", "جزاء مكتسب", "Penalty Won", "Penalty Won"),
        }),

        // ============ SET PIECE (كرات ثابتة) ============
        new("set_piece", "كرة ثابتة", "#2C3E50", "Set Piece", new()
        {
            new("corner_in", "ركنية داخلية", "Corner In", "Corner"),
            new("corner_out", "ركنية خارجية", "Corner Out", "Corner"),
            new("free_kick_direct", "حرة مباشرة", "Free Kick Direct", "Free Kick"),
            new("free_kick_indirect", "حرة غير مباشرة", "Free Kick Indirect", "Free Kick"),
            new("throw_in", "رمية تماس", "Throw In", "Throw In"),
            new("goal_kick", "ركلة مرمى", "Goal Kick", "Goal Kick"),
            new("penalty", "ركلة جزاء", "Penalty", "Penalty"),
        }),

        // ============ GOALKEEPER (حارس) ============
        new("goalkeeper", "حارس المرمى", "#D35400", "Goal Keeper", new()
        {
            new("save", "تصدٍ", "Save", "Save", RequiresFreezeFrame: true),
            new("punch", "لكمة", "Punch", "Punch"),
            new("catch", "إمساك", "Catch", "Catch"),
            new("parry", "صد مع ارتداد", "Parry", "Parry"),
            new("sweeper", "خروج ككنس", "Sweeper", "Sweeper"),
            new("distribution", "توزيع", "Distribution", "Distribution"),
            new("claim", "استحواذ عالي", "Claim", "Claim"),
        }),

        // ============ SUBSTITUTION ============
        new("substitution", "تبديل", "#7F8C8D", "Substitution", new()
        {
            new("tactical", "تبديل تكتيكي", "Tactical", "Substitution"),
            new("injury", "تبديل إصابة", "Injury", "Substitution"),
            new("blood", "تبديل دم", "Blood", "Substitution"),
        }),

        // ============ MISCONTROL (فقدان السيطرة) ============
        new("miscontrol", "فقدان السيطرة", "#95A5A6", "Miscontrol", new()
        {
            new("first_touch_error", "خطأ في اللمسة الأولى", "First Touch", "Miscontrol"),
            new("heavy_touch", "لمسة ثقيلة", "Heavy Touch", "Miscontrol"),
            new("loss_of_control", "فقدان كرة", "Loss of Control", "Miscontrol"),
        }),
    };

    // Qualifiers (خصائص إضافية تُطبَّق على أي حدث)
    public static readonly Dictionary<string, string> Qualifiers = new()
    {
        ["under_pressure"] = "تحت ضغط",
        ["counterpress"] = "ضغط مضاد",
        ["counter"] = "هجمة مرتدة",
        ["open_play"] = "لعب مفتوح",
        ["left_foot"] = "قدم يسرى",
        ["right_foot"] = "قدم يمنى",
        ["head"] = "رأس",
        ["first_time"] = "أول لمسة",
        ["deflected"] = "مرتدة",
        ["blocked"] = "مصدودة",
        ["x_small"] = "مساحة صغيرة جداً",
        ["small"] = "مساحة صغيرة",
        ["medium"] = "مساحة متوسطة",
        ["large"] = "مساحة كبيرة",
        ["high_speed"] = "سرعة عالية",
        ["low_speed"] = "سرعة منخفضة",
        ["leads_to_shot"] = "أدت لتسديدة",
        ["leads_to_goal"] = "أدت لهدف",
        ["key_pass"] = "تمريرة مفتاحية",
        ["through_ball"] = "بينية",
        ["switch_play"] = "تبديل لعب",
        ["cross_play"] = "عرضية",
        ["advanced"] = "متقدم",
        ["behind_defense"] = "خلف الدفاع",
        ["between_lines"] = "بين الخطوط",
        ["outside_box"] = "خارج المنطقة",
        ["inside_box"] = "داخل المنطقة",
        ["six_yard_box"] = "منطقة الست ياردات",
        ["zone_14"] = "المنطقة 14",
    };

    public static CategoryDef? GetCategory(string key) =>
        Categories.FirstOrDefault(c => c.Key == key);

    public static SubTypeDef? GetSubType(string category, string key) =>
        GetCategory(category)?.SubTypes.FirstOrDefault(s => s.Key == key);
}
