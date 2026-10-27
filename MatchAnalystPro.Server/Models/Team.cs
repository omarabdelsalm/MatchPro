namespace MatchAnalystPro.Server.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string League { get; set; } = "الدوري المصري";
    public string? Stadium { get; set; }
    public string? Coach { get; set; }
    public string? PrimaryColor { get; set; }
}
