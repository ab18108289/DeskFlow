using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopCalendar.Models;
using DesktopCalendar.Services;

namespace DesktopCalendar
{
    public partial class MainWindow : Window
    {
        private string _currentFilter = "Today";
        private string? _currentGroupId; // 当前选中的分组ID
        private Priority _selectedPriority = Priority.Low;
        private Priority _editPriority = Priority.Low;
        private string? _editingTodoId;
        
        // 分组弹窗选择状态
        private string _newGroupIcon = "📁";
        private string _newGroupColor = "#6366F1";
        
        // 导航按钮引用
        private Button? _activeNavButton;
        
        public ObservableCollection<TodoItem> FilteredTodos { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            FilteredTodos = new ObservableCollection<TodoItem>();
            DataContext = this;
            
            DataService.Instance.Todos.CollectionChanged += (s, e) => Dispatcher.Invoke(RefreshAll);
            
            // 监听习惯数据变化（与桌面小部件同步）
            DataService.Instance.HabitsChanged += (s, e) => Dispatcher.Invoke(() => 
            {
                // 习惯打卡功能已移除
            });
            
            // 监听分组数据变化
            DataService.Instance.GroupsChanged += (s, e) => Dispatcher.Invoke(RefreshGroupNav);
            
            _activeNavButton = NavToday;
            RefreshAll();
            RefreshGroupNav();
            RefreshGroupCombo();
            RefreshProjectNavList();
            UpdateViewTitle();
            UpdatePriorityButtons();
            InitializeCloudSync();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Hide();

        #region 主题切换

        private void Theme_Dark(object sender, MouseButtonEventArgs e)
        {
            SetTheme("#0D0D12", "#1A1A24");
            UpdateThemeBorders(sender);
        }

        private void Theme_Blue(object sender, MouseButtonEventArgs e)
        {
            SetTheme("#0F172A", "#1E3A5F");
            UpdateThemeBorders(sender);
        }

        private void Theme_Purple(object sender, MouseButtonEventArgs e)
        {
            SetTheme("#1A0F2E", "#2E1E3F");
            UpdateThemeBorders(sender);
        }

        private void Theme_Green(object sender, MouseButtonEventArgs e)
        {
            SetTheme("#0F1A0F", "#1E2E1E");
            UpdateThemeBorders(sender);
        }

        private void SetTheme(string startColor, string endColor)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(startColor), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(endColor), 1));
            MainBorder.Background = brush;
            
