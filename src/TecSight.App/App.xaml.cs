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
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}\n\n";
            File.AppendAllText(Path.Combine(LogDir, "error.log"), line);
        }
        catch
        {
            // 日志失败不影响运行
        }
    }
}