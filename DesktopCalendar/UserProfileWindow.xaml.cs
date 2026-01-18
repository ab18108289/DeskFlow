using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopCalendar.Services;

namespace DesktopCalendar
{
    public partial class UserProfileWindow : Window
    {
        public bool NeedRefresh { get; private set; } = false;
        private string _userEmail = "";
        private Color _themeColor;
        
        public UserProfileWindow()
        {
            InitializeComponent();
            ApplyTheme();
            LoadUserInfo();
            Loaded += async (s, e) => await AutoSync();
        }
        
        private void ApplyTheme()
        {
            // 获取全局主题颜色，稍微提亮
            var startColor = (Color)ColorConverter.ConvertFromString(App.ThemeStartColor);
            var endColor = (Color)ColorConverter.ConvertFromString(App.ThemeEndColor);
            
            // 提亮背景色
            startColor = Color.FromRgb(
                (byte)Math.Min(255, startColor.R + 25),
                (byte)Math.Min(255, startColor.G + 25),
                (byte)Math.Min(255, startColor.B + 35));
            endColor = Color.FromRgb(
                (byte)Math.Min(255, endColor.R + 20),
                (byte)Math.Min(255, endColor.G + 20),
                (byte)Math.Min(255, endColor.B + 30));
            
            // 根据主题确定强调色
            _themeColor = App.ThemeStartColor switch
            {
                "#0F172A" => Color.FromRgb(0x3B, 0x82, 0xF6), // 蓝色主题
                "#1A0F2E" => Color.FromRgb(0x8B, 0x5C, 0xF6), // 紫色主题
                "#0F1A0F" => Color.FromRgb(0x22, 0xC5, 0x5E), // 绿色主题
                _ => Color.FromRgb(0x8B, 0x8B, 0x9A)           // 深色主题 - 更亮
            };
            
            // 应用背景渐变
            var bgBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            bgBrush.GradientStops.Add(new GradientStop(startColor, 0));
            bgBrush.GradientStops.Add(new GradientStop(endColor, 1));
            MainBorder.Background = bgBrush;
            
            // 头像渐变
            var avatarBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            avatarBrush.GradientStops.Add(new GradientStop(_themeColor, 0));
            avatarBrush.GradientStops.Add(new GradientStop(
                Color.FromRgb(
                    (byte)Math.Min(255, _themeColor.R + 60),
                    (byte)Math.Min(255, _themeColor.G + 40),
                    (byte)Math.Min(255, _themeColor.B + 80)), 1));
            AvatarBorder.Background = avatarBrush;
            
            // 图标颜色
            Icon1.Fill = new SolidColorBrush(_themeColor);
            Icon1Bg.Background = new SolidColorBrush(_themeColor) { Opacity = 0.15 };
            
            // 光晕颜色
            Glow1.Color = Color.FromArgb(32, _themeColor.R, _themeColor.G, _themeColor.B);
            Glow2.Color = Color.FromArgb(21, 
                (byte)Math.Min(255, _themeColor.R + 40),
                (byte)Math.Min(255, _themeColor.G + 20),
                (byte)Math.Min(255, _themeColor.B + 60));
            
            // 按钮渐变
            var btnBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            btnBrush.GradientStops.Add(new GradientStop(_themeColor, 0));
            btnBrush.GradientStops.Add(new GradientStop(
                Color.FromRgb(
                    (byte)Math.Min(255, _themeColor.R + 40),
                    (byte)Math.Min(255, _themeColor.G + 30),
                    (byte)Math.Min(255, _themeColor.B + 50)), 1));
            
            SendCodeBtn.Tag = btnBrush;
            ConfirmBtn.Tag = btnBrush;
        }
        
        private void LoadUserInfo()
        {
            _userEmail = CloudService.Instance.UserEmail ?? "";
            UserEmail.Text = _userEmail;
            EmailDisplay.Text = _userEmail;
            if (!string.IsNullOrEmpty(_userEmail))
                UserAvatar.Text = _userEmail[0].ToString().ToUpper();
        }
        
