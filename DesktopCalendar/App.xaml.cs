using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using DesktopCalendar.Services;

namespace DesktopCalendar
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private DesktopWidget? _desktopWidget;
        private MainWindow? _mainWindow;
        private HwndSource? _hwndSource;

        // Win32 API 用于全局热键
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const uint VK_OEM_3 = 0xC0; // ` 键 (反引号/波浪号键)

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
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
                ToolTipText = "桌面日历 - 双击打开主界面",
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

        // 动态生成一个日历图标
        private System.Drawing.Icon CreateCalendarIcon()
        {
            int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            
            // 抗锯齿
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            
            // 背景圆角矩形 - 蓝色
            using var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(59, 130, 246));
            g.FillRectangle(bgBrush, 2, 2, size - 4, size - 4);
            
            // 顶部红色条
            using var topBrush = new SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68));
            g.FillRectangle(topBrush, 2, 2, size - 4, 8);
            
            // 日期数字（当前日期）
            string dayText = DateTime.Now.Day.ToString();
            using var font = new Font("Arial", 14, System.Drawing.FontStyle.Bold);
            using var textBrush = new SolidBrush(System.Drawing.Color.White);
            
            var textSize = g.MeasureString(dayText, font);
            float x = (size - textSize.Width) / 2;
            float y = 10 + (size - 10 - textSize.Height) / 2;
            g.DrawString(dayText, font, textBrush, x, y);

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
            base.OnExit(e);
        }
    }
}