            // 同时更新其他面板的背景色
            var panelBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(startColor));
            ReviewPanel.Background = panelBrush;
            ProjectDetailPanel.Background = panelBrush;
            ProjectPanel.Background = panelBrush;
            
            // 更新全局主题颜色
            App.ThemeStartColor = startColor;
            App.ThemeEndColor = endColor;
        }

        private void UpdateThemeBorders(object sender)
        {
            // 清除所有主题按钮的边框
            var parent = (sender as Border)?.Parent as StackPanel;
            if (parent != null)
            {
                foreach (Border child in parent.Children.OfType<Border>())
                {
                    child.BorderBrush = Brushes.Transparent;
                }
            }
            // 设置当前选中的边框
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }
        }

        #endregion

        #region 导航筛选

        private void QuickNav_Today(object sender, MouseButtonEventArgs e)
        {
            _currentFilter = "Today";
            SetActiveNavButton(NavToday);
            RefreshAll();
        }

        private void QuickNav_Urgent(object sender, MouseButtonEventArgs e)
        {
            _currentFilter = "Urgent";
            SetActiveNavButton(null); // 紧急任务没有对应的侧边栏按钮
            RefreshAll();
        }

        private void NavFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filter)
            {
                // 隐藏所有其他面板
                ReviewPanel.Visibility = Visibility.Collapsed;
                // HabitPanel已移除
                ProjectPanel.Visibility = Visibility.Collapsed;
                ProjectDetailPanel.Visibility = Visibility.Collapsed;
                DiaryPanel.Visibility = Visibility.Collapsed;
                
                _currentFilter = filter;
                SetActiveNavButton(btn);
                RefreshAll();
            }
        }

        private void SetActiveNavButton(Button? newActive)
        {
            // 找到 NavButton 和 NavButtonActive 样式
            var navStyle = (Style)FindResource("NavButton");
            var navActiveStyle = (Style)FindResource("NavButtonActive");

            // 重置之前的按钮
            if (_activeNavButton != null)
            {
                _activeNavButton.Style = navStyle;
            }

            // 设置新的活动按钮
            _activeNavButton = newActive;
            if (_activeNavButton != null)
            {
                _activeNavButton.Style = navActiveStyle;
            }
        }

        private void UpdateViewTitle()
        {
            var today = DateTime.Today;
            string[] days = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            ViewSubtitle.Text = $"{today:yyyy年M月d日} {days[(int)today.DayOfWeek]}";

            ViewTitle.Text = _currentFilter switch
            {
                "Today" => "今天",
                "Week" => "最近7天",
                "All" => "全部待办",
                "Completed" => "已完成",
                "Urgent" => "紧急任务",
                "Overdue" => "已逾期/顺延",
                "Group" => _currentGroupId != null ? DataService.Instance.GetGroupName(_currentGroupId) : "分组",
                _ => "待办清单"
            };
        }

        private void UpdateNavCounts()
        {
            var todos = DataService.Instance.Todos;
            var today = DateTime.Today;

            // 今日
            var todayCount = todos.Count(t => (t.DueDate?.Date == today || t.DueDate == null) && !t.IsCompleted);
            NavTodayCount.Text = todayCount > 0 ? todayCount.ToString() : "";
            TodayCardCount.Text = todayCount.ToString();

            // 最近7天（过去7天到今天）
            var weekCount = todos.Count(t => t.DueDate?.Date >= today.AddDays(-7) && t.DueDate?.Date <= today && !t.IsCompleted);
            NavWeekCount.Text = weekCount > 0 ? weekCount.ToString() : "";

            // 全部（包含所有任务）
            var allCount = todos.Count;
            NavAllCount.Text = allCount > 0 ? allCount.ToString() : "";

            // 逾期
            var overdueCount = todos.Count(t => t.DueDate?.Date < today && !t.IsCompleted);
            NavOverdueCount.Text = overdueCount > 0 ? overdueCount.ToString() : "";

            // 已完成
            var completedCount = todos.Count(t => t.IsCompleted);
            NavCompletedCount.Text = completedCount > 0 ? completedCount.ToString() : "";

            // 紧急
            var urgentCount = todos.Count(t => t.Priority == Priority.High && !t.IsCompleted);
            UrgentCardCount.Text = urgentCount.ToString();
        }

        #endregion

        #region 刷新数据

        private void RefreshAll()
        {
            RefreshFilteredTodos();
            UpdateStats();
            UpdateNavCounts();
            UpdateViewTitle();
            RefreshTodayReviewCard();
            
            // 只有"今天"视图显示添加待办区域，其他视图隐藏
            AddTodoSection.Visibility = _currentFilter == "Today" ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private void RefreshTodayReviewCard()
        {
            var today = DateTime.Today;
            var todos = DataService.Instance.Todos.ToList();
            
            // 今日待办统计
            var todayTodos = todos.Where(t => t.DueDate?.Date == today && !t.IsSubTask).ToList();
            var completedCount = todayTodos.Count(t => t.IsCompleted);
            var totalCount = todayTodos.Count;
            
            TodayCompletedText.Text = $"完成 {completedCount} 项";
            
            // 计算连续高效天数（每天都有完成任务）
            int streakDays = 0;
            var checkDate = today;
            for (int i = 0; i < 365; i++)
            {
                var dayTodos = todos.Where(t => t.CompletedAt?.Date == checkDate && t.IsCompleted).ToList();
                if (dayTodos.Count > 0)
                {
                    streakDays++;
                    checkDate = checkDate.AddDays(-1);
                }
                else if (i > 0) // 第一天（今天）可以没完成
                {
                    break;
                }
                else
                {
                    checkDate = checkDate.AddDays(-1);
                }
            }
            TodayStreakText.Text = $"连续 {streakDays} 天高效";
            
            // 进度百分比
            int percentage = totalCount > 0 ? (completedCount * 100 / totalCount) : 0;
            TodayProgressText.Text = $"{percentage}%";
            
            // 更新进度环（通过StrokeDashArray）
            double circumference = 2 * Math.PI * 24; // 半径约24
            double dashLength = (percentage / 100.0) * circumference;
            TodayProgressRing.StrokeDashArray = new System.Windows.Media.DoubleCollection { dashLength / 4, 100 };
        }
        
        private void TodayReviewCard_Click(object sender, MouseButtonEventArgs e)
        {
            // 打开数据统计面板
            NavReview_Click(NavReview, new RoutedEventArgs());
        }

        private void RefreshFilteredTodos()
        {
            var allTodos = DataService.Instance.Todos.ToList();
            var today = DateTime.Today;

            // 先获取所有父任务（非子任务）
            var parentTodos = allTodos.Where(t => !t.IsSubTask).AsEnumerable();

            // 应用筛选（只对父任务筛选）
            parentTodos = _currentFilter switch
            {
                // 今天：包含今天的任务（含已完成）
                "Today" => parentTodos.Where(t => t.DueDate?.Date == today || (t.DueDate == null && !t.IsCompleted)),
                // 最近7天：过去7天到今天的任务（含已完成）
                "Week" => parentTodos.Where(t => t.DueDate?.Date >= today.AddDays(-7) && t.DueDate?.Date <= today),
                // 全部待办：包含所有任务
                "All" => parentTodos,
                // 已完成
                "Completed" => parentTodos.Where(t => t.IsCompleted),
                // 紧急任务
                "Urgent" => parentTodos.Where(t => t.Priority == Priority.High && !t.IsCompleted),
                // 已逾期/顺延：逾期未完成的 或者 已顺延的
                "Overdue" => parentTodos.Where(t => (t.DueDate?.Date < today && !t.IsCompleted) || t.IsPostponed),
                // 自定义分组过滤
                "Group" => parentTodos.Where(t => t.GroupId == _currentGroupId && !t.IsCompleted),
                _ => parentTodos.Where(t => !t.IsCompleted)
            };

            FilteredTodos.Clear();
            
            // 按优先级和创建时间排序父任务，并将子任务插入到父任务后面
            foreach (var parent in parentTodos.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt))
            {
                FilteredTodos.Add(parent);
                
                // 如果父任务展开，添加其子任务
                if (parent.IsExpanded && parent.HasSubTasks)
                {
                    var subTasks = allTodos.Where(t => t.ParentId == parent.Id)
                                           .OrderBy(t => t.CreatedAt);
                    foreach (var sub in subTasks)
                    {
                        FilteredTodos.Add(sub);
                    }
                }
            }

            // 更新空状态
            EmptyState.Visibility = FilteredTodos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TodoList.Visibility = FilteredTodos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStats()
        {
            // 更新完成率
            var rate = DataService.Instance.GetTodayCompletionRate();
            CompletionRate.Text = $"{rate:F0}%";
            
            // 进度条宽度（父容器宽度约180）
            var maxWidth = 180.0;
            ProgressBar.Width = (rate / 100) * maxWidth;
        }

        #endregion

        #region 添加待办

        private void QuickAdd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTodo();
        }

        private void QuickAdd_Click(object sender, RoutedEventArgs e) => AddTodo();

        private void Priority_Select(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string priorityStr)
            {
                if (Enum.TryParse<Priority>(priorityStr, out var priority))
                {
                    _selectedPriority = priority;
                    UpdatePriorityButtons();
                }
            }
        }

        private void UpdatePriorityButtons()
        {
            // 重置所有按钮
            PriorityHigh.Background = Brushes.Transparent;
            PriorityMedium.Background = Brushes.Transparent;

            // 高亮选中的
            switch (_selectedPriority)
            {
                case Priority.High:
                    PriorityHigh.Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68));
                    break;
                case Priority.Medium:
                    PriorityMedium.Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
                    break;
                case Priority.Low:
                    // Low 已经有渐变背景
                    break;
            }
        }

        private void AddTodo()
        {
            var input = QuickAddBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                // 输入为空时，聚焦到输入框并提示
                QuickAddBox.Focus();
                return;
            }

            DateTime? dueDate = DateTime.Today;
            var title = input;

            // 智能日期识别
            title = ParseSmartDate(input, ref dueDate);

            // 获取分组
            string? groupId = null;
            if (GroupCombo.SelectedItem is ComboBoxItem item && item.Tag is string groupStr && !string.IsNullOrEmpty(groupStr))
            {
                groupId = groupStr;
            }

            if (!string.IsNullOrEmpty(title))
            {
                DataService.Instance.AddTodo(title, _selectedPriority, dueDate, groupId);
                RefreshAll();
            }
            
            QuickAddBox.Clear();
            _selectedPriority = Priority.Low;
            UpdatePriorityButtons();
        }

        private string ParseSmartDate(string input, ref DateTime? dueDate)
        {
            var title = input;

            // 明天
            if (input.Contains("明天"))
            {
                dueDate = DateTime.Today.AddDays(1);
                title = input.Replace("明天", "").Trim();
            }
            // 后天
            else if (input.Contains("后天"))
            {
                dueDate = DateTime.Today.AddDays(2);
                title = input.Replace("后天", "").Trim();
            }
            // 下周
            else if (input.Contains("下周"))
            {
                dueDate = DateTime.Today.AddDays(7);
                title = input.Replace("下周", "").Trim();
            }
            // 周一到周日
            else if (input.Contains("周一")) { dueDate = GetNextWeekday(DayOfWeek.Monday); title = input.Replace("周一", "").Trim(); }
            else if (input.Contains("周二")) { dueDate = GetNextWeekday(DayOfWeek.Tuesday); title = input.Replace("周二", "").Trim(); }
            else if (input.Contains("周三")) { dueDate = GetNextWeekday(DayOfWeek.Wednesday); title = input.Replace("周三", "").Trim(); }
            else if (input.Contains("周四")) { dueDate = GetNextWeekday(DayOfWeek.Thursday); title = input.Replace("周四", "").Trim(); }
            else if (input.Contains("周五")) { dueDate = GetNextWeekday(DayOfWeek.Friday); title = input.Replace("周五", "").Trim(); }
            else if (input.Contains("周六")) { dueDate = GetNextWeekday(DayOfWeek.Saturday); title = input.Replace("周六", "").Trim(); }
            else if (input.Contains("周日")) { dueDate = GetNextWeekday(DayOfWeek.Sunday); title = input.Replace("周日", "").Trim(); }

            return string.IsNullOrEmpty(title) ? input : title;
        }

        private DateTime GetNextWeekday(DayOfWeek day)
        {
            var today = DateTime.Today;
            int daysUntil = ((int)day - (int)today.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7; // 如果是今天，则返回下周同一天
            return today.AddDays(daysUntil);
        }

        #endregion

        #region 待办操作

        private void ToggleComplete_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string id)
            {
                DataService.Instance.ToggleComplete(id);
                RefreshAll();
            }
        }

        private void DeleteTodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                DataService.Instance.DeleteTodo(id);
                RefreshAll();
            }
        }

        private void PostponeTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                DataService.Instance.PostponeToToday(id);
                RefreshAll();
            }
        }

        #region 子任务管理

        private void ToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                var todo = DataService.Instance.GetTodo(id);
                if (todo != null)
                {
                    todo.IsExpanded = !todo.IsExpanded;
                    RefreshFilteredTodos();
                }
            }
        }

        private void AddSubTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string parentId)
            {
                // 打开添加子任务弹窗
                SubTaskPopup.IsOpen = true;
                SubTaskParentId.Text = parentId;
                SubTaskInput.Text = "";
                SubTaskInput.Focus();
            }
        }

        private void CloseSubTaskPopup_Click(object sender, RoutedEventArgs e)
        {
            SubTaskPopup.IsOpen = false;
        }

        private void SubTaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmAddSubTask_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                SubTaskPopup.IsOpen = false;
            }
        }

        private void ConfirmAddSubTask_Click(object sender, RoutedEventArgs e)
        {
            var title = SubTaskInput.Text.Trim();
            var parentId = SubTaskParentId.Text;

            if (string.IsNullOrEmpty(title))
            {
                SubTaskInput.Focus();
                return;
            }

            DataService.Instance.AddSubTask(parentId, title);
            
            // 确保父任务是展开的
            var parent = DataService.Instance.GetTodo(parentId);
            if (parent != null)
            {
                parent.IsExpanded = true;
            }

            SubTaskPopup.IsOpen = false;
            RefreshAll();
        }

        #endregion

        private void EditTodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                OpenEditPopup(id);
            }
        }

        private void TodoList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TodoList.SelectedItem is TodoItem todo)
            {
                OpenEditPopup(todo.Id);
            }
        }

        #endregion

        #region 编辑弹窗

        private void OpenEditPopup(string id)
        {
            var todo = DataService.Instance.GetTodo(id);
            if (todo == null) return;

            // 先刷新分组下拉框，确保显示最新的分类
            RefreshGroupCombo();

            _editingTodoId = id;
            EditTitleBox.Text = todo.Title;
            EditDatePicker.SelectedDate = todo.DueDate;
            _selectedTime = todo.DueTime ?? "";
            // 延迟更新时间显示（等待模板加载）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (TimePickerButton.Template.FindName("TimeDisplay", TimePickerButton) is TextBlock display)
                {
                    if (!string.IsNullOrEmpty(_selectedTime))
                    {
                        display.Text = _selectedTime;
                        display.Foreground = new SolidColorBrush(Colors.White);
                    }
                    else
                    {
                        display.Text = "时间";
                        display.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            EditNoteBox.Text = todo.Note ?? "";
            _editPriority = todo.Priority;
            UpdateEditPriorityButtons();
            
            // 设置分组下拉框
            EditGroupCombo.SelectedIndex = 0; // 默认"未分组"
            for (int i = 0; i < EditGroupCombo.Items.Count; i++)
            {
                if (EditGroupCombo.Items[i] is ComboBoxItem item && item.Tag is string groupId)
                {
                    if (groupId == todo.GroupId)
                    {
                        EditGroupCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            EditPopup.Visibility = Visibility.Visible;
            
            // 延迟聚焦，确保弹窗渲染完成后再聚焦
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EditTitleBox.Focus();
                Keyboard.Focus(EditTitleBox);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void EditPriority_Select(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string priorityStr)
            {
                if (Enum.TryParse<Priority>(priorityStr, out var priority))
                {
                    _editPriority = priority;
                    UpdateEditPriorityButtons();
                }
            }
        }

        private void UpdateEditPriorityButtons()
        {
            EditPriorityHigh.Background = Brushes.Transparent;
            EditPriorityMedium.Background = Brushes.Transparent;
            EditPriorityLow.Background = Brushes.Transparent;

            switch (_editPriority)
            {
                case Priority.High:
                    EditPriorityHigh.Background = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68));
                    break;
                case Priority.Medium:
                    EditPriorityMedium.Background = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11));
                    break;
                case Priority.Low:
                    EditPriorityLow.Background = new SolidColorBrush(Color.FromArgb(50, 34, 197, 94));
                    break;
            }
        }

        private void CloseEditPopup_Click(object sender, RoutedEventArgs e)
        {
            EditPopup.Visibility = Visibility.Collapsed;
            _editingTodoId = null;
        }

        private string _selectedTime = "";

        private void TimePickerButton_Click(object sender, RoutedEventArgs e)
        {
            TimePopup.IsOpen = !TimePopup.IsOpen;
        }

        private void TimeOption_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string time)
            {
                _selectedTime = time;
                // 更新按钮内的 TextBlock
                if (TimePickerButton.Template.FindName("TimeDisplay", TimePickerButton) is TextBlock display)
                {
                    display.Text = time;
                    display.Foreground = new SolidColorBrush(Colors.White);
                }
                TimePopup.IsOpen = false;
            }
        }

        private void SaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_editingTodoId == null) return;

            var title = EditTitleBox.Text.Trim();
            if (string.IsNullOrEmpty(title)) return;

            var dueTime = _selectedTime;
            // 验证时间格式
            if (!string.IsNullOrEmpty(dueTime) && !TimeSpan.TryParse(dueTime, out _))
            {
                MessageBox.Show("时间格式不正确，请使用 HH:mm 格式，如 18:00", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 获取分组
            string? groupId = null;
            if (EditGroupCombo.SelectedItem is ComboBoxItem item && item.Tag is string groupStr && !string.IsNullOrEmpty(groupStr))
            {
                groupId = groupStr;
            }

            DataService.Instance.UpdateTodo(
                _editingTodoId,
                title,
                _editPriority,
                EditDatePicker.SelectedDate,
                string.IsNullOrEmpty(dueTime) ? null : dueTime,
                string.IsNullOrEmpty(EditNoteBox.Text) ? null : EditNoteBox.Text,
                groupId
            );

            EditPopup.Visibility = Visibility.Collapsed;
            _editingTodoId = null;
            RefreshAll();
        }

        #endregion

        #region 复盘功能

        private string _reviewPeriod = "Day";

        private void NavReview_Click(object sender, RoutedEventArgs e)
        {
            // 取消导航按钮高亮
            SetActiveNavButton(null);
            // HabitPanel已移除
            ProjectPanel.Visibility = Visibility.Collapsed;
            ProjectDetailPanel.Visibility = Visibility.Collapsed;
            DiaryPanel.Visibility = Visibility.Collapsed;
            
            ReviewPanel.Visibility = Visibility.Visible;
            _reviewPeriod = "Day";
            UpdateReviewPeriodButtons();
            RefreshReviewData();
        }

        private void CloseReview_Click(object sender, RoutedEventArgs e)
        {
            ReviewPanel.Visibility = Visibility.Collapsed;
        }

        private void ReviewPeriod_Click(object sender, RoutedEventArgs e)
        {
            // 兼容旧版 Button 点击
            if (sender is Button btn && btn.Tag is string period1)
            {
                _reviewPeriod = period1;
                UpdateReviewPeriodButtons();
                RefreshReviewData();
            }
        }

        private void ReviewPeriod_Click(object sender, MouseButtonEventArgs e)
        {
            // 新版 Border Tab 点击
            if (sender is Border border && border.Tag is string period)
            {
                _reviewPeriod = period;
                UpdateReviewPeriodButtons();
                RefreshReviewData();
            }
        }

        private void UpdateReviewPeriodButtons()
        {
            var inactiveColor = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255)); // #60FFFFFF
            var activeColor = Brushes.White;
            var indicatorColor = new SolidColorBrush(Color.FromRgb(99, 102, 241)); // #6366F1

            // 重置所有 Tab
            ReviewDay.Foreground = inactiveColor;
            ReviewDay.FontWeight = FontWeights.Normal;
            ReviewDayIndicator.Background = Brushes.Transparent;

            ReviewWeek.Foreground = inactiveColor;
            ReviewWeek.FontWeight = FontWeights.Normal;
            ReviewWeekIndicator.Background = Brushes.Transparent;

            ReviewMonth.Foreground = inactiveColor;
            ReviewMonth.FontWeight = FontWeights.Normal;
            ReviewMonthIndicator.Background = Brushes.Transparent;

            ReviewYear.Foreground = inactiveColor;
            ReviewYear.FontWeight = FontWeights.Normal;
            ReviewYearIndicator.Background = Brushes.Transparent;

            // 高亮当前选中的 Tab
            switch (_reviewPeriod)
            {
                case "Day":
                    ReviewDay.Foreground = activeColor;
                    ReviewDay.FontWeight = FontWeights.Medium;
                    ReviewDayIndicator.Background = indicatorColor;
                    break;
                case "Week":
                    ReviewWeek.Foreground = activeColor;
                    ReviewWeek.FontWeight = FontWeights.Medium;
                    ReviewWeekIndicator.Background = indicatorColor;
                    break;
                case "Month":
                    ReviewMonth.Foreground = activeColor;
                    ReviewMonth.FontWeight = FontWeights.Medium;
                    ReviewMonthIndicator.Background = indicatorColor;
                    break;
                case "Year":
                    ReviewYear.Foreground = activeColor;
                    ReviewYear.FontWeight = FontWeights.Medium;
                    ReviewYearIndicator.Background = indicatorColor;
                    break;
            }
        }

        private void RefreshReviewData()
        {
            var todos = DataService.Instance.Todos.ToList();
            var today = DateTime.Today;

            // 根据时间段筛选
            DateTime startDate, endDate;
            string periodTitle;

            switch (_reviewPeriod)
            {
                case "Day":
                    startDate = today;
                    endDate = today;
                    periodTitle = $"今日统计 - {today:M月d日}";
                    break;
                case "Week":
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    startDate = today.AddDays(-diff);
                    endDate = today;
                    periodTitle = $"本周统计 - {startDate:M月d日} 至 {endDate:M月d日}";
                    break;
                case "Month":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = today;
                    periodTitle = $"本月统计 - {today:yyyy年M月}";
                    break;
                case "Year":
                    startDate = new DateTime(today.Year, 1, 1);
                    endDate = today;
                    periodTitle = $"年度统计 - {today:yyyy年}";
                    break;
                default:
                    startDate = today;
                    endDate = today;
                    periodTitle = "数据统计";
                    break;
            }

            ReviewTitle.Text = "📊 " + periodTitle;

            // 筛选时间段内的任务（按创建日期或截止日期）
            var periodTodos = todos.Where(t => 
                (t.CreatedAt.Date >= startDate && t.CreatedAt.Date <= endDate) ||
                (t.DueDate?.Date >= startDate && t.DueDate?.Date <= endDate) ||
                (t.CompletedAt?.Date >= startDate && t.CompletedAt?.Date <= endDate)
            ).ToList();

            var completedTodos = periodTodos.Where(t => t.IsCompleted).ToList();
            var overdueTodos = periodTodos.Where(t => !t.IsCompleted && t.DueDate?.Date < today).ToList();

            // 更新统计卡片
            ReviewTotalCount.Text = periodTodos.Count.ToString();
            ReviewCompletedCount.Text = completedTodos.Count.ToString();
            ReviewOverdueCount.Text = overdueTodos.Count.ToString();

            var rate = periodTodos.Count > 0 ? (double)completedTodos.Count / periodTodos.Count * 100 : 0;
            ReviewCompletionRate.Text = $"{rate:F0}%";

            // 分组统计（动态显示用户创建的分组）
            var groups = DataService.Instance.Groups.ToList();
            if (groups.Count > 0)
            {
                ReviewGroupSection.Visibility = Visibility.Visible;
                var groupStats = groups.Select(g => new
                {
                    g.Icon,
                    g.Name,
                    g.Color,
                    Stats = $"{completedTodos.Count(t => t.GroupId == g.Id)}/{periodTodos.Count(t => t.GroupId == g.Id)}"
                }).ToList();
                ReviewGroupList.ItemsSource = groupStats;
            }
            else
            {
                ReviewGroupSection.Visibility = Visibility.Collapsed;
            }

            // 优先级统计
            var highCount = periodTodos.Count(t => t.Priority == Priority.High);
            var mediumCount = periodTodos.Count(t => t.Priority == Priority.Medium);
            var lowCount = periodTodos.Count(t => t.Priority == Priority.Low);
            var maxPriority = Math.Max(Math.Max(highCount, mediumCount), Math.Max(lowCount, 1));
            double barMaxWidth = 400;

            ReviewHighCount.Text = highCount.ToString();
            ReviewMediumCount.Text = mediumCount.ToString();
            ReviewLowCount.Text = lowCount.ToString();

            ReviewHighBar.Width = (highCount / (double)maxPriority) * barMaxWidth;
            ReviewMediumBar.Width = (mediumCount / (double)maxPriority) * barMaxWidth;
            ReviewLowBar.Width = (lowCount / (double)maxPriority) * barMaxWidth;

            // 更新分类统计
            UpdateCategoryStats(completedTodos);
            
            // 更新完成趋势图（最近7天）
            UpdateTrendChart(todos, today, _reviewPeriod);
            
            // 更新对比（根据时间段）
            UpdateComparison(todos, today, _reviewPeriod);
            
            // 更新效率洞察
            UpdateEfficiencyInsights(completedTodos, periodTodos, today, _reviewPeriod);
            
            // 更新鼓励语
            UpdateEncourageText(completedTodos.Count, periodTodos.Count);
        }
        
        private void UpdateTrendChart(List<TodoItem> todos, DateTime today, string period)
        {
            Border[] bars = { TrendBar0, TrendBar1, TrendBar2, TrendBar3, TrendBar4, TrendBar5, TrendBar6 };
            TextBlock[] labels = { TrendLabel0, TrendLabel1, TrendLabel2, TrendLabel3, TrendLabel4, TrendLabel5, TrendLabel6 };
            TextBlock[] countLabels = { TrendCount0, TrendCount1, TrendCount2, TrendCount3, TrendCount4, TrendCount5, TrendCount6 };
            
            int[] counts = new int[7];
            string[] labelTexts = new string[7];
            int maxCount = 1;
            
            // 根据不同的时间段设置标题和数据
            switch (period)
            {
                case "Day":
                    ReviewTrendTitle.Text = "最近7天";
                    for (int i = 0; i < 7; i++)
                    {
                        var date = today.AddDays(i - 6);
                        counts[i] = todos.Count(t => t.CompletedAt?.Date == date && t.IsCompleted);
                        labelTexts[i] = date.ToString("d日");
                    }
                    break;
                    
                case "Week":
                    ReviewTrendTitle.Text = "本周趋势";
                    // 计算本周一的日期
                    int daysFromMonday = (7 + (int)today.DayOfWeek - 1) % 7;
                    var weekMonday = today.AddDays(-daysFromMonday);
                    string[] weekDayNames = { "一", "二", "三", "四", "五", "六", "日" };
                    for (int i = 0; i < 7; i++)
                    {
                        var date = weekMonday.AddDays(i);
                        counts[i] = todos.Count(t => t.CompletedAt?.Date == date && t.IsCompleted);
                        labelTexts[i] = weekDayNames[i];
                    }
                    break;
                    
                case "Month":
                    ReviewTrendTitle.Text = "本月趋势";
                    // 只显示4周（合理的月度划分）
                    var mStart = new DateTime(today.Year, today.Month, 1);
                    string[] weekLabels = { "第1周", "第2周", "第3周", "第4周" };
                    for (int i = 0; i < 7; i++)
                    {
                        if (i < 4)
                        {
                            var weekStart = mStart.AddDays(i * 7);
                            var weekEnd = weekStart.AddDays(6);
                            var monthEnd = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                            if (weekEnd > monthEnd) weekEnd = monthEnd;
                            if (weekEnd > today) weekEnd = today;
                            
                            if (weekStart > today)
                                counts[i] = 0;
                            else
                                counts[i] = todos.Count(t => t.CompletedAt?.Date >= weekStart && t.CompletedAt?.Date <= weekEnd && t.IsCompleted);
                            labelTexts[i] = weekLabels[i];
                        }
                        else
                        {
                            counts[i] = -1; // 标记为隐藏
                            labelTexts[i] = "";
                        }
                    }
                    break;
                
                case "Year":
                    ReviewTrendTitle.Text = "全年趋势";
                    // 只显示4个季度（合理的年度划分）
                    string[] quarterLabels = { "Q1", "Q2", "Q3", "Q4" };
                    for (int i = 0; i < 7; i++)
                    {
                        if (i < 4)
                        {
                            var qStart = new DateTime(today.Year, i * 3 + 1, 1);
                            var qEnd = qStart.AddMonths(3).AddDays(-1);
                            if (qEnd > today) qEnd = today;
                            
                            if (qStart > today)
                                counts[i] = 0;
                            else
                                counts[i] = todos.Count(t => t.CompletedAt?.Date >= qStart && t.CompletedAt?.Date <= qEnd && t.IsCompleted);
                            labelTexts[i] = quarterLabels[i];
                        }
                        else
                        {
                            counts[i] = -1; // 标记为隐藏
                            labelTexts[i] = "";
                        }
                    }
                    break;
                    
                default:
                    ReviewTrendTitle.Text = "趋势";
                    for (int i = 0; i < 7; i++)
                    {
                        var date = today.AddDays(i - 6);
                        counts[i] = todos.Count(t => t.CompletedAt?.Date == date && t.IsCompleted);
                        labelTexts[i] = date.ToString("d日");
                    }
                    break;
            }
            
            // 计算最大值
            for (int i = 0; i < 7; i++)
            {
                if (counts[i] > 0)
                    maxCount = Math.Max(maxCount, counts[i]);
            }
            
            // 更新柱状图
            // 获取列容器
            StackPanel[] columns = { TrendColumn0, TrendColumn1, TrendColumn2, TrendColumn3, TrendColumn4, TrendColumn5, TrendColumn6 };
            
            int visibleCount = 0;
            for (int i = 0; i < 7; i++)
            {
                if (counts[i] == -1)
                {
                    // 隐藏整个列
                    columns[i].Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 显示这个列
                    columns[i].Visibility = Visibility.Visible;
                    visibleCount++;
                    
                    double height = (counts[i] / (double)maxCount) * 80;
                    bars[i].Height = Math.Max(height, counts[i] > 0 ? 8 : 4);
                    labels[i].Text = labelTexts[i];
                    countLabels[i].Text = counts[i] > 0 ? counts[i].ToString() : "";
                }
            }
            
            // 动态设置 UniformGrid 的列数
            TrendChartGrid.Columns = visibleCount;
        }
        
        private void UpdateComparison(List<TodoItem> todos, DateTime today, string period)
        {
            int currentCount = 0;
            int previousCount = 0;
            string label = "较上周";
            
            switch (period)
            {
                case "Day":
                    label = "较昨日";
                    currentCount = todos.Count(t => t.CompletedAt?.Date == today && t.IsCompleted);
                    previousCount = todos.Count(t => t.CompletedAt?.Date == today.AddDays(-1) && t.IsCompleted);
                    break;
                    
                case "Week":
                    label = "较上周";
                    int thisDiff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var thisWeekStart = today.AddDays(-thisDiff);
                    currentCount = todos.Count(t => t.CompletedAt?.Date >= thisWeekStart && t.CompletedAt?.Date <= today && t.IsCompleted);
                    var lastWeekStart = thisWeekStart.AddDays(-7);
                    var lastWeekEnd = thisWeekStart.AddDays(-1);
                    previousCount = todos.Count(t => t.CompletedAt?.Date >= lastWeekStart && t.CompletedAt?.Date <= lastWeekEnd && t.IsCompleted);
                    break;
                    
                case "Month":
                    label = "较上月";
                    var thisMonthStart = new DateTime(today.Year, today.Month, 1);
                    currentCount = todos.Count(t => t.CompletedAt?.Date >= thisMonthStart && t.CompletedAt?.Date <= today && t.IsCompleted);
                    var lastMonthStart = thisMonthStart.AddMonths(-1);
                    var lastMonthEnd = thisMonthStart.AddDays(-1);
                    previousCount = todos.Count(t => t.CompletedAt?.Date >= lastMonthStart && t.CompletedAt?.Date <= lastMonthEnd && t.IsCompleted);
                    break;
                    
                case "Year":
                    label = "较去年";
                    var thisYearStart = new DateTime(today.Year, 1, 1);
                    currentCount = todos.Count(t => t.CompletedAt?.Date >= thisYearStart && t.CompletedAt?.Date <= today && t.IsCompleted);
                    var lastYearStart = thisYearStart.AddYears(-1);
                    var lastYearEnd = thisYearStart.AddDays(-1);
                    previousCount = todos.Count(t => t.CompletedAt?.Date >= lastYearStart && t.CompletedAt?.Date <= lastYearEnd && t.IsCompleted);
                    break;
            }
            
            ReviewCompareLabel.Text = label;
            
            // 计算变化
            if (previousCount > 0)
            {
                int change = currentCount - previousCount;
                if (change >= 0)
                {
                    ReviewCompareValue.Text = $"+{change}";
                    ReviewCompareValue.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
                else
                {
                    ReviewCompareValue.Text = change.ToString();
                    ReviewCompareValue.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
            }
            else
            {
                ReviewCompareValue.Text = currentCount > 0 ? $"+{currentCount}" : "0";
                ReviewCompareValue.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
        }
        
        private void UpdateEfficiencyInsights(List<TodoItem> completedTodos, List<TodoItem> periodTodos, DateTime today, string period)
        {
            // 计算效率最高的日期
            if (completedTodos.Count > 0)
            {
                var bestDay = completedTodos
                    .Where(t => t.CompletedAt.HasValue)
                    .GroupBy(t => t.CompletedAt!.Value.DayOfWeek)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                    
                if (bestDay != null)
                {
                    string[] dayNames = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
                    ReviewBestDay.Text = $"{dayNames[(int)bestDay.Key]} ({bestDay.Count()}项)";
                }
                else
                {
                    ReviewBestDay.Text = "暂无数据";
                }
            }
            else
            {
                ReviewBestDay.Text = "暂无数据";
            }
            
            // 计算日均完成
            int dayCount = period switch
            {
                "Day" => 1,
                "Week" => Math.Max(1, (int)(today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek)),
                "Month" => today.Day,
                "Year" => today.DayOfYear,
                _ => 1
            };
            
            double avgDaily = completedTodos.Count / (double)dayCount;
            ReviewAvgDaily.Text = $"{avgDaily:F1} 项";
        }
        
        private void UpdateCategoryStats(List<TodoItem> completedTodos)
        {
            ReviewCategoryStats.Children.Clear();
            
            // 按分类统计完成数（将GroupId转换为显示名称）
            var categoryStats = completedTodos
                .GroupBy(t => string.IsNullOrEmpty(t.GroupId) ? "未分类" : t.GroupId)
                .Select(g => {
                    var groupName = g.Key == "未分类" ? "未分类" : DataService.Instance.GetGroupName(g.Key);
                    return new { Name = groupName, Count = g.Count() };
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();
            
            if (categoryStats.Count == 0)
            {
                ReviewNoCategoryStats.Visibility = Visibility.Visible;
                return;
            }
            
            ReviewNoCategoryStats.Visibility = Visibility.Collapsed;
            var maxCount = categoryStats.Max(x => x.Count);
            
            string[] colors = { "#3B82F6", "#8B5CF6", "#22C55E", "#F59E0B", "#EC4899" };
            int colorIndex = 0;
            
            foreach (var stat in categoryStats)
            {
                var color = colors[colorIndex++ % colors.Length];
                var percentage = maxCount > 0 ? (double)stat.Count / completedTodos.Count * 100 : 0;
                
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                
                var nameText = new TextBlock
                {
                    Text = stat.Name,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameText, 0);
                grid.Children.Add(nameText);
                
                var barBorder = new Border
                {
                    Background = (SolidColorBrush)new BrushConverter().ConvertFrom(color)!,
                    CornerRadius = new CornerRadius(3),
                    Height = 10,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(10, percentage * 1.5)
                };
                Grid.SetColumn(barBorder, 1);
                grid.Children.Add(barBorder);
                
                var percentText = new TextBlock
                {
                    Text = $"{percentage:F0}%",
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(percentText, 2);
                grid.Children.Add(percentText);
                
                ReviewCategoryStats.Children.Add(grid);
            }
        }
        
        private void UpdateEncourageText(int completed, int total)
        {
            string[] messages;
            
            if (total == 0)
            {
                messages = new[] { "开始添加待办，让每一天都有意义 ✨" };
            }
            else
            {
                double rate = (double)completed / total;
                if (rate >= 1.0)
                {
                    messages = new[] { "太棒了！全部完成！🎉", "满分成就！继续保持！💯", "效率之王！你今天超厉害！🏆" };
                }
                else if (rate >= 0.7)
                {
                    messages = new[] { "做得很好！再加把劲！💪", "已经完成大部分，继续努力！🌟", "优秀！胜利就在眼前！🚀" };
                }
                else if (rate >= 0.3)
                {
                    messages = new[] { "加油！每一步都是进步！🌱", "进度不错，继续前进！🎯", "正在路上，保持节奏！⚡" };
                }
                else
                {
                    messages = new[] { "今天也是新的开始！☀️", "千里之行，始于足下 🚶", "每完成一件，就离目标更近！🎯" };
                }
            }
            
            var random = new Random();
            EncourageText.Text = messages[random.Next(messages.Length)];
        }

        // 保留历史记录查看（隐藏状态）
        private void ViewReviewHistory_Click(object sender, RoutedEventArgs e)
        {
            var history = DataService.Instance.GetReviewHistory(_reviewPeriod, 10).ToList();
            
            if (history.Count == 0)
            {
                MessageBox.Show($"暂无{GetPeriodName(_reviewPeriod)}历史记录", "历史记录", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var historyText = new System.Text.StringBuilder();
            historyText.AppendLine($"📚 {GetPeriodName(_reviewPeriod)}复盘历史记录\n");
            
            foreach (var review in history)
            {
                historyText.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━");
                historyText.AppendLine($"📅 {review.Date:yyyy年M月d日}");
                historyText.AppendLine($"📝 {review.Title}");
                historyText.AppendLine();
                historyText.AppendLine(review.Content);
                if (!string.IsNullOrEmpty(review.Reflection))
                {
                    historyText.AppendLine();
                    historyText.AppendLine($"💡 反思: {review.Reflection}");
                }
                historyText.AppendLine();
            }

            MessageBox.Show(historyText.ToString(), "历史复盘记录", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private string GetPeriodName(string period)
        {
            return period switch
            {
                "Day" => "日",
                "Week" => "周",
                "Month" => "月",
                "Year" => "年",
                _ => ""
            };
        }

        #endregion

        #region 分组管理

        private void RefreshGroupNav()
        {
            // 分组导航已移除，仅刷新下拉框
        }

        private void RefreshGroupCombo()
        {
            // 动态更新分组下拉框
            GroupCombo.Items.Clear();
            GroupCombo.Items.Add(new ComboBoxItem { Content = "📂 未分组", Tag = "" });
            
            foreach (var group in DataService.Instance.Groups.OrderBy(g => g.Order))
            {
                GroupCombo.Items.Add(new ComboBoxItem 
                { 
                    Content = $"{group.Icon} {group.Name}", 
                    Tag = group.Id 
                });
            }
            GroupCombo.SelectedIndex = 0;
            
            // 同时更新编辑弹窗的分组下拉框
            EditGroupCombo.Items.Clear();
            EditGroupCombo.Items.Add(new ComboBoxItem { Content = "📁 未分组", Tag = "", Foreground = new SolidColorBrush(Colors.White) });
            
            foreach (var group in DataService.Instance.Groups.OrderBy(g => g.Order))
            {
                EditGroupCombo.Items.Add(new ComboBoxItem 
                { 
                    Content = $"{group.Icon} {group.Name}", 
                    Tag = group.Id,
                    Foreground = new SolidColorBrush(Colors.White)
                });
            }
            EditGroupCombo.SelectedIndex = 0;
        }

        private void NavGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string groupId)
            {
                _currentFilter = "Group";
                _currentGroupId = groupId;
                
                // 隐藏所有其他面板
                ReviewPanel.Visibility = Visibility.Collapsed;
                // HabitPanel已移除
                ProjectPanel.Visibility = Visibility.Collapsed;
                ProjectDetailPanel.Visibility = Visibility.Collapsed;
                DiaryPanel.Visibility = Visibility.Collapsed;
                
                SetActiveNavButton(null); // 分组按钮不使用通用高亮
                RefreshAll();
            }
        }

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            _newGroupIcon = "📁";
            _newGroupColor = "#6366F1";
            GroupNameInput.Clear();
            UpdateGroupIconSelection();
            UpdateGroupColorSelection();
            AddGroupPopup.Visibility = Visibility.Visible;
            GroupNameInput.Focus();
        }

        private void CloseGroupPopup_Click(object sender, RoutedEventArgs e)
        {
            AddGroupPopup.Visibility = Visibility.Collapsed;
        }

        private void SelectIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string icon)
            {
                _newGroupIcon = icon;
                UpdateGroupIconSelection();
            }
        }

        private void SelectColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string color)
            {
                _newGroupColor = color;
                UpdateGroupColorSelection();
            }
        }

        private void UpdateGroupIconSelection()
        {
            foreach (Border border in IconSelector.Children.OfType<Border>())
            {
                border.BorderBrush = border.Tag?.ToString() == _newGroupIcon
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"))
                    : Brushes.Transparent;
                border.BorderThickness = new Thickness(border.Tag?.ToString() == _newGroupIcon ? 2 : 0);
            }
        }

        private void UpdateGroupColorSelection()
        {
            foreach (Border border in ColorSelector.Children.OfType<Border>())
            {
                border.BorderBrush = border.Tag?.ToString() == _newGroupColor
                    ? new SolidColorBrush(Colors.White)
                    : Brushes.Transparent;
                border.BorderThickness = new Thickness(border.Tag?.ToString() == _newGroupColor ? 2 : 0);
            }
        }

        private void ConfirmAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var name = GroupNameInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入分组名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DataService.Instance.AddGroup(name, _newGroupIcon, _newGroupColor);
            AddGroupPopup.Visibility = Visibility.Collapsed;
            RefreshGroupCombo();
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // 阻止事件冒泡到父按钮
            
            if (sender is Button btn && btn.Tag is string groupId)
            {
                var group = DataService.Instance.GetGroup(groupId);
                if (group != null)
                {
                    var result = MessageBox.Show($"确定删除分组 {group.Name} 吗？\n该分组下的待办将变为未分组", 
                        "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        DataService.Instance.DeleteGroup(groupId);
                        RefreshGroupCombo();
                        
                        // 如果当前正在查看这个分组，切换到今天
                        if (_currentGroupId == groupId)
                        {
                            _currentFilter = "Today";
                            _currentGroupId = null;
                            SetActiveNavButton(NavToday);
                            RefreshAll();
                        }
                    }
                }
            }
        }

        #endregion

        #region 项目管理

        private string _selectedProjectIcon = "📁";
        private string? _currentProjectId = null;

        private void NavProject_Click(object sender, RoutedEventArgs e)
        {
            // 取消导航按钮高亮
            SetActiveNavButton(null);
            ReviewPanel.Visibility = Visibility.Collapsed;
            // HabitPanel已移除
            ProjectDetailPanel.Visibility = Visibility.Collapsed;
            
            ProjectPanel.Visibility = Visibility.Visible;
            RefreshProjectList();
        }

        private void ProjectNav_Click(object sender, RoutedEventArgs e)
        {
            // 点击侧边栏分类项，直接打开分类详情
            if (sender is Button btn && btn.Tag is string projectId)
            {
                SetActiveNavButton(null);
                ReviewPanel.Visibility = Visibility.Collapsed;
                // HabitPanel已移除
                ProjectPanel.Visibility = Visibility.Collapsed;
                DiaryPanel.Visibility = Visibility.Collapsed;
                
                OpenProjectDetail(projectId);
            }
        }

        private void RefreshProjectNavList()
        {
            var projects = DataService.Instance.Projects.Where(p => !p.IsArchived).ToList();
            var displayList = projects.Select(p => {
                var linkedTodos = DataService.Instance.GetProjectLinkedTodos(p.Id).ToList();
                return new {
                    Id = p.Id,
                    Name = p.Name,
                    Icon = p.Icon,
                    TaskCount = linkedTodos.Count(t => !t.IsCompleted)
                };
            }).ToList();
            ProjectNavList.ItemsSource = displayList;
        }

        private void RefreshProjectList()
        {
            var projects = DataService.Instance.Projects.Where(p => !p.IsArchived).ToList();
            
            // 为每个项目计算基于TodoItem的统计数据
            var projectDisplayList = projects.Select(p => {
                var linkedTodos = DataService.Instance.GetProjectLinkedTodos(p.Id).ToList();
                var completedCount = linkedTodos.Count(t => t.IsCompleted);
                var totalCount = linkedTodos.Count;
                
                // 计算最近完成的任务标题
                var lastCompleted = linkedTodos
                    .Where(t => t.IsCompleted && t.CompletedAt.HasValue)
                    .OrderByDescending(t => t.CompletedAt)
                    .FirstOrDefault();
                
                return new {
                    p.Id,
                    p.Name,
                    p.Icon,
                    p.Color,
                    ProgressText = $"{completedCount}/{totalCount} 完成",
                    ConsecutiveDays = CalculateConsecutiveDays(linkedTodos),
                    LastCompletedTitle = lastCompleted != null ? $"最近：完成了「{lastCompleted.Title}」" : ""
                };
            }).ToList();
            
            ProjectList.ItemsSource = projectDisplayList;
            
            EmptyProjectState.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // 刷新侧边栏分类列表
            RefreshProjectNavList();
        }
        
        private int CalculateConsecutiveDays(List<TodoItem> todos)
        {
            var recentCompleted = todos
                .Where(t => t.IsCompleted && t.CompletedAt.HasValue)
                .Select(t => t.CompletedAt!.Value.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();
            
            if (recentCompleted.Count == 0) return 0;
            
            int days = 0;
            var checkDate = DateTime.Today;
            
            foreach (var date in recentCompleted)
            {
                if (date == checkDate || date == checkDate.AddDays(-1))
                {
                    days++;
                    checkDate = date.AddDays(-1);
                }
                else break;
            }
            
            return days;
        }

        private void CloseProjectPanel_Click(object sender, RoutedEventArgs e)
        {
            ProjectPanel.Visibility = Visibility.Collapsed;
            _currentFilter = "Today";
            SetActiveNavButton(NavToday);
            RefreshAll();
        }

        private void AddProject_Click(object sender, RoutedEventArgs e)
        {
            _selectedProjectIcon = "📁";
            NewProjectName.Text = "";
            AddProjectPopup.Visibility = Visibility.Visible;
            NewProjectName.Focus();
        }

        private void CloseAddProjectPopup_Click(object sender, RoutedEventArgs e)
        {
            AddProjectPopup.Visibility = Visibility.Collapsed;
        }

        private void SelectProjectIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string icon)
            {
                _selectedProjectIcon = icon;
                
                // 更新选中状态
                foreach (Border child in ProjectIconList.Children)
                {
                    child.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                }
                border.Background = new SolidColorBrush(Color.FromArgb(80, 139, 92, 246));
            }
        }

        private void ConfirmAddProject_Click(object sender, RoutedEventArgs e)
        {
            var name = NewProjectName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                NewProjectName.Focus();
                return;
            }

            DataService.Instance.AddProject(name, _selectedProjectIcon);
            AddProjectPopup.Visibility = Visibility.Collapsed;
            RefreshProjectList();
            RefreshProjectNavList();  // 刷新侧边栏分类列表
            RefreshGroupCombo();  // 刷新分组列表，让新项目分组显示在待办分组选择中
        }

        private void ProjectCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string projectId)
            {
                OpenProjectDetail(projectId);
            }
        }

        private void OpenProjectDetail(string projectId)
        {
            _currentProjectId = projectId;
            var project = DataService.Instance.GetProject(projectId);
            if (project == null) return;

            // 获取关联的待办（作为分类任务）
            var linkedTodos = DataService.Instance.GetProjectLinkedTodos(projectId).ToList();
            var completedCount = linkedTodos.Count(t => t.IsCompleted);
            var totalCount = linkedTodos.Count;

            // 设置标题
            ProjectDetailIcon.Text = project.Icon;
            ProjectDetailName.Text = project.Name;
            ProjectDetailProgress.Text = $"{completedCount}/{totalCount} 完成";
            
            // 计算连续天数（基于最近完成的待办）
            var recentCompleted = linkedTodos
                .Where(t => t.IsCompleted && t.CompletedAt.HasValue)
                .OrderByDescending(t => t.CompletedAt)
                .Take(30)
                .ToList();
            
            int consecutiveDays = 0;
            var checkDate = DateTime.Today;
            foreach (var todo in recentCompleted)
            {
                if (todo.CompletedAt?.Date == checkDate || todo.CompletedAt?.Date == checkDate.AddDays(-1))
                {
                    if (todo.CompletedAt?.Date == checkDate.AddDays(-1))
                    {
                        checkDate = checkDate.AddDays(-1);
                    }
                    consecutiveDays++;
                }
                else break;
            }
            ProjectDetailDays.Text = consecutiveDays.ToString();

            // 成长记录（最近完成的待办）
            var records = recentCompleted.Take(5).Select(t => new { 
                Date = t.CompletedAt?.ToString("MM-dd") ?? "", 
                Title = $"完成了「{t.Title}」" 
            }).ToList();
            GrowthRecordList.ItemsSource = records;
            NoGrowthRecordText.Visibility = records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // 隐藏原来的"关联待办"区域（已合并到分类任务）
            LinkedTodosSection.Visibility = Visibility.Collapsed;

            // 分类任务列表（显示所有关联的TodoItem）
            ProjectTaskList.ItemsSource = linkedTodos;

            // 切换面板
            ProjectPanel.Visibility = Visibility.Collapsed;
            ProjectDetailPanel.Visibility = Visibility.Visible;
        }

        private void RefreshProjectDetail()
        {
            if (_currentProjectId == null) return;
            OpenProjectDetail(_currentProjectId);
        }

        private void BackToProjectList_Click(object sender, RoutedEventArgs e)
        {
            ProjectDetailPanel.Visibility = Visibility.Collapsed;
            ProjectPanel.Visibility = Visibility.Visible;
            RefreshProjectList();
        }

        private void ProjectMore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void ProjectTaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddProjectTask_Click(sender, e);
            }
        }

        private void AddProjectTask_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProjectId == null) return;

            var project = DataService.Instance.GetProject(_currentProjectId);
            if (project == null || string.IsNullOrEmpty(project.LinkedGroupId)) return;

            var title = ProjectTaskInput.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                ProjectTaskInput.Focus();
                return;
            }

            // 创建普通待办，关联到分类的分组，截止日期为今天
            DataService.Instance.AddTodo(title, Priority.Low, DateTime.Today, project.LinkedGroupId);
            ProjectTaskInput.Text = "";
            RefreshProjectDetail();
            RefreshFilteredTodos(); // 同步刷新待办列表
        }

        private void ToggleProjectTask_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string todoId)
            {
                DataService.Instance.ToggleComplete(todoId);
                RefreshProjectDetail();
                RefreshFilteredTodos(); // 同步刷新待办列表
            }
        }

        private void DeleteProjectTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string todoId)
            {
                DataService.Instance.DeleteTodo(todoId);
                RefreshProjectDetail();
                RefreshFilteredTodos(); // 同步刷新待办列表
            }
        }

        private void ToggleLinkedTodo_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string todoId)
            {
                DataService.Instance.ToggleComplete(todoId);
                RefreshProjectDetail();
            }
        }

        private void DeleteProject_Click(object sender, MouseButtonEventArgs e)
        {
            DeleteProjectCore();
        }

        private void DeleteProject_Click(object sender, RoutedEventArgs e)
        {
            DeleteProjectCore();
        }

        private void DeleteProjectCore()
        {
            if (_currentProjectId == null) return;

            var project = DataService.Instance.GetProject(_currentProjectId);
            if (project == null) return;

            var result = MessageBox.Show($"确定要删除分类「{project.Name}」吗？\n分类任务将被删除。", 
                "删除分类", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                bool deleteGroup = false;
                
                // 如果有关联分组，询问是否同时删除
                if (!string.IsNullOrEmpty(project.LinkedGroupId))
                {
                    var groupResult = MessageBox.Show(
                        $"是否同时删除关联的分组「{project.Icon} {project.Name}」？\n\n" +
                        "• 选择【是】：分组和其下的待办都会被清理\n" +
                        "• 选择【否】：保留分组，待办不受影响", 
                        "删除关联分组", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    deleteGroup = (groupResult == MessageBoxResult.Yes);
                }

                DataService.Instance.DeleteProject(_currentProjectId, deleteGroup);
                _currentProjectId = null;
                ProjectDetailPanel.Visibility = Visibility.Collapsed;
                ProjectPanel.Visibility = Visibility.Visible;
                RefreshProjectList();
                RefreshProjectNavList();  // 刷新侧边栏分类列表
                RefreshGroupCombo();
            }
        }

        #endregion

        #region 支持作者

        private void SupportAuthor_Click(object sender, MouseButtonEventArgs e)
        {
            SupportAuthorPopup.Visibility = Visibility.Visible;
        }

        private void CloseSupportAuthorPopup_Click(object sender, RoutedEventArgs e)
        {
            SupportAuthorPopup.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region 云同步

        private void InitializeCloudSync()
        {
            // 监听云服务状态变化
            CloudService.Instance.AuthStateChanged += (s, e) => Dispatcher.Invoke(UpdateCloudSyncUI);
            CloudService.Instance.SyncStatusChanged += (s, status) => Dispatcher.Invoke(() => 
            {
                CloudStatusText.Text = status;
            });
            
            // 初始化 UI
            UpdateCloudSyncUI();
        }

        private void UpdateCloudSyncUI()
        {
            if (CloudService.Instance.IsLoggedIn)
            {
                CloudIcon.Text = "✅";
                CloudStatusText.Text = "已登录";
                CloudUserEmail.Text = CloudService.Instance.UserEmail ?? "";
                CloudUserEmail.Visibility = Visibility.Visible;
                SyncStatusIcon.Text = "🔄";
            }
            else
            {
                CloudIcon.Text = "☁️";
                CloudStatusText.Text = "点击登录同步数据";
                CloudUserEmail.Visibility = Visibility.Collapsed;
                SyncStatusIcon.Text = "→";
            }
        }

        private async void CloudSync_Click(object sender, MouseButtonEventArgs e)
        {
            if (CloudService.Instance.IsLoggedIn)
            {
                // 已登录，打开个人中心
                var profileWindow = new UserProfileWindow();
                profileWindow.Owner = this;
                profileWindow.ShowDialog();
                
                if (profileWindow.NeedRefresh)
                {
                    RefreshAll();
                }
                UpdateCloudSyncUI();
            }
            else
            {
                // 未登录，打开登录窗口
                var loginWindow = new LoginWindow();
                loginWindow.Owner = this;
                var dialogResult = loginWindow.ShowDialog();

                if (dialogResult == true && loginWindow.IsLoggedIn)
                {
                    UpdateCloudSyncUI();
                    
                    // 登录成功后静默执行智能同步
                    CloudStatusText.Text = "同步中...";
                    var (success, _, _) = await CloudService.Instance.SmartSyncAsync();
                    
                    if (success)
                    {
                        RefreshAll();
                    }
                    
                    // 启动自动同步定时器
                    CloudService.Instance.StartAutoSyncTimer();
                    
                    UpdateCloudSyncUI();
                }
            }
        }

        #endregion

        #region 选择现有待办

        private void SelectExistingTodo_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProjectId == null) return;

            var project = DataService.Instance.GetProject(_currentProjectId);
            if (project == null || string.IsNullOrEmpty(project.LinkedGroupId)) return;

            // 获取所有不属于当前分类的待办（非子任务）
            var availableTodos = DataService.Instance.Todos
                .Where(t => !t.IsSubTask && t.GroupId != project.LinkedGroupId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            SelectableTodoList.ItemsSource = availableTodos;
            NoSelectableTodosText.Visibility = availableTodos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SelectExistingTodoPopup.Visibility = Visibility.Visible;
        }

        private void CloseSelectTodoPopup_Click(object sender, RoutedEventArgs e)
        {
            SelectExistingTodoPopup.Visibility = Visibility.Collapsed;
        }

        private void SelectTodoItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string todoId && _currentProjectId != null)
            {
                var project = DataService.Instance.GetProject(_currentProjectId);
                if (project == null || string.IsNullOrEmpty(project.LinkedGroupId)) return;

                var todo = DataService.Instance.GetTodo(todoId);
                if (todo != null)
                {
                    // 将待办添加到当前分类（通过更新GroupId）
                    todo.GroupId = project.LinkedGroupId;
                    todo.UpdatedAt = DateTime.Now;
                    DataService.Instance.Save();

                    // 关闭弹窗并刷新
                    SelectExistingTodoPopup.Visibility = Visibility.Collapsed;
                    RefreshProjectDetail();
                    RefreshFilteredTodos();
                }
            }
        }

        #endregion

        #region 日记功能

        private string? _editingDiaryId;

        private void NavDiary_Click(object sender, RoutedEventArgs e)
        {
            // 取消导航按钮高亮
            SetActiveNavButton(null);
            
            // 隐藏其他面板
            ReviewPanel.Visibility = Visibility.Collapsed;
            ProjectPanel.Visibility = Visibility.Collapsed;
            ProjectDetailPanel.Visibility = Visibility.Collapsed;
            
            // 显示日记面板
            DiaryPanel.Visibility = Visibility.Visible;
            RefreshDiaryList();
        }

        private void CloseDiary_Click(object sender, RoutedEventArgs e)
        {
            DiaryPanel.Visibility = Visibility.Collapsed;
        }

        private void RefreshDiaryList(string? searchKeyword = null)
        {
            var diaries = DataService.Instance.Diaries.ToList();
            
            // 搜索过滤
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                diaries = diaries.Where(d => d.Content.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 按天分组
            var groups = diaries
                .OrderByDescending(d => d.CreatedAt)
                .GroupBy(d => d.DateKey)
                .Select(g => new DiaryGroup
                {
                    DateKey = g.Key,
                    DateDisplay = g.First().FriendlyDateDisplay + " " + g.First().DateDisplay,
                    Entries = g.OrderByDescending(d => d.CreatedAt).ToList()
                })
                .ToList();

            DiaryList.ItemsSource = groups;

            // 更新统计
            var totalCount = DataService.Instance.Diaries.Count;
            var todayCount = DataService.Instance.GetTodayDiaryCount();
            DiaryCountText.Text = $"共 {totalCount} 条记录，今日 {todayCount} 条";

            // 空状态
            DiaryEmptyState.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void DiarySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshDiaryList(DiarySearchBox.Text);
        }

        private void DiaryInput_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Enter 提交
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SubmitDiary();
                e.Handled = true;
            }
        }

        private void DiaryInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 更新字数
            DiaryInputCount.Text = $"{DiaryInput.Text.Length} 字";
        }

        private void SubmitDiary_Click(object sender, RoutedEventArgs e)
        {
            SubmitDiary();
        }

        private void SubmitDiary()
        {
            var content = DiaryInput.Text?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            DataService.Instance.AddDiary(content);
            DiaryInput.Text = "";
            DiaryInputCount.Text = "0 字";
            RefreshDiaryList();
        }

        private void EditDiary_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string diaryId)
            {
                var diary = DataService.Instance.GetDiary(diaryId);
                if (diary == null) return;

                _editingDiaryId = diaryId;
                EditDiaryContent.Text = diary.Content;
                EditDiaryPopup.Visibility = Visibility.Visible;
            }
        }

        private void DeleteDiary_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string diaryId)
            {
                var result = MessageBox.Show("确定要删除这条日记吗？", "确认删除", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    DataService.Instance.DeleteDiary(diaryId);
                    RefreshDiaryList();
                }
            }
        }

        private void CloseEditDiaryPopup_Click(object sender, RoutedEventArgs e)
        {
            EditDiaryPopup.Visibility = Visibility.Collapsed;
            _editingDiaryId = null;
        }

        private void ConfirmEditDiary_Click(object sender, RoutedEventArgs e)
        {
            if (_editingDiaryId == null) return;

            var content = EditDiaryContent.Text?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                MessageBox.Show("日记内容不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataService.Instance.UpdateDiary(_editingDiaryId, content);
            EditDiaryPopup.Visibility = Visibility.Collapsed;
            _editingDiaryId = null;
            RefreshDiaryList();
        }

        #endregion
    }
}
