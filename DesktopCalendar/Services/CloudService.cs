using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using DesktopCalendar.Models;
using System.Collections.Generic;
using System.Linq;

namespace DesktopCalendar.Services
{
    /// <summary>
    /// 云端同步服务 - 使用 Supabase REST API
    /// </summary>
    public class CloudService
    {
        private static CloudService? _instance;
        public static CloudService Instance => _instance ??= new CloudService();

        // Supabase 配置
        private const string SUPABASE_URL = "https://sshenbpgeqfqbknqgcms.supabase.co";
        private const string SUPABASE_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InNzaGVuYnBnZXFmcWJrbnFnY21zIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Njg1MzkwNzcsImV4cCI6MjA4NDExNTA3N30.f_BPiKT6ufT1Vcu6PYu74YOp3SGC84n5u7Sxy468NUc";

        // 自动同步配置
        private const int DEBOUNCE_DELAY_MS = 3000;      // 防抖延迟：3秒
        private const int AUTO_SYNC_INTERVAL_MS = 300000; // 定时同步：5分钟

        private readonly HttpClient _httpClient;
        private readonly string _sessionPath;
        private string? _accessToken;
        private string? _refreshToken;
        private string? _lastBackupPath;

        // 自动同步相关
        private CancellationTokenSource? _debounceCts;
        private System.Timers.Timer? _autoSyncTimer;
        private bool _isSyncing = false;
        private DateTime _lastSyncTime = DateTime.MinValue;

        public event EventHandler? AuthStateChanged;
        public event EventHandler<string>? SyncStatusChanged;

        public bool IsLoggedIn => CurrentUser != null;
        public UserInfo? CurrentUser { get; private set; }
        public string? UserEmail => CurrentUser?.Email;
        public bool IsSyncing => _isSyncing;
        public DateTime LastSyncTime => _lastSyncTime;

        private CloudService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "DesktopCalendar");
            Directory.CreateDirectory(appFolder);
            _sessionPath = Path.Combine(appFolder, "session.json");

