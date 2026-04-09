using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using EliteDataCollector.UI.Views;
using Windows.Graphics;

namespace EliteDataCollector.UI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;

            // Set window size programmatically (not supported in WinUI 3 XAML)
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(1200, 800));

            // Navigate to dashboard by default
            ContentFrame.Navigate(typeof(DashboardPage));
        }

        private void OnNavigateDashboard(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(DashboardPage));
        }

        private void OnNavigateColonization(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(ColonizationPage));
        }

        private void OnNavigateBGS(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(BgsPage));
        }

        private void OnNavigatePowerplay(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(PowerplayPage));
        }

        private void OnNavigateContubernium(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(ContuberniumPage));
        }

        private void OnNavigateSettings(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }
    }
}
