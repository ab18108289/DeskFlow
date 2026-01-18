using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using AutoUpdaterDotNET;

namespace DesktopCalendar
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateInfoEventArgs _updateInfo;

        public UpdateDialog(UpdateInfoEventArgs updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            
            // 设置版本信息
            VersionText.Text = $"v{updateInfo.CurrentVersion} 可用";
            CurrentVersionText.Text = $"当前版本: v{updateInfo.InstalledVersion}";
            
            // 允许拖动窗口
            MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AutoUpdater.DownloadUpdate(_updateInfo))
                {
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemindLater_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            // 跳过此版本 - 保存到注册表
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\DeskFlow");
                key?.SetValue("SkipVersion", _updateInfo.CurrentVersion.ToString());
                key?.Close();
            }
            catch { }
            
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}





