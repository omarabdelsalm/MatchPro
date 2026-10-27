namespace MatchAnalystPro.Server.Models;

public class Possession
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match? Match { get; set; }
    public int TeamId { get; set; }
    public int SequenceNumber { get; set; }       // ترتيب الحيازة في المباراة
    public int Period { get; set; }
    public long StartFrame { get; set; }
    public long EndFrame { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public int EventCount { get; set; }
    public string StartType { get; set; } = "";    // "kick_off", "throw_in", "interception"...
    public string EndType { get; set; } = "";      // "shot", "out", "foul"...
    public bool LedToShot { get; set; }
    public bool LedToGoal { get; set; }
    public double? Xg { get; set; }
    public List<Event> Events { get; set; } = new();
}
