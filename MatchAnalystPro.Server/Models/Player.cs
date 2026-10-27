namespace MatchAnalystPro.Server.Models;

public class Player
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team? Team { get; set; }
    public string Name { get; set; } = "";
    public string NameAr { get; set; } = "";
    public int ShirtNumber { get; set; }
    public string Position { get; set; } = "";   // GK, CB, LB, CM, ST...
    public string PreferredFoot { get; set; } = "Right";
    public DateTime? BirthDate { get; set; }
    public int HeightCm { get; set; }
}
