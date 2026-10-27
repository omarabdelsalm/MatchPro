using MatchAnalystPro.Server.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;

namespace MatchAnalystPro.Server.Services;

public class ReportService
{
    private readonly string _fontPath;
    
    public ReportService(IWebHostEnvironment env)
    {
        var webRoot = !string.IsNullOrEmpty(env.WebRootPath)
            ? env.WebRootPath
            : Path.Combine(AppContext.BaseDirectory, "wwwroot");
        _fontPath = Path.Combine(webRoot, "fonts", "Cairo-Regular.ttf");
        QuestPDF.Settings.License = LicenseType.Community;
        
        if (File.Exists(_fontPath))
        {
            try
            {
                using var stream = File.OpenRead(_fontPath);
                FontManager.RegisterFont(stream);
            }
            catch { }
        }
    }

    public byte[] GenerateOppositionReport(
        Match match, 
        OppositionReport report,
        List<Event> events,
        List<Player> keyPlayers)
    {
        double totalXg = Math.Round(events.Where(e => e.Category == "shot").Sum(TacticalIntelligenceService.CalculateShotXg), 2);
        double totalXt = Math.Round(events.Sum(TacticalIntelligenceService.CalculateActionXt), 2);
        int totalPasses = events.Count(e => e.Category == "pass");
        int succPasses = events.Count(e => e.Category == "pass" && (e.Outcome == "successful" || e.Outcome == "key"));
        double passAcc = totalPasses > 0 ? Math.Round(succPasses * 100.0 / totalPasses, 1) : 0;
        int boxEntries = events.Count(e => (e.Category == "pass" || e.Category == "carry") && e.EndX >= 83.0 && e.EndY >= 21.0 && e.EndY <= 79.0);

        var freezeEvents = events
            .Where(e => !string.IsNullOrEmpty(e.FreezeFramePath) && File.Exists(e.FreezeFramePath))
            .OrderBy(e => e.FrameNumber)
            .Take(6)
            .ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontFamily("Cairo").FontSize(10).DirectionFromRightToLeft());

