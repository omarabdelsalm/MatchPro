using MatchAnalystPro.Server.Components;
using MatchAnalystPro.Server.Data;
using MatchAnalystPro.Server.Models;
using MatchAnalystPro.Server.Services;
using MatchAnalystPro.Server.Services.Export;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace MatchAnalystPro.Server;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatchAnalystPro", "matches.db3");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));
        services.AddRazorComponents().AddInteractiveServerComponents();

        // الخدمات الأساسية
        services.AddSingleton<CoordinateService>();
        services.AddScoped<QualityControlService>();
        services.AddScoped<PossessionService>();
        services.AddScoped<QueryService>();
        services.AddScoped<ClipService>();
        services.AddScoped<ReportService>();
        services.AddScoped<VideoService>();
        services.AddScoped<TacticalIntelligenceService>();

        // المصدرون
        services.AddScoped<StatsBombExporter>();
        services.AddScoped<OptaExporter>();
        services.AddScoped<WyscoutExporter>();
    }

    public void Configure(WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            SeedData(db);

            var sampleVideo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatchAnalystPro", "videos", "sample_match.mp4");
            if (File.Exists(sampleVideo))
            {
                var m1 = db.Matches.FirstOrDefault(m => m.Id == 1 && string.IsNullOrEmpty(m.VideoPath));
                if (m1 != null)
                {
                    m1.VideoPath = sampleVideo;
                    db.SaveChanges();
                }
            }
        }

        // Ensure clips directory exists in WebRoot
        var root = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(root);
        var clipsDir = Path.Combine(root, "clips");
        Directory.CreateDirectory(clipsDir);

        var videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatchAnalystPro", "videos");
        Directory.CreateDirectory(videosDir);

        app.UseStaticFiles();
        app.UseAntiforgery();

        // نقطة نهاية خدمة الفيديو متعدد الكاميرات
        app.MapGet("/api/video/{matchId:int}/{cam:int}", (int matchId, int cam, AppDbContext db) =>
        {
            var match = db.Matches.FirstOrDefault(m => m.Id == matchId);
            if (match?.VideoPath == null) return Results.NotFound();

            string path = cam == 0
                ? match.VideoPath
                : Path.Combine(Path.GetDirectoryName(match.VideoPath) ?? "", $"cam{cam}.mp4");

            if (!File.Exists(path)) return Results.NotFound();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string contentType = ext switch
            {
                ".webm" => "video/webm",
                ".mkv" => "video/x-matroska",
                ".mov" => "video/quicktime",
                _ => "video/mp4"
            };
            return Results.File(path, contentType, enableRangeProcessing: true);
        });

        // نقطة نهاية مفردة للتوافق
        app.MapGet("/api/video/{matchId:int}", (int matchId, AppDbContext db) =>
        {
            var match = db.Matches.FirstOrDefault(m => m.Id == matchId);
            if (match?.VideoPath == null || !File.Exists(match.VideoPath)) return Results.NotFound();
            string ext = Path.GetExtension(match.VideoPath).ToLowerInvariant();
            string contentType = ext switch
            {
                ".webm" => "video/webm",
                ".mkv" => "video/x-matroska",
                ".mov" => "video/quicktime",
                _ => "video/mp4"
            };
            return Results.File(match.VideoPath, contentType, enableRangeProcessing: true);
        });

        // نقطة نهاية صور Freeze Frames
        app.MapGet("/api/freezes/{fileName}", (string fileName) =>
        {
            var freezeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatchAnalystPro", "freezes");
            var path = Path.Combine(freezeDir, fileName);
            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(path, "image/jpeg");
        });

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
    }

    private static void SeedData(AppDbContext db)
    {
        if (db.Teams.Any()) return;

        var ahly = new Team { Name = "Al Ahly SC", NameAr = "الأهلي", League = "الدوري المصري الممتاز", PrimaryColor = "#e61919" };
        var zamalek = new Team { Name = "Zamalek SC", NameAr = "الزمالك", League = "الدوري المصري الممتاز", PrimaryColor = "#ffffff" };
        var pyramids = new Team { Name = "Pyramids FC", NameAr = "بيراميدز", League = "الدوري المصري الممتاز", PrimaryColor = "#1f5f8b" };
        var masry = new Team { Name = "Al Masry", NameAr = "المصري البورسعيدي", League = "الدوري المصري الممتاز", PrimaryColor = "#008751" };
        var ismaily = new Team { Name = "Ismaily SC", NameAr = "الإسماعيلي", League = "الدوري المصري الممتاز", PrimaryColor = "#ffcc00" };
        var smouha = new Team { Name = "Smouha SC", NameAr = "سموحة", League = "الدوري المصري الممتاز", PrimaryColor = "#0055a5" };

        db.Teams.AddRange(ahly, zamalek, pyramids, masry, ismaily, smouha);
        db.SaveChanges();

        // Sample players for Al Ahly
        db.Players.AddRange(
            new Player { TeamId = ahly.Id, Name = "Mohamed El Shenawy", NameAr = "محمد الشناوي", ShirtNumber = 1, Position = "GK", PreferredFoot = "Right" },
            new Player { TeamId = ahly.Id, Name = "Mohamed Abdelmonem", NameAr = "محمد عبد المنعم", ShirtNumber = 24, Position = "CB", PreferredFoot = "Right" },
            new Player { TeamId = ahly.Id, Name = "Ali Maaloul", NameAr = "علي معلول", ShirtNumber = 21, Position = "LB", PreferredFoot = "Left" },
            new Player { TeamId = ahly.Id, Name = "Emam Ashour", NameAr = "إمام عاشور", ShirtNumber = 22, Position = "CM", PreferredFoot = "Right" },
            new Player { TeamId = ahly.Id, Name = "Marwan Attia", NameAr = "مروان عطية", ShirtNumber = 8, Position = "DM", PreferredFoot = "Right" },
            new Player { TeamId = ahly.Id, Name = "Hussein El Shahat", NameAr = "حسين الشحات", ShirtNumber = 14, Position = "LW", PreferredFoot = "Right" },
            new Player { TeamId = ahly.Id, Name = "Percy Tau", NameAr = "بيرسي تاو", ShirtNumber = 10, Position = "RW", PreferredFoot = "Left" },
            new Player { TeamId = ahly.Id, Name = "Wessam Abou Ali", NameAr = "وسام أبو علي", ShirtNumber = 9, Position = "ST", PreferredFoot = "Right" }
        );

        // Sample players for Zamalek
        db.Players.AddRange(
            new Player { TeamId = zamalek.Id, Name = "Mohamed Sobhy", NameAr = "محمد صبحي", ShirtNumber = 16, Position = "GK", PreferredFoot = "Right" },
            new Player { TeamId = zamalek.Id, Name = "Hossam Abdelmaguid", NameAr = "حسام عبد المجيد", ShirtNumber = 5, Position = "CB", PreferredFoot = "Right" },
            new Player { TeamId = zamalek.Id, Name = "Ahmed Fatouh", NameAr = "أحمد فتوح", ShirtNumber = 29, Position = "LB", PreferredFoot = "Left" },
            new Player { TeamId = zamalek.Id, Name = "Nabil Emad Dunga", NameAr = "نبيل عماد دونجا", ShirtNumber = 8, Position = "DM", PreferredFoot = "Right" },
            new Player { TeamId = zamalek.Id, Name = "Abdallah El Said", NameAr = "عبد الله السعيد", ShirtNumber = 19, Position = "AM", PreferredFoot = "Right" },
            new Player { TeamId = zamalek.Id, Name = "Ahmed Sayed Zizo", NameAr = "أحمد سيد زيزو", ShirtNumber = 25, Position = "RW", PreferredFoot = "Right" },
            new Player { TeamId = zamalek.Id, Name = "Mostafa Shalaby", NameAr = "مصطفى شلبي", ShirtNumber = 11, Position = "LW", PreferredFoot = "Right" },
            new Player { TeamId = zamalek.Id, Name = "Seifeddine Jaziri", NameAr = "سيف الدين الجزيري", ShirtNumber = 30, Position = "ST", PreferredFoot = "Right" }
        );

        db.SaveChanges();

        // Sample match
        var match = new Match
        {
            Date = DateTime.Today.AddDays(-2),
            HomeTeamId = ahly.Id,
            AwayTeamId = zamalek.Id,
            Competition = "الدوري المصري الممتاز",
            Venue = "ستاد القاهرة الدولي",
            VideoPath = "",
            VideoFps = 25,
            VideoDurationMs = 90 * 60 * 1000,
            Status = "Tagging"
        };
        db.Matches.Add(match);
        db.SaveChanges();

        // Sample events
        db.Events.AddRange(
            new Event
            {
                MatchId = match.Id,
                TeamId = ahly.Id,
                PlayerId = db.Players.FirstOrDefault(p => p.TeamId == ahly.Id && p.ShirtNumber == 8)?.Id,
                FrameNumber = 250,
                TimestampMs = 10000,
                Minute = 0,
                Second = 10,
                Period = 1,
                Category = "pass",
                SubType = "short",
                Outcome = "successful",
                X = 50.0,
                Y = 50.0,
                EndX = 65.0,
                EndY = 45.0,
                Note = "تمريرة افتتاحية في خط الوسط"
            },
            new Event
            {
                MatchId = match.Id,
                TeamId = ahly.Id,
                PlayerId = db.Players.FirstOrDefault(p => p.TeamId == ahly.Id && p.ShirtNumber == 22)?.Id,
                FrameNumber = 400,
                TimestampMs = 16000,
                Minute = 0,
                Second = 16,
                Period = 1,
                Category = "carry",
                SubType = "run",
                Outcome = "successful",
                X = 65.0,
                Y = 45.0,
                EndX = 78.0,
                EndY = 40.0,
                Note = "تقدم بالكرة نحو الثلث الهجومي"
            },
            new Event
            {
                MatchId = match.Id,
                TeamId = ahly.Id,
                PlayerId = db.Players.FirstOrDefault(p => p.TeamId == ahly.Id && p.ShirtNumber == 9)?.Id,
                FrameNumber = 750,
                TimestampMs = 30000,
                Minute = 0,
                Second = 30,
                Period = 1,
                Category = "shot",
                SubType = "open_play",
                Outcome = "successful",
                X = 88.0,
                Y = 48.0,
                EndX = 100.0,
                EndY = 50.0,
                Note = "تسديدة خطيرة على المرمى",
                QualifiersJson = "[\"inside_box\",\"leads_to_shot\"]"
            }
        );
        db.SaveChanges();
    }
}
