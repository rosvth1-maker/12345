using System.Windows;
using TsmClient.Data;

namespace TsmClient.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            string path = CrashReportService.Write(args.Exception, System.IO.Path.Combine(AppContext.BaseDirectory, "crash-reports"));
            MessageBox.Show($"ตัวเกมพบข้อผิดพลาดและสร้างรายงานที่:\n{path}\n\nรายงานไม่บันทึกบัญชีหรือรหัสผ่าน", "TS Dark World", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) CrashReportService.Write(ex, System.IO.Path.Combine(AppContext.BaseDirectory, "crash-reports"));
        };
        base.OnStartup(e);
    }
}
