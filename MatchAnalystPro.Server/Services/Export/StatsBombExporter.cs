using MatchAnalystPro.Server.Models;
using MatchAnalystPro.Server.Taxonomy;
using Newtonsoft.Json;

namespace MatchAnalystPro.Server.Services.Export;

public class StatsBombExporter
{
    public string Export(Match match, List<Event> events)
    {
        var sbEvents = new List<object>();
        int index = 1;

        foreach (var e in events.OrderBy(x => x.FrameNumber))
        {
            var sub = EventTaxonomy.GetSubType(e.Category, e.SubType);
            var sbType = sub?.StatsBombName ?? MapCategory(e.Category);

            var obj = new Dictionary<string, object?>
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["index"] = index++,
                ["period"] = e.Period,
                ["timestamp"] = FormatTimestamp(e.TimestampMs),
                ["minute"] = e.Minute,
                ["second"] = e.Second,
                ["type"] = new { name = sbType },
                ["possession"] = e.PossessionId ?? 0,
                ["possession_team"] = new { id = e.TeamId },
                ["play_pattern"] = new { name = "Regular Play" },
                ["team"] = new { id = e.TeamId },
                ["player"] = e.PlayerId.HasValue ? new { id = e.PlayerId.Value } : null,
            };

            if (e.X != null && e.Y != null)
                obj["location"] = new[] { e.X, e.Y };

            if (e.EndX != null && e.EndY != null)
                obj["pass"] = new { end_location = new[] { e.EndX, e.EndY } };

            // Qualifiers
            var qualifiers = JsonConvert.DeserializeObject<List<string>>(e.QualifiersJson ?? "[]") ?? new();
            if (qualifiers.Any())
                obj["qualifiers"] = qualifiers.Select(q => new { type = new { name = q } }).ToList();

            sbEvents.Add(obj);
        }

        var doc = new
        {
            metadata = new
            {
                match_id = match.Id,
                home_team = match.HomeTeam?.Name,
                away_team = match.AwayTeam?.Name,
                match_date = match.Date.ToString("yyyy-MM-dd"),
                competition = match.Competition,
                exporter = "MatchAnalystPro",
                exported_at = DateTime.UtcNow
            },
            events = sbEvents
        };

        return JsonConvert.SerializeObject(doc, Formatting.Indented);
    }

    private static string FormatTimestamp(long ms)
        => $"00:{(ms / 60000):00}:{((ms % 60000) / 1000):00}.{ms % 1000:000}";

    private static string MapCategory(string cat) => cat switch
    {
        "pass" => "Pass",
        "shot" => "Shot",
        "carry" => "Carry",
        "duel" => "Duel",
        "interception" => "Interception",
        "clearance" => "Clearance",
        "press" => "Pressure",
        "foul" => "Foul Committed",
        "set_piece" => "Set Piece",
        "goalkeeper" => "Goal Keeper",
        "substitution" => "Substitution",
        "miscontrol" => "Miscontrol",
        _ => cat
    };
}