        private async System.Threading.Tasks.Task AutoSync()
        {
            var (success, _, _) = await CloudService.Instance.SmartSyncAsync();
            if (success) NeedRefresh = true;
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        
        private void ChangePassword_Click(object sender, MouseButtonEventArgs e)
        {
            MainPanel.Visibility = Visibility.Collapsed;
            PasswordStep1.Visibility = Visibility.Visible;
            Step1Status.Text = "";
        }
        
        private void BackToMain_Click(object sender, MouseButtonEventArgs e)
        {
            PasswordStep1.Visibility = Visibility.Collapsed;
            PasswordStep2.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            this.Height = 420;
        }
        
        private void BackToStep1_Click(object sender, MouseButtonEventArgs e)
        {
            PasswordStep2.Visibility = Visibility.Collapsed;
            PasswordStep1.Visibility = Visibility.Visible;
            this.Height = 420;
        }
        
        private async void SendCode_Click(object sender, RoutedEventArgs e)
        {
            SendCodeBtn.IsEnabled = false;
            SendCodeBtn.Content = "发送中...";
            Step1Status.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
            Step1Status.Text = "";
            
            try
            {
                var (success, message) = await CloudService.Instance.SendPasswordResetEmailAsync(_userEmail);
                
                if (success)
                {
                    Step1Status.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                    Step1Status.Text = "✓ 验证码已发送，请查看邮箱";
                    
                    await System.Threading.Tasks.Task.Delay(800);
                    PasswordStep1.Visibility = Visibility.Collapsed;
                    PasswordStep2.Visibility = Visibility.Visible;
                    this.Height = 500;
                    VerifyCodeBox.Text = "";
                    NewPasswordBox.Password = "";
                    ConfirmPasswordBox.Password = "";
                    Step2Status.Text = "";
                }
                else
                {
                    Step1Status.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    Step1Status.Text = message;
                }
            }
            catch (Exception ex)
            {
                Step1Status.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                Step1Status.Text = ex.Message;
            }
            finally
            {
                SendCodeBtn.IsEnabled = true;
                SendCodeBtn.Content = "发送验证码";
            }
        }
        
        private async void ConfirmPassword_Click(object sender, RoutedEventArgs e)
        {
            var code = VerifyCodeBox.Text?.Trim();
            var newPwd = NewPasswordBox.Password;
            var confirmPwd = ConfirmPasswordBox.Password;
            
            Step2Status.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            
            if (string.IsNullOrEmpty(code) || code.Length < 6)
            {
                Step2Status.Text = "请输入6位验证码";
                return;
            }
            if (string.IsNullOrWhiteSpace(newPwd))
            {
                Step2Status.Text = "请输入新密码";
                return;
            }
            if (newPwd.Length < 6)
            {
                Step2Status.Text = "密码至少6位";
                return;
            }
            if (newPwd != confirmPwd)
            {
                Step2Status.Text = "两次密码不一致";
                return;
            }
            
            ConfirmBtn.IsEnabled = false;
            ConfirmBtn.Content = "验证中...";
            
            try
            {
                var (success, message) = await CloudService.Instance.VerifyAndUpdatePasswordAsync(_userEmail, code, newPwd);
                
                if (success)
                {
                    Step2Status.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                    Step2Status.Text = "✓ 密码修改成功";
                    await System.Threading.Tasks.Task.Delay(1200);
                    BackToMain_Click(null, null);
                }
                else
                {
                    Step2Status.Text = message;
                }
            }
            catch (Exception ex)
            {
                Step2Status.Text = ex.Message;
            }
            finally
            {
                ConfirmBtn.IsEnabled = true;
                ConfirmBtn.Content = "确认修改";
            }
        }
        
        private void Backup_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var path = DataService.Instance.CreateFullBackup();
                MessageBox.Show($"备份成功！\n\n文件位置：\n{path}", "备份完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"备份失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void Logout_Click(object sender, MouseButtonEventArgs e)
        {
            if (MessageBox.Show("确定退出登录？\n\n本地数据将保留。", "退出登录", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await CloudService.Instance.SignOutAsync();
                DialogResult = true;
                Close();
            }
        }
    }
}
