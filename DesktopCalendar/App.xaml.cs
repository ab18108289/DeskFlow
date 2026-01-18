using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using DesktopCalendar.Services;
using AutoUpdaterDotNET;

namespace DesktopCalendar
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private DesktopWidget? _desktopWidget;
        private MainWindow? _mainWindow;
        private HwndSource? _hwndSource;
        
        // 单实例互斥锁
        private static Mutex? _mutex;
        
        // 全局主题颜色（默认深色主题）
        public static string ThemeStartColor { get; set; } = "#0D0D12";
        public static string ThemeEndColor { get; set; } = "#1A1A24";
        public static string ThemeAccentColor { get; set; } = "#6B7B8A";

        // Win32 API 用于全局热键
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const uint VK_OEM_3 = 0xC0; // ` 键 (反引号/波浪号键)

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 检查是否已有实例在运行
            const string mutexName = "DeskFlow_SingleInstance_Mutex";
            _mutex = new Mutex(true, mutexName, out bool createdNew);
            
            if (!createdNew)
            {
                // 已有实例在运行，显示提示并退出
                MessageBox.Show("DeskFlow 已在运行中！\n\n请查看任务栏右下角的托盘图标。", 
                    "DeskFlow", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            
            base.OnStartup(e);
            
            // 初始化云服务
            await CloudService.Instance.InitializeAsync();
            
            // 检查更新（从 GitHub 获取 update.xml）- 使用自定义界面
            AutoUpdater.CheckForUpdateEvent += OnCheckForUpdate;
            AutoUpdater.Start("https://raw.githubusercontent.com/ab18108289/DeskFlow/main/update.xml");
            
            _ = DataService.Instance;

            _desktopWidget = new DesktopWidget();
            _desktopWidget.Show();

            CreateTrayIcon();
            RegisterGlobalHotKey();
        }

        private void RegisterGlobalHotKey()
        {
            // 创建一个隐藏窗口来接收热键消息
            var helper = new WindowInteropHelper(new Window());
            helper.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);

            // 注册热键: ` 键 (无修饰键)
            RegisterHotKey(_hwndSource!.Handle, HOTKEY_ID, 0, VK_OEM_3);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleDesktopWidget();
                handled = true;
            }
            
            return IntPtr.Zero;
        }

        private void CreateTrayIcon()
        {
            _notifyIcon = new TaskbarIcon
            {
                Icon = CreateCalendarIcon(),
                ToolTipText = "DeskFlow - 双击打开主界面",
                Visibility = Visibility.Visible
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var openMainItem = new System.Windows.Controls.MenuItem { Header = "📋 打开主界面" };
            openMainItem.Click += (s, e) => ShowMainWindow();

            var toggleWidgetItem = new System.Windows.Controls.MenuItem { Header = "📅 显示/隐藏桌面日历 (`)" };
            toggleWidgetItem.Click += (s, e) => ToggleDesktopWidget();

            var separatorItem = new System.Windows.Controls.Separator();

            var exitItem = new System.Windows.Controls.MenuItem { Header = "❌ 退出" };
            exitItem.Click += (s, e) => ExitApplication();

            contextMenu.Items.Add(openMainItem);
            contextMenu.Items.Add(toggleWidgetItem);
            contextMenu.Items.Add(separatorItem);
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.TrayMouseDoubleClick += (s, e) => ShowMainWindow();
        }

        // 动态生成 DeskFlow 品牌图标（紫蓝渐变 + D字母）
        private System.Drawing.Icon CreateCalendarIcon()
        {
            int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            
            // 抗锯齿
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            
            // 紫蓝渐变背景
            using var gradientBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, size, size),
                System.Drawing.Color.FromArgb(139, 92, 246),  // 紫色 #8B5CF6
                System.Drawing.Color.FromArgb(59, 130, 246),  // 蓝色 #3B82F6
                System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal);
            
            // 圆角矩形背景
            var rect = new System.Drawing.Rectangle(2, 2, size - 4, size - 4);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int radius = 6;
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            g.FillPath(gradientBrush, path);
            
            // D 字母
            using var font = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
            using var textBrush = new SolidBrush(System.Drawing.Color.White);
            
            var textSize = g.MeasureString("D", font);
            float x = (size - textSize.Width) / 2 + 1;
            float y = (size - textSize.Height) / 2;
            g.DrawString("D", font, textBrush, x, y);

            // 转换为Icon
            IntPtr hIcon = bitmap.GetHicon();
            return System.Drawing.Icon.FromHandle(hIcon);
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Closed += (s, e) => _mainWindow = null;
            }
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.WindowState = WindowState.Normal;
        }

        private void ToggleDesktopWidget()
        {
            if (_desktopWidget != null)
            {
                if (_desktopWidget.IsVisible)
                    _desktopWidget.Hide();
                else
                    _desktopWidget.Show();
            }
        }

        private void ExitApplication()
        {
            _notifyIcon?.Dispose();
            _desktopWidget?.Close();
            _mainWindow?.Close();
            Shutdown();
        }

        private void OnCheckForUpdate(UpdateInfoEventArgs args)
        {
            if (args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    // 检查是否跳过了此版本
                    try
                    {
                        var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\DeskFlow");
                        var skipVersion = key?.GetValue("SkipVersion")?.ToString();
                        key?.Close();
                        
                        if (skipVersion == args.CurrentVersion.ToString())
                        {
                            return; // 用户选择跳过此版本
                        }
                    }
                    catch { }
                    
                    // 显示自定义更新对话框
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new UpdateDialog(args);
                        dialog.ShowDialog();
                    });
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 取消注册热键
            if (_hwndSource != null)
            {
                UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
            }
            
            DataService.Instance.Save();
            _notifyIcon?.Dispose();
            
            // 释放互斥锁
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            
            base.OnExit(e);
        }
    }
}
