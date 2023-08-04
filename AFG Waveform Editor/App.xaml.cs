using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Windows;

namespace AFG_Waveform_Editor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider? Services { get; set; }

        //ViewModel不需要宣告成 static, 用APP.Current.ViewMode存取
        //如果找不到物件時，會直接回傳 null 空值
        public ViewModel? viewModel;
        //public MainViewModel? ViewModel => Services?.GetService<MainViewModel>(); // Get MainViewModel Service
        //public ILogger? logger = Services?.GetService<ILogger<App>>(); // Get logger service
        public static MainWindow? mainWindow { get; set; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            var ttt = services.AddSingleton<ViewModel>(); // 整個 Process 只建立一個 Instance，任何時候都共用它, 要整個 Process 共用一份的服務可註冊成 Singleton
            return services.BuildServiceProvider();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            /* 避免重複開視窗 */
            var mutex = new Mutex(true, "AFG31000", out bool isNewInstance);
            if (!isNewInstance)
            {
                //Log.Warning("Application instance is already running!");
                Win32Commands.ActivateWindow("AFG31000");
                Shutdown();
                return;

            }

            mainWindow = new MainWindow(); // 先定義window, 後定義viewmodel, 才能順利存取viewmodel內的window instance
            viewModel = Services?.GetService<ViewModel>(); // Get MainViewModel
            mainWindow.DataContext = viewModel;
            viewModel!.window = mainWindow;
            this.MainWindow = mainWindow;
            this.MainWindow!.Top = 0;
            this.MainWindow.Left = 0;
            this.MainWindow.Show();

            base.OnStartup(e);
        }
    }
}
