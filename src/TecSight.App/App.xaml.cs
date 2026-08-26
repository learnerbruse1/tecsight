using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace TecSight.App;

public partial class App : Application
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TecSight", "logs");

    private static Mutex? _singleInstance;
    private bool _errorDialogShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        var isElevatedRestart = e.Args.Any(a => string.Equals(a, "--restart-as-admin", StringComparison.OrdinalIgnoreCase));

        // 单实例：已在运行则提示并退出
        _singleInstance = new Mutex(true, "TecSight.SingleInstance", out var createdNew);
        if (!createdNew && isElevatedRestart)
        {
            // 提权重启：旧实例正在退出，轮询等待其释放互斥锁（最多 ~10 秒），避免误判"已在运行"导致重启失败
            for (var i = 0; i < 100 && !createdNew; i++)
            {
                Thread.Sleep(100);
                _singleInstance.Dispose();
                _singleInstance = new Mutex(true, "TecSight.SingleInstance", out createdNew);
            }
        }

        if (!createdNew)
        {
            MessageBox.Show(Localization.LocalizationManager.Instance["Common.AlreadyRunning"], "TecSight", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 全局异常捕获：记录日志，UI 线程异常不静默崩溃
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
            // 同一个会话内只弹一次框，避免同一错误在每次点击时反复打断用户；每次异常仍会写入日志。
            if (_errorDialogShown) return;
            _errorDialogShown = true;
            var loc = Localization.LocalizationManager.Instance;
            MessageBox.Show(loc["Common.UnhandledError"] + "\n" + args.Exception.Message + "\n\n" + loc["Common.ErrorLogged"], "TecSight",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogError(args.ExceptionObject as Exception);

        base.OnStartup(e);
    }

    public static void LogError(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var path = Path.Combine(LogDir, "error.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}\n\n";
            File.AppendAllText(path, line);
            // 防止日志无限增长：超过 256KB 时截断为最近一半
            var fi = new FileInfo(path);
            if (fi.Length > 256 * 1024)
            {
                var keep = fi.Length - 128 * 1024;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
                fs.Seek(keep, SeekOrigin.Begin);
                var buf = new byte[fi.Length - keep];
                _ = fs.Read(buf, 0, buf.Length);
                fs.SetLength(0);
                fs.Seek(0, SeekOrigin.Begin);
                fs.Write(buf, 0, buf.Length);
            }
        }
        catch
        {
            // 日志失败不影响运行
        }
    }
}
