namespace MatchAnalystPro.Server.Models;

public class OppositionReport
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match? Match { get; set; }
    public int OpponentTeamId { get; set; }
    public string Formation { get; set; } = "";
    public string BuildUpNotes { get; set; } = "";
    public string PressingNotes { get; set; } = "";
    public string AttackingPatterns { get; set; } = "";
    public string DefensiveWeaknesses { get; set; } = "";
    public string SetPieceNotes { get; set; } = "";
    public string KeyPlayersNotes { get; set; } = "";
    public string AnalystName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