            // 创建 HttpClientHandler 处理 SSL 证书问题
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("apikey", SUPABASE_KEY);
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // 设置超时时间
        }

        /// <summary>
        /// 初始化服务（恢复登录状态）
        /// </summary>
        public async Task InitializeAsync()
        {
            await RestoreSessionAsync();
        }

        /// <summary>
        /// 初始化并自动同步（应用启动时调用）
        /// </summary>
        public async Task InitializeWithSyncAsync()
        {
            await RestoreSessionAsync();
            
            // 如果已登录，启动时自动从云端拉取最新数据
            if (IsLoggedIn)
            {
                await StartupSyncAsync();
                StartAutoSyncTimer();
            }
        }

        /// <summary>
        /// 启动时同步 - 优先拉取云端数据
        /// </summary>
        private async Task StartupSyncAsync()
        {
            try
            {
                _isSyncing = true;
                SyncStatusChanged?.Invoke(this, "正在同步...");
                
                // 智能同步：合并本地和云端数据
                var (success, _, _) = await SmartSyncAsync();
                
                if (success)
                {
                    _lastSyncTime = DateTime.Now;
                    SyncStatusChanged?.Invoke(this, "同步完成");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Startup sync error: {ex.Message}");
                SyncStatusChanged?.Invoke(this, "同步失败");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        #region 自动同步机制

        /// <summary>
        /// 启动定时自动同步
        /// </summary>
        public void StartAutoSyncTimer()
        {
            StopAutoSyncTimer();
            
            _autoSyncTimer = new System.Timers.Timer(AUTO_SYNC_INTERVAL_MS);
            _autoSyncTimer.Elapsed += async (s, e) => await OnAutoSyncTimerElapsed();
            _autoSyncTimer.AutoReset = true;
            _autoSyncTimer.Start();
            
            System.Diagnostics.Debug.WriteLine("Auto sync timer started");
        }

        /// <summary>
        /// 停止定时自动同步
        /// </summary>
        public void StopAutoSyncTimer()
        {
            if (_autoSyncTimer != null)
            {
                _autoSyncTimer.Stop();
                _autoSyncTimer.Dispose();
                _autoSyncTimer = null;
            }
        }

        /// <summary>
        /// 定时同步触发
        /// </summary>
        private async Task OnAutoSyncTimerElapsed()
        {
            if (!IsLoggedIn || _isSyncing) return;
            
            try
            {
                _isSyncing = true;
                await UploadOnlyAsync();
                _lastSyncTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto sync error: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        /// <summary>
        /// 数据变更时调用 - 防抖上传
        /// </summary>
        public void NotifyDataChanged()
        {
            if (!IsLoggedIn) return;
            
            // 取消之前的延迟任务
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            
            var token = _debounceCts.Token;
            
            // 延迟执行上传
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DEBOUNCE_DELAY_MS, token);
                    
                    if (!token.IsCancellationRequested && IsLoggedIn && !_isSyncing)
                    {
                        _isSyncing = true;
                        SyncStatusChanged?.Invoke(this, "正在同步...");
                        
                        var (success, _) = await UploadOnlyAsync();
                        
                        if (success)
                        {
                            _lastSyncTime = DateTime.Now;
                            SyncStatusChanged?.Invoke(this, "已同步");
                        }
                        
                        _isSyncing = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    // 被取消，忽略
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Debounce upload error: {ex.Message}");
                    _isSyncing = false;
                }
            });
        }

        /// <summary>
        /// 应用退出时强制同步
        /// </summary>
        public async Task ForceSyncOnExitAsync()
        {
            if (!IsLoggedIn || _isSyncing) return;
            
            try
            {
                _debounceCts?.Cancel(); // 取消防抖
                StopAutoSyncTimer();    // 停止定时器
                
                _isSyncing = true;
                await UploadOnlyAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exit sync error: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        #endregion

        #region 认证方法

        /// <summary>
        /// 用户注册
        /// </summary>
        public async Task<(bool Success, string? Error)> SignUpAsync(string email, string password)
        {
            try
            {
                var payload = new { email, password };
                var json = JsonConvert.SerializeObject(payload);

                var response = await SendWithRetryAsync(async () =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return await _httpClient.PostAsync($"{SUPABASE_URL}/auth/v1/signup", content);
                });
                var responseBody = await response.Content.ReadAsStringAsync();

                // 200 或 201 都算成功
                if (response.IsSuccessStatusCode)
                {
                    // 注册成功（无论是否需要邮箱验证）
                    return (true, null);
                }

                // 422 状态码但包含用户信息也算成功（需要邮箱确认的情况）
                if ((int)response.StatusCode == 422 || (int)response.StatusCode == 400)
                {
                    // 检查是否是"需要确认邮箱"的情况
                    if (responseBody.Contains("confirmation") || responseBody.Contains("confirm"))
                    {
                        return (true, null);
                    }
                }

                var error = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                var errorMsg = error?.Message ?? error?.Msg ?? error?.ErrorDescription ?? "注册失败";
                return (false, ParseError(errorMsg));
            }
            catch (Exception ex)
            {
                return (false, $"网络错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 带重试的 HTTP 请求
        /// </summary>
        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpResponseMessage>> request, int maxRetries = 3)
        {
            Exception? lastException = null;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await request();
                }
                catch (Exception ex) when (ex is HttpRequestException || ex.Message.Contains("SSL"))
                {
                    lastException = ex;
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(1000 * (i + 1)); // 递增延迟重试
                    }
                }
            }
            throw lastException ?? new Exception("请求失败");
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        public async Task<(bool Success, string? Error)> SignInAsync(string email, string password)
        {
            try
            {
                var payload = new { email, password };
                var json = JsonConvert.SerializeObject(payload);

                var response = await SendWithRetryAsync(async () =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return await _httpClient.PostAsync(
                        $"{SUPABASE_URL}/auth/v1/token?grant_type=password", content);
                });
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<AuthResponse>(responseBody);
                    if (result?.User != null && result.AccessToken != null)
                    {
                        _accessToken = result.AccessToken;
                        _refreshToken = result.RefreshToken;
                        CurrentUser = new UserInfo
                        {
                            Id = result.User.Id,
                            Email = result.User.Email
                        };
                        await SaveSessionAsync();
                        AuthStateChanged?.Invoke(this, EventArgs.Empty);
                        return (true, null);
                    }
                }

                var error = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                return (false, ParseError(error?.Message ?? error?.Msg ?? "登录失败"));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 退出登录
        /// </summary>
        public async Task SignOutAsync()
        {
            // 停止自动同步
            StopAutoSyncTimer();
            _debounceCts?.Cancel();
            
            try
            {
                if (_accessToken != null)
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{SUPABASE_URL}/auth/v1/logout");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    await _httpClient.SendAsync(request);
                }
            }
            catch { }

            _accessToken = null;
            _refreshToken = null;
            CurrentUser = null;
            DeleteSession();
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        public async Task<(bool Success, string? Error)> ChangePasswordAsync(string newPassword)
        {
            if (!IsLoggedIn || _accessToken == null)
            {
                return (false, "请先登录");
            }

            try
            {
                var payload = new { password = newPassword };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Put, $"{SUPABASE_URL}/auth/v1/user");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var error = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                return (false, error?.Message ?? "修改密码失败");
            }
            catch (Exception ex)
            {
                return (false, $"网络错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 发送密码重置邮件
        /// </summary>
        public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email)
        {
            try
            {
                var payload = new { email };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{SUPABASE_URL}/auth/v1/recover", content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var error = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                return (false, error?.Message ?? "发送失败");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 发送密码重置验证码邮件（OTP方式）
        /// </summary>
        public async Task<(bool Success, string? Error)> SendPasswordResetEmailAsync(string email)
        {
            try
            {
                // 使用 recovery 类型发送 OTP
                var payload = new { email, type = "recovery" };
                var json = JsonConvert.SerializeObject(payload);

                var response = await SendWithRetryAsync(async () =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return await _httpClient.PostAsync($"{SUPABASE_URL}/auth/v1/otp", content);
                });

                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var error = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                return (false, ParseError(error?.Message ?? error?.Msg ?? "发送失败"));
            }
            catch (Exception ex)
            {
                return (false, ParseError(ex.Message));
            }
        }

        /// <summary>
        /// 验证OTP并更新密码
        /// </summary>
        public async Task<(bool Success, string? Error)> VerifyAndUpdatePasswordAsync(string email, string token, string newPassword)
        {
            try
            {
                // 1. 验证OTP并获取session
                var verifyPayload = new { email, token, type = "recovery" };
                var verifyJson = JsonConvert.SerializeObject(verifyPayload);

                var verifyResponse = await SendWithRetryAsync(async () =>
                {
                    var content = new StringContent(verifyJson, Encoding.UTF8, "application/json");
                    return await _httpClient.PostAsync($"{SUPABASE_URL}/auth/v1/verify", content);
                });

                if (!verifyResponse.IsSuccessStatusCode)
                {
                    var errorBody = await verifyResponse.Content.ReadAsStringAsync();
                    var error = JsonConvert.DeserializeObject<ErrorResponse>(errorBody);
                    var errorMsg = error?.Message ?? error?.Msg ?? "验证码错误";
                    if (errorMsg.Contains("Token has expired"))
                        return (false, "验证码已过期，请重新发送");
                    if (errorMsg.Contains("Invalid") || errorMsg.Contains("invalid"))
                        return (false, "验证码错误");
                    return (false, ParseError(errorMsg));
                }

                // 获取新的access_token
                var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
                var authResult = JsonConvert.DeserializeObject<AuthResponse>(verifyBody);
                
                if (authResult?.AccessToken == null)
                {
                    return (false, "验证失败，请重试");
                }

                // 2. 使用新token更新密码
                var updatePayload = new { password = newPassword };
                var updateJson = JsonConvert.SerializeObject(updatePayload);
                var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

                var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"{SUPABASE_URL}/auth/v1/user");
                updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
                updateRequest.Content = updateContent;

                var updateResponse = await _httpClient.SendAsync(updateRequest);

                if (updateResponse.IsSuccessStatusCode)
                {
                    // 更新本地session
                    _accessToken = authResult.AccessToken;
                    _refreshToken = authResult.RefreshToken;
                    if (authResult.User != null)
                    {
                        CurrentUser = new UserInfo
                        {
                            Id = authResult.User.Id,
                            Email = authResult.User.Email
                        };
                    }
                    await SaveSessionAsync();
                    return (true, null);
                }

                var updateErrorBody = await updateResponse.Content.ReadAsStringAsync();
                var updateError = JsonConvert.DeserializeObject<ErrorResponse>(updateErrorBody);
                return (false, updateError?.Message ?? "修改密码失败");
            }
            catch (Exception ex)
            {
                return (false, ParseError(ex.Message));
            }
        }

        /// <summary>
        /// 直接更新密码（已登录状态）
        /// </summary>
        public async Task<(bool Success, string? Error)> UpdatePasswordAsync(string newPassword)
        {
            return await ChangePasswordAsync(newPassword);
        }

        private string ParseError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "未知错误";
            
            // 登录相关
            if (message.Contains("Invalid login credentials"))
                return "邮箱或密码错误";
            if (message.Contains("Email not confirmed"))
                return "请先验证邮箱后再登录";
            
            // 注册相关
            if (message.Contains("User already registered"))
                return "该邮箱已注册，请直接登录";
            if (message.Contains("Password should be"))
                return "密码至少需要6个字符";
            if (message.Contains("Unable to validate email"))
                return "邮箱格式不正确";
            if (message.Contains("Signups not allowed"))
                return "注册功能已关闭，请联系管理员";
            if (message.Contains("Email rate limit exceeded"))
                return "发送邮件过于频繁，请稍后再试";
            if (message.Contains("over_email_send_rate_limit"))
                return "发送邮件过于频繁，请稍后再试";
            
            // 网络相关
            if (message.Contains("Failed to fetch") || message.Contains("Network"))
                return "网络连接失败，请检查网络";
            if (message.Contains("timeout"))
                return "连接超时，请稍后重试";
            if (message.Contains("SSL") || message.Contains("certificate"))
                return "网络连接不稳定，请重试";
            if (message.Contains("请求失败"))
                return "网络连接失败，请检查网络后重试";
            
            return message;
        }

        #endregion

        #region 会话管理

        private async Task SaveSessionAsync()
        {
            try
            {
                var session = new SessionData
                {
                    AccessToken = _accessToken,
                    RefreshToken = _refreshToken,
                    UserId = CurrentUser?.Id,
                    Email = CurrentUser?.Email
                };
                var json = JsonConvert.SerializeObject(session);
                await File.WriteAllTextAsync(_sessionPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save session error: {ex.Message}");
            }
        }

        private async Task RestoreSessionAsync()
        {
            try
            {
                if (File.Exists(_sessionPath))
                {
                    var json = await File.ReadAllTextAsync(_sessionPath);
                    var session = JsonConvert.DeserializeObject<SessionData>(json);
                    if (session != null && !string.IsNullOrEmpty(session.AccessToken))
                    {
                        _accessToken = session.AccessToken;
                        _refreshToken = session.RefreshToken;
                        
                        // 验证 token 是否有效
                        var isValid = await ValidateTokenAsync();
                        if (isValid)
                        {
                            CurrentUser = new UserInfo
                            {
                                Id = session.UserId ?? "",
                                Email = session.Email ?? ""
                            };
                            AuthStateChanged?.Invoke(this, EventArgs.Empty);
                        }
                        else
                        {
                            // Token 无效，尝试刷新
                            var refreshed = await RefreshTokenAsync();
                            if (!refreshed)
                            {
                                DeleteSession();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Restore session error: {ex.Message}");
                DeleteSession();
            }
        }

        private async Task<bool> ValidateTokenAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{SUPABASE_URL}/auth/v1/user");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> RefreshTokenAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_refreshToken)) return false;

                var payload = new { refresh_token = _refreshToken };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{SUPABASE_URL}/auth/v1/token?grant_type=refresh_token", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<AuthResponse>(responseBody);
                    if (result?.AccessToken != null)
                    {
                        _accessToken = result.AccessToken;
                        _refreshToken = result.RefreshToken;
                        if (result.User != null)
                        {
                            CurrentUser = new UserInfo
                            {
                                Id = result.User.Id,
                                Email = result.User.Email
                            };
                        }
                        await SaveSessionAsync();
                        AuthStateChanged?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void DeleteSession()
        {
            try
            {
                if (File.Exists(_sessionPath))
                {
                    File.Delete(_sessionPath);
                }
            }
            catch { }
        }

        #endregion

        #region 数据同步

        /// <summary>
        /// 同步数据到云端
        /// </summary>
        public async Task<(bool Success, string? Error)> SyncToCloudAsync()
        {
            if (!IsLoggedIn || _accessToken == null)
                return (false, "请先登录");

            try
            {
                SyncStatusChanged?.Invoke(this, "正在同步...");

                var userId = CurrentUser!.Id;
                var userData = new UserData
                {
                    UserId = userId,
                    Todos = DataService.Instance.Todos.ToList(),
                    Groups = DataService.Instance.Groups.ToList(),
                    Projects = DataService.Instance.Projects.ToList(),
                    Reviews = DataService.Instance.Reviews.ToList(),
                    Diaries = DataService.Instance.Diaries.ToList(),
                    UpdatedAt = DateTime.UtcNow
                };

                var dataJson = JsonConvert.SerializeObject(userData);
                var record = new
                {
                    user_id = userId,
                    data = dataJson,
                    updated_at = DateTime.UtcNow.ToString("o")
                };

                var json = JsonConvert.SerializeObject(record);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, 
                    $"{SUPABASE_URL}/rest/v1/user_data?on_conflict=user_id");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Headers.Add("Prefer", "resolution=merge-duplicates");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    SyncStatusChanged?.Invoke(this, "同步完成");
                    return (true, null);
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                SyncStatusChanged?.Invoke(this, "同步失败");
                return (false, $"同步失败: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                SyncStatusChanged?.Invoke(this, "同步失败");
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 智能同步 - 合并本地和云端数据（印象笔记策略）
        /// </summary>
        public async Task<(bool Success, string? Error, SyncResult? Result)> SmartSyncAsync()
        {
            if (!IsLoggedIn || _accessToken == null)
                return (false, "请先登录", null);

            try
            {
                // 1. 先备份本地数据
                SyncStatusChanged?.Invoke(this, "正在备份...");
                _lastBackupPath = DataService.Instance.CreateFullBackup();

                // 2. 获取云端数据
                SyncStatusChanged?.Invoke(this, "正在同步...");
                var cloudData = await FetchCloudDataAsync();

                // 3. 获取本地数据
                var localData = new UserData
                {
                    UserId = CurrentUser!.Id,
                    Todos = DataService.Instance.Todos.ToList(),
                    Groups = DataService.Instance.Groups.ToList(),
                    Projects = DataService.Instance.Projects.ToList(),
                    Reviews = DataService.Instance.Reviews.ToList(),
                    Diaries = DataService.Instance.Diaries.ToList(),
                    UpdatedAt = DateTime.UtcNow
                };

                // 4. 合并数据（本地优先 + 云端补充）
                var result = MergeData(localData, cloudData);

                // 5. 保存合并后的数据到本地
                ApplyMergedData(result.MergedData);

                // 6. 上传合并后的数据到云端
                await UploadDataAsync(result.MergedData);

                SyncStatusChanged?.Invoke(this, "同步完成");
                return (true, null, result);
            }
            catch (Exception ex)
            {
                SyncStatusChanged?.Invoke(this, "同步失败");
                return (false, ex.Message, null);
            }
        }

        /// <summary>
        /// 从云端获取数据
        /// </summary>
        private async Task<UserData?> FetchCloudDataAsync()
        {
            try
            {
                var userId = CurrentUser!.Id;
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"{SUPABASE_URL}/rest/v1/user_data?user_id=eq.{userId}&select=*");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var records = JsonConvert.DeserializeObject<List<UserDataRecord>>(responseBody);
                    if (records != null && records.Count > 0 && !string.IsNullOrEmpty(records[0].Data))
                    {
                        return JsonConvert.DeserializeObject<UserData>(records[0].Data);
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 上传数据到云端
        /// </summary>
        private async Task UploadDataAsync(UserData data)
        {
            var dataJson = JsonConvert.SerializeObject(data);
            var record = new
            {
                user_id = data.UserId,
                data = dataJson,
                updated_at = DateTime.UtcNow.ToString("o")
            };

            var json = JsonConvert.SerializeObject(record);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{SUPABASE_URL}/rest/v1/user_data?on_conflict=user_id");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("Prefer", "resolution=merge-duplicates");
            request.Content = content;

            await _httpClient.SendAsync(request);
        }

        /// <summary>
        /// 合并本地和云端数据（本地优先策略）
        /// </summary>
        private SyncResult MergeData(UserData local, UserData? cloud)
        {
            var result = new SyncResult();
            var merged = new UserData { UserId = local.UserId, UpdatedAt = DateTime.UtcNow };

            // 合并 Todos - 以 ID 为准，保留最新的
            var allTodos = new Dictionary<string, TodoItem>();
            
            // 先加入云端数据
            if (cloud?.Todos != null)
            {
                foreach (var todo in cloud.Todos)
                {
                    allTodos[todo.Id] = todo;
                    result.CloudOnlyItems++;
                }
            }
            
            // 本地数据覆盖（本地优先）
            foreach (var todo in local.Todos)
            {
                if (allTodos.ContainsKey(todo.Id))
                {
                    // 比较更新时间，保留最新的
                    var existing = allTodos[todo.Id];
                    if (todo.UpdatedAt > existing.UpdatedAt || todo.CompletedAt > existing.CompletedAt)
                    {
                        allTodos[todo.Id] = todo;
                    }
                    result.MergedItems++;
                }
                else
                {
                    allTodos[todo.Id] = todo;
                    result.LocalOnlyItems++;
                }
            }
            merged.Todos = allTodos.Values.ToList();

            // 合并 Groups
            var allGroups = new Dictionary<string, TodoGroup>();
            if (cloud?.Groups != null)
            {
                foreach (var g in cloud.Groups) allGroups[g.Id] = g;
            }
            foreach (var g in local.Groups)
            {
                allGroups[g.Id] = g; // 本地优先
            }
            merged.Groups = allGroups.Values.ToList();

            // 合并 Projects
            var allProjects = new Dictionary<string, Project>();
            if (cloud?.Projects != null)
            {
                foreach (var p in cloud.Projects) allProjects[p.Id] = p;
            }
            foreach (var p in local.Projects)
            {
                allProjects[p.Id] = p; // 本地优先
            }
            merged.Projects = allProjects.Values.ToList();

            // 合并 Reviews
            var allReviews = new Dictionary<string, ReviewNote>();
            if (cloud?.Reviews != null)
            {
                foreach (var r in cloud.Reviews) allReviews[r.Id] = r;
            }
            foreach (var r in local.Reviews)
            {
                allReviews[r.Id] = r; // 本地优先
            }
            merged.Reviews = allReviews.Values.ToList();

            // 合并 Diaries - 以 ID 为准，保留最新的
            var allDiaries = new Dictionary<string, DiaryEntry>();
            if (cloud?.Diaries != null)
            {
                foreach (var d in cloud.Diaries) allDiaries[d.Id] = d;
            }
            foreach (var d in local.Diaries)
            {
                if (allDiaries.ContainsKey(d.Id))
                {
                    // 比较更新时间，保留最新的
                    var existing = allDiaries[d.Id];
                    if (d.UpdatedAt > existing.UpdatedAt)
                    {
                        allDiaries[d.Id] = d;
                    }
                }
                else
                {
                    allDiaries[d.Id] = d;
                }
            }
            merged.Diaries = allDiaries.Values.ToList();

            result.MergedData = merged;
            return result;
        }

        /// <summary>
        /// 应用合并后的数据到本地（不触发云同步通知，避免死循环）
        /// </summary>
        private void ApplyMergedData(UserData data)
        {
            DataService.Instance.Todos.Clear();
            foreach (var todo in data.Todos)
                DataService.Instance.Todos.Add(todo);
            DataService.Instance.Save(notifyCloud: false);

            DataService.Instance.Groups.Clear();
            foreach (var group in data.Groups)
                DataService.Instance.Groups.Add(group);
            DataService.Instance.SaveGroups(notifyCloud: false);

            DataService.Instance.Projects.Clear();
            foreach (var project in data.Projects)
                DataService.Instance.Projects.Add(project);
            DataService.Instance.SaveProjects(notifyCloud: false);

            DataService.Instance.Reviews.Clear();
            foreach (var review in data.Reviews)
                DataService.Instance.Reviews.Add(review);
            DataService.Instance.SaveReviews(notifyCloud: false);

            DataService.Instance.Diaries.Clear();
            foreach (var diary in data.Diaries)
                DataService.Instance.Diaries.Add(diary);
            DataService.Instance.SaveDiaries(notifyCloud: false);
        }

        /// <summary>
        /// 仅上传本地数据到云端（不下载）
        /// </summary>
        public async Task<(bool Success, string? Error)> UploadOnlyAsync()
        {
            if (!IsLoggedIn || _accessToken == null)
                return (false, "请先登录");

            try
            {
                SyncStatusChanged?.Invoke(this, "正在上传...");
                
                var data = new UserData
                {
                    UserId = CurrentUser!.Id,
                    Todos = DataService.Instance.Todos.ToList(),
                    Groups = DataService.Instance.Groups.ToList(),
                    Projects = DataService.Instance.Projects.ToList(),
                    Reviews = DataService.Instance.Reviews.ToList(),
                    Diaries = DataService.Instance.Diaries.ToList(),
                    UpdatedAt = DateTime.UtcNow
                };

                await UploadDataAsync(data);
                
                SyncStatusChanged?.Invoke(this, "上传完成");
                return (true, null);
            }
            catch (Exception ex)
            {
                SyncStatusChanged?.Invoke(this, "上传失败");
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 仅从云端下载（覆盖本地，会先备份）
        /// </summary>
        public async Task<(bool Success, string? Error)> DownloadOnlyAsync()
        {
            if (!IsLoggedIn || _accessToken == null)
                return (false, "请先登录");

            try
            {
                SyncStatusChanged?.Invoke(this, "正在备份本地数据...");
                _lastBackupPath = DataService.Instance.CreateFullBackup();
                
                SyncStatusChanged?.Invoke(this, "正在下载...");
                var cloudData = await FetchCloudDataAsync();
                
                if (cloudData != null)
                {
                    ApplyMergedData(cloudData);
                    SyncStatusChanged?.Invoke(this, "下载完成");
                    return (true, null);
                }
                else
                {
                    SyncStatusChanged?.Invoke(this, "云端无数据");
                    return (false, "云端没有数据");
                }
            }
            catch (Exception ex)
            {
                SyncStatusChanged?.Invoke(this, "下载失败");
                return (false, ex.Message);
            }
        }

        // 保留旧方法兼容性
        public async Task<(bool Success, string? Error)> SyncFromCloudAsync() => await DownloadOnlyAsync();

        /// <summary>
        /// 撤销上次云端下载，恢复本地备份
        /// </summary>
        public bool RestoreLastBackup()
        {
            if (string.IsNullOrEmpty(_lastBackupPath))
                return false;

            return DataService.Instance.RestoreFromBackup(_lastBackupPath);
        }

        /// <summary>
        /// 获取是否有可恢复的备份
        /// </summary>
        public bool HasBackup => !string.IsNullOrEmpty(_lastBackupPath);

        /// <summary>
        /// 获取所有备份列表
        /// </summary>
        public List<string> GetBackupList() => DataService.Instance.GetBackupList();

        /// <summary>
        /// 从指定备份恢复
        /// </summary>
        public bool RestoreFromBackup(string backupPath) => DataService.Instance.RestoreFromBackup(backupPath);

        #endregion
    }

    #region 数据模型

    public class UserInfo
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class SessionData
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? UserId { get; set; }
        public string? Email { get; set; }
    }

    public class AuthResponse
    {
        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonProperty("user")]
        public AuthUser? User { get; set; }
    }

    public class AuthUser
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("email")]
        public string Email { get; set; } = "";
    }

    public class ErrorResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("msg")]
        public string? Msg { get; set; }

        [JsonProperty("error_description")]
        public string? ErrorDescription { get; set; }
    }

    public class UserData
    {
        public string UserId { get; set; } = "";
        public List<TodoItem> Todos { get; set; } = new();
        public List<TodoGroup> Groups { get; set; } = new();
        public List<Project> Projects { get; set; } = new();
        public List<ReviewNote> Reviews { get; set; } = new();
        public List<DiaryEntry> Diaries { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class UserDataRecord
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = "";

        [JsonProperty("data")]
        public string Data { get; set; } = "";

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 同步结果
    /// </summary>
    public class SyncResult
    {
        public UserData MergedData { get; set; } = new();
        public int LocalOnlyItems { get; set; }
        public int CloudOnlyItems { get; set; }
        public int MergedItems { get; set; }
        
        public string Summary => $"本地新增 {LocalOnlyItems} 项，云端新增 {CloudOnlyItems} 项，合并 {MergedItems} 项";
    }

    #endregion
}