                // ─── الغلاف والرأسية ───
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("تقرير سكاوتنج الخصم التكتيكي")
                                .FontSize(22).Bold().FontColor("#C8102E");
                            c.Item().Text($"{match.HomeTeam?.NameAr} vs {match.AwayTeam?.NameAr}")
                                .FontSize(14).FontColor("#333");
                            c.Item().Text($"{match.Competition} • {match.Date:dd/MM/yyyy}")
                                .FontSize(10).FontColor("#666");
                        });
                        row.ConstantItem(140).AlignLeft().Column(c =>
                        {
                            c.Item().Text($"المحلل: {report.AnalystName}").FontSize(10);
                            c.Item().Text($"التاريخ: {DateTime.Now:dd/MM/yyyy}").FontSize(10);
                        });
                    });
                    col.Item().PaddingVertical(8).LineHorizontal(2).LineColor("#C8102E");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    // ─── شريط المؤشرات الرقمية المتقدمة (xG, xT, Pass%, Box Entries) ───
                    col.Item().Background("#F5F5F5").Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("الرسم التكتيكي").FontSize(9).FontColor("#666");
                            c.Item().Text(string.IsNullOrEmpty(report.Formation) ? "4-2-3-1" : report.Formation).FontSize(14).Bold().FontColor("#C8102E");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("الأهداف المتوقعة (xG)").FontSize(9).FontColor("#666");
                            c.Item().Text(totalXg.ToString("F2")).FontSize(14).Bold();
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("التهديد المتوقع (xT)").FontSize(9).FontColor("#666");
                            c.Item().Text(totalXt.ToString("F2")).FontSize(14).Bold();
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("دقة التمرير").FontSize(9).FontColor("#666");
                            c.Item().Text($"{passAcc}%").FontSize(14).Bold();
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("اختراقات الصندوق").FontSize(9).FontColor("#666");
                            c.Item().Text(boxEntries.ToString()).FontSize(14).Bold();
                        });
                    });

                    // ─── الأقسام التكتيكية ───
                    Section(col, "1. البناء من الخلف", report.BuildUpNotes);
                    Section(col, "2. الضغط والضغط المضاد", report.PressingNotes);
                    Section(col, "3. الأنماط الهجومية", report.AttackingPatterns);
                    Section(col, "4. نقاط الضعف الدفاعية", report.DefensiveWeaknesses);
                    Section(col, "5. الكرات الثابتة", report.SetPieceNotes);
                    Section(col, "6. اللاعبون المفتاحيون", report.KeyPlayersNotes);

                    // ─── الإحصائيات التكتيكية ───
                    col.Item().PaddingTop(10).Text("📊 ملخص إحصائي للأحداث")
                        .FontSize(13).Bold().FontColor("#C8102E");

                    var grouped = events.GroupBy(e => e.Category)
                        .OrderByDescending(g => g.Count()).ToList();
                    
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background("#C8102E").Padding(6)
                                .Text("الفئة").FontColor(Colors.White).Bold();
                            h.Cell().Background("#C8102E").Padding(6)
                                .AlignCenter().Text("العدد").FontColor(Colors.White).Bold();
                            h.Cell().Background("#C8102E").Padding(6)
                                .AlignCenter().Text("النسبة").FontColor(Colors.White).Bold();
                        });
                        
                        foreach (var g in grouped)
                        {
                            var ar = Taxonomy.EventTaxonomy.GetCategory(g.Key)?.Ar ?? g.Key;
                            var pct = events.Count > 0 ? (g.Count() * 100.0 / events.Count) : 0;
                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(ar);
                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(g.Count().ToString());
                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text($"{pct:F1}%");
                        }
                    });

                    // ─── اللاعبون المفتاحيون ───
                    if (keyPlayers.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Text("⭐ اللاعبون المفتاحيون")
                            .FontSize(14).Bold().FontColor("#C8102E");

                        foreach (var p in keyPlayers)
                        {
                            var pEvents = events.Where(e => e.PlayerId == p.Id).ToList();
                            double pXt = Math.Round(pEvents.Sum(TacticalIntelligenceService.CalculateActionXt), 2);
                            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                            {
                                c.Item().Text($"{p.ShirtNumber} - {p.NameAr} ({p.Position})")
                                    .FontSize(12).Bold();
                                c.Item().Text($"عدد الأحداث: {pEvents.Count} | تسديدات: {pEvents.Count(e => e.Category == "shot")} | تهديد متوقع (xT): {pXt:F2}")
                                    .FontSize(10).FontColor("#666");
                            });
                        }
                    }

                    // ─── معرض الحالات التكتيكية المصورة (Freeze Frames & Telestration) ───
                    if (freezeEvents.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Text("📸 اللقطات التكتيكية المصورة (Freeze Frames & Telestration)")
                            .FontSize(14).Bold().FontColor("#C8102E");

                        foreach (var fe in freezeEvents)
                        {
                            try
                            {
                                var imgBytes = File.ReadAllBytes(fe.FreezeFramePath!);
                                var catAr = Taxonomy.EventTaxonomy.GetCategory(fe.Category)?.Ar ?? fe.Category;
                                var subAr = Taxonomy.EventTaxonomy.GetSubType(fe.Category, fe.SubType)?.Ar ?? fe.SubType;
                                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                                {
                                    c.Item().Text($"الدقيقة {fe.Minute}:{fe.Second:00} — {catAr} ({subAr}) {(fe.Player != null ? $"• {fe.Player.NameAr}" : "")}")
                                        .FontSize(11).Bold().FontColor("#C8102E");
                                    if (!string.IsNullOrWhiteSpace(fe.Note))
                                    {
                                        c.Item().Text(fe.Note).FontSize(10).FontColor("#333");
                                    }
                                    c.Item().PaddingTop(4).MaxHeight(180).Image(imgBytes).FitArea();
                                });
                            }
                            catch { }
                        }
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Match Analyst Pro • صفحة ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        col.Item().PaddingTop(6).Text(title).FontSize(12).Bold().FontColor("#C8102E");
        col.Item().Text(content).FontSize(10).LineHeight(1.5f);
    }
}
