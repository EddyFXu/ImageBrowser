using System;
using System.Windows;
using System.IO;

namespace ImageBrowser
{
    public class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                App app = new App();
                if (app.Resources == null) app.Resources = new ResourceDictionary();

                string initialPath = null;
                if (args != null && args.Length > 0)
                {
                    string path = args[0];
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) initialPath = path;
                }

                MainWindow win = new MainWindow(initialPath);
                win.Show();
                app.Run();
            }
            catch (Exception ex)
            {
                try 
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImageBrowser", "error.log");
                    string dir = Path.GetDirectoryName(logPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(logPath, ex.ToString());
                } catch {}
                
                MessageBox.Show("程序启动发生错误：\n" + ex.ToString(), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
