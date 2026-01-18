using System.Windows;
using System.Windows.Input;
using DesktopCalendar.Services;

namespace DesktopCalendar
{
    public partial class LoginWindow : Window
    {
        public bool IsLoggedIn { get; private set; }
        public bool SkippedLogin { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SwitchToRegister_MouseDown(object sender, MouseButtonEventArgs e)
        {
            LoginForm.Visibility = Visibility.Collapsed;
            RegisterForm.Visibility = Visibility.Visible;
            ClearErrors();
        }

        private void SwitchToLogin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            RegisterForm.Visibility = Visibility.Collapsed;
            LoginForm.Visibility = Visibility.Visible;
            ClearErrors();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailInput.Text.Trim();
            var password = PasswordInput.Password;

            if (string.IsNullOrEmpty(email))
            {
                ShowError("请输入邮箱地址");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("请输入密码");
                return;
            }

            ShowLoading("登录中...");

            var (success, error) = await CloudService.Instance.SignInAsync(email, password);

            HideLoading();

            if (success)
            {
                IsLoggedIn = true;
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError(error ?? "登录失败");
            }
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var email = RegEmailInput.Text.Trim();
            var password = RegPasswordInput.Password;
            var confirmPassword = RegConfirmPasswordInput.Password;

            if (string.IsNullOrEmpty(email))
            {
                ShowRegError("请输入邮箱地址");
                return;
            }

            if (!email.Contains("@"))
            {
                ShowRegError("请输入有效的邮箱地址");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowRegError("请设置密码");
                return;
            }

            if (password.Length < 6)
            {
                ShowRegError("密码至少需要6个字符");
                return;
            }

            if (password != confirmPassword)
            {
                ShowRegError("两次密码不一致");
                return;
            }

            ShowLoading("注册中...");

            var (success, error) = await CloudService.Instance.SignUpAsync(email, password);

            HideLoading();

            if (success)
            {
                MessageBox.Show("注册成功！请检查邮箱点击验证链接。", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                SwitchToLogin_MouseDown(sender, null!);
                EmailInput.Text = email;
            }
            else
            {
                ShowRegError(error ?? "注册失败");
            }
        }

        private async void ForgotPassword_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var email = EmailInput.Text.Trim();
            if (string.IsNullOrEmpty(email))
            {
                ShowError("请先输入邮箱地址");
                return;
            }

            ShowLoading("发送中...");

            var (success, error) = await CloudService.Instance.ResetPasswordAsync(email);

            HideLoading();

            if (success)
            {
                MessageBox.Show("重置邮件已发送，请检查邮箱。", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ShowError(error ?? "发送失败");
            }
        }

        private void SkipLogin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SkippedLogin = true;
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        private void ShowRegError(string message)
        {
            RegErrorText.Text = message;
            RegErrorBorder.Visibility = Visibility.Visible;
        }

        private void ClearErrors()
        {
            ErrorBorder.Visibility = Visibility.Collapsed;
            RegErrorBorder.Visibility = Visibility.Collapsed;
        }

        private void ShowLoading(string text)
        {
            LoadingText.Text = text;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoginButton.IsEnabled = false;
            RegisterButton.IsEnabled = false;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoginButton.IsEnabled = true;
            RegisterButton.IsEnabled = true;
        }
    }
}
