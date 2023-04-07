using Microsoft.UI.Xaml;
using PInvoke;
using System;
using static Symbol_Mapper_Project.MainWindow;
using WinRT.Interop;
using System.Threading.Tasks;

namespace Symbol_Mapper_Project
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private static Window MainWindow;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();

            // Setup hidden Window
            #region Hidden_Window
            IntPtr hwnd = WindowNative.GetWindowHandle(MainWindow);

            User32.ShowWindow(hwnd, User32.WindowShowStyle.SW_HIDE);

            _ = RunWithDelay(() =>
            {
                User32.ShowWindow(hwnd, User32.WindowShowStyle.SW_HIDE);
            }, 1);

            int exStyle = User32.GetWindowLong(hwnd, User32.WindowLongIndexFlags.GWL_EXSTYLE);
            exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            User32.SetWindowLongPtr(hwnd, User32.WindowLongIndexFlags.GWL_EXSTYLE, (IntPtr) exStyle);
            #endregion

            MainWindow.Activate();
        }

        static async Task RunWithDelay(Action callback, int delay_ms)
        {
            await Task.Delay(delay_ms);
            callback();
        }
    }
}
