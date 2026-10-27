using MatchAnalystPro.Server.Models;
using MatchAnalystPro.Server.Taxonomy;
using System.Text;

namespace MatchAnalystPro.Server.Services.Export;

public class OptaExporter
{
    public string Export(Match match, List<Event> events)
    {
        // تنسيق F24 المبسّط (CSV)
        var sb = new StringBuilder();
        sb.AppendLine("EventId,MatchId,Period,Min,Sec,TeamId,PlayerId,Type,Outcome,X,Y,EndX,EndY,Qualifiers");

        int id = 1;
        foreach (var e in events.OrderBy(x => x.FrameNumber))
        {
            var sub = EventTaxonomy.GetSubType(e.Category, e.SubType);
            var type = MapOptaType(e.Category, sub?.OptaName);

            sb.AppendLine(string.Join(",",
                id++,
                match.Id,
                e.Period,
                e.Minute,
                e.Second,
                e.TeamId,
                e.PlayerId ?? 0,
                type,
                e.Outcome == "successful" ? "1" : "0",
                e.X?.ToString("F2") ?? "",
                e.Y?.ToString("F2") ?? "",
                e.EndX?.ToString("F2") ?? "",
                e.EndY?.ToString("F2") ?? "",
                EscapeCsv(e.QualifiersJson ?? "[]")
            ));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

    private static string MapOptaType(string cat, string? subtype) => (cat, subtype) switch
    {
        ("pass", "cross") => "Cross",
        ("pass", "through_ball") => "Through Ball",
        ("pass", _) => "Pass",
        ("shot", _) => "Shot",
        ("carry", _) => "Take On",
        ("duel", "aerial") => "Aerial",
        ("duel", _) => "Tackle",
        ("interception", _) => "Interception",
        ("clearance", _) => "Clearance",
        _ => cat
    };
}
