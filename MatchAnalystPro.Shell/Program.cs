using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MatchAnalystPro.Shell;

internal static class Program
{
    private static Process? _serverProcess;
    private const int Port = 5198;

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatchAnalystPro");
        Directory.CreateDirectory(appData);

        EnsureServerRunning();

        var form = new Form
        {
            Text = "Match Analyst Pro — منصة تحليل المباريات الاحترافية",
            WindowState = FormWindowState.Maximized,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(13, 17, 23),
            KeyPreview = true
        };

        var webView = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(webView);

        form.Shown += async (_, _) =>
        {
            await WaitForServerAsync(TimeSpan.FromSeconds(15));

            // استخدام مجلد ثابت لبيانات WebView2 لمنع تراكم المجلدات المؤقتة والحفاظ على الـ Cache
            var webViewDataDir = Path.Combine(appData, "WebView2Data");
            Directory.CreateDirectory(webViewDataDir);

            var env = await CoreWebView2Environment.CreateAsync(null, webViewDataDir);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Navigate($"http://localhost:{Port}");
        };

        form.FormClosed += (_, _) =>
        {
            try
            {
                if (_serverProcess is { HasExited: false })
                    _serverProcess.Kill(entireProcessTree: true);
            }
            catch { }
        };

        Application.Run(form);
    }

    private static void EnsureServerRunning()
    {
        if (IsServerListening()) return;

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            // 1. في نفس مجلد برنامج الديسكتوب (يعمل فوراً عند النشر للعملاء بدون الحاجة لـ .NET SDK)
            Path.Combine(baseDir, "MatchAnalystPro.Server.exe"),
            // 2. مسارات بيئة التطوير (Visual Studio)
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\MatchAnalystPro.Server\bin\Debug\net9.0\MatchAnalystPro.Server.exe")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\MatchAnalystPro.Server\bin\Release\net9.0\MatchAnalystPro.Server.exe"))
        };

        var serverExe = candidates.FirstOrDefault(File.Exists);
        if (serverExe != null)
        {
            _serverProcess = Process.Start(new ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = $"--urls http://localhost:{Port}",
                WorkingDirectory = Path.GetDirectoryName(serverExe)!,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return;
        }

        // خيار احتياطي لبيئة التطوير فقط
        var serverProjDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\MatchAnalystPro.Server"));
        if (Directory.Exists(serverProjDir))
        {
            _serverProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --urls http://localhost:{Port}",
                WorkingDirectory = serverProjDir,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
    }

    private static bool IsServerListening()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(400) };
            var resp = client.GetAsync($"http://localhost:{Port}").GetAwaiter().GetResult();
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WaitForServerAsync(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (IsServerListening()) return;
            await Task.Delay(200);
        }
    }
}
