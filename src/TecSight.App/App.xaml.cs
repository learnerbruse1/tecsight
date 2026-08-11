using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace TecSight.App;

public partial class App : Application
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TecSight", "logs");

    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例：已在运行则提示并退出
        _singleInstance = new Mutex(true, "TecSight.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("TecSight 已在运行。", "TecSight", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 全局异常捕获：记录日志，UI 线程异常不静默崩溃
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            MessageBox.Show("发生未处理的错误：\n" + args.Exception.Message + "\n\n详情已写入日志。", "TecSight",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
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