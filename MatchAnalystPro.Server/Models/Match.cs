namespace MatchAnalystPro.Server.Models;

public class Match
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int HomeTeamId { get; set; }
    public Team? HomeTeam { get; set; }
    public int AwayTeamId { get; set; }
    public Team? AwayTeam { get; set; }
    public string Competition { get; set; } = "الدوري المصري الممتاز";
    public string? Venue { get; set; }
    public bool IsNeutralVenue { get; set; }
    public string? VideoPath { get; set; }
    public int VideoFps { get; set; } = 25;
    public int VideoDurationMs { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Tagging, Review, Complete
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Event> Events { get; set; } = new();
    public List<Possession> Possessions { get; set; } = new();
    public List<PitchCalibration> Calibrations { get; set; } = new();
}
