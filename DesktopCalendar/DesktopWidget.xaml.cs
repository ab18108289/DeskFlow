using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.Models;
using DesktopCalendar.Services;

namespace DesktopCalendar
{
    public partial class DesktopWidget : Window
    {
        private DateTime _currentMonth = DateTime.Now;
        private DateTime? _selectedDate;
        private DateTime? _filterDate; // 当前筛选的日期
        private Priority _selectedPriority = Priority.Low;
        private string _currentView = "Day"; // Day 或 Week
        private DispatcherTimer _clockTimer = null!;
        
        // 待办列表
        public ObservableCollection<TodoItem> FilteredTodos { get; private set; }

        public DesktopWidget()
        {
            InitializeComponent();
            FilteredTodos = new ObservableCollection<TodoItem>();
            DataContext = this;
            
            Loaded += DesktopWidget_Loaded;
            
            // 监听数据变化
            DataService.Instance.Todos.CollectionChanged += (s, e) => Dispatcher.Invoke(RefreshAll);
            
            // 监听习惯数据变化（与主界面同步）
            DataService.Instance.HabitsChanged += (s, e) => Dispatcher.Invoke(RefreshHabits);
            
            // 初始化时钟
            InitializeClock();
            
            // 默认显示今天的任务
            _filterDate = DateTime.Today;
            RefreshAll();
        }

        #region 时钟

        private void InitializeClock()
        {
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            CurrentTime.Text = now.ToString("HH:mm");
            CurrentDate.Text = now.ToString("yyyy年M月d日");
            string[] weekdays = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            CurrentWeekday.Text = weekdays[(int)now.DayOfWeek];
        }

        #endregion

        #region 窗口设置

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private void DesktopWidget_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (source != null && IsInteractiveElement(source))
            {
                return;
            }
            this.DragMove();
        }
        
        private bool IsInteractiveElement(DependencyObject element)
        {
            while (element != null)
            {
                if (element is Button || element is TextBox || element is ListBox || 
                    element is ListBoxItem || element is ScrollBar || element is ScrollViewer)
                {
                    return true;
                }
                if (element is Border border && border.Tag is DateTime)
                {
                    return true;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        #endregion

        #region 数据刷新

        private void RefreshAll()
        {
            RefreshTodoList();
            RefreshStats();
            RefreshHabits();
            RenderCalendar();
        }
        
        private void RefreshHabits()
        {
            var habits = DataService.Instance.Habits.Where(h => h.IsActive).ToList();
            
            // 使用选中的日期，如果没有选中则用今天
            var targetDate = _filterDate ?? DateTime.Today;
            var isToday = targetDate.Date == DateTime.Today;
            
            // 统一显示"当日习惯"
            HabitDateLabel.Text = "当日习惯";
            
            // 更新每个习惯的打卡状态（针对选中日期）
            foreach (var habit in habits)
            {
                habit.IsCheckedToday = DataService.Instance.IsHabitChecked(habit.Id, targetDate);
            }
            
            // 选中日期需打卡的习惯（最多显示4个）
            var targetHabits = habits.Where(h => h.IsTargetDay(targetDate)).Take(4).ToList();
            WidgetHabitList.ItemsSource = targetHabits;
            
            // 统计
            var allTargetHabits = habits.Where(h => h.IsTargetDay(targetDate)).ToList();
            var checkedCount = allTargetHabits.Count(h => h.IsCheckedToday);
            var totalCount = allTargetHabits.Count;
            
            if (totalCount > 0)
            {
                HabitProgress.Text = $"{checkedCount}/{totalCount}";
                HabitSection.Visibility = Visibility.Visible;
                NoHabitsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                HabitProgress.Text = "";
                NoHabitsText.Visibility = habits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        
        private void WidgetHabitCheck_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string habitId)
            {
                // 使用选中的日期进行打卡，如果没有选中则用今天
                var targetDate = _filterDate ?? DateTime.Today;
                DataService.Instance.ToggleHabitCheck(habitId, targetDate);
                RefreshHabits();
                e.Handled = true;
            }
        }

        private void RefreshTodoList()
        {
            FilteredTodos.Clear();
            
            IEnumerable<TodoItem> todos;
            
            if (_filterDate.HasValue)
            {
                // 显示指定日期的所有任务（包括已完成和未完成）
                todos = DataService.Instance.Todos
                    .Where(t => t.DueDate?.Date == _filterDate.Value.Date)
                    .OrderBy(t => t.IsCompleted)
                    .ThenByDescending(t => t.Priority)
                    .ThenByDescending(t => t.CreatedAt);
            }
            else
            {
                // 显示所有未完成任务
                todos = DataService.Instance.Todos
                    .Where(t => !t.IsCompleted)
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.DueDate)
                    .ThenByDescending(t => t.CreatedAt);
            }

            foreach (var todo in todos)
            {
                FilteredTodos.Add(todo);
            }

            // 更新标题
            UpdateTodoListTitle();

            // 更新徽章和空状态
            var pendingCount = DataService.Instance.Todos.Count(t => !t.IsCompleted);
            TodoCountBadge.Text = pendingCount.ToString();
            EmptyTodoState.Visibility = FilteredTodos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TodoListBox.Visibility = FilteredTodos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private void UpdateTodoListTitle()
        {
            if (_filterDate.HasValue)
            {
                var date = _filterDate.Value;
                if (date.Date == DateTime.Today)
                {
                    TodoListTitle.Text = "今日任务";
                }
                else if (date.Date == DateTime.Today.AddDays(1))
                {
                    TodoListTitle.Text = "明日任务";
                }
                else
                {
                    TodoListTitle.Text = $"{date:M月d日}";
                }
            }
            else
            {
                TodoListTitle.Text = "全部待办";
            }
        }

        private void RefreshStats()
        {
            var todos = DataService.Instance.Todos;
            var today = DateTime.Today;

            StatToday.Text = todos.Count(t => (t.DueDate?.Date == today || t.DueDate == null) && !t.IsCompleted).ToString();
            StatOverdue.Text = todos.Count(t => t.DueDate?.Date < today && !t.IsCompleted).ToString();
            StatCompleted.Text = todos.Count(t => t.IsCompleted).ToString();
        }

        #endregion

        #region 快速添加

        private void QuickTodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var title = QuickTodoInput.Text.Trim();
                if (!string.IsNullOrEmpty(title))
                {
                    // 使用当前选中的日期，如果没有选中则使用今天
                    var dueDate = _filterDate ?? DateTime.Today;
                    DataService.Instance.AddTodo(title, Priority.Low, dueDate);
                    QuickTodoInput.Clear();
                    RefreshAll();
                }
            }
        }

        #endregion

        #region 待办操作

        private void CompleteTodo_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string id)
            {
                DataService.Instance.ToggleComplete(id);
                RefreshAll();
                e.Handled = true;
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

        private void PostponeTask_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string id)
            {
                DataService.Instance.PostponeToToday(id);
                RefreshAll();
                e.Handled = true;
            }
        }

        #endregion

        #region 视图切换

        private void SwitchView_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string view)
            {
                _currentView = view;
                UpdateViewButtons();
                
                if (view == "Week")
                {
                    // 周视图：显示本周任务
                    ShowWeekTodos();
                }
                else
                {
                    // 天视图：显示选中日期任务
                    if (!_filterDate.HasValue) _filterDate = DateTime.Today;
                    RefreshTodoList();
                }
                
                RenderCalendar();
            }
        }
        
        private void ShowWeekTodos()
        {
            FilteredTodos.Clear();
            
            var today = DateTime.Today;
            var startOfWeek = GetStartOfWeek(today);
            var endOfWeek = startOfWeek.AddDays(6);
            
            var weekTodos = DataService.Instance.Todos
                .Where(t => t.DueDate?.Date >= startOfWeek && t.DueDate?.Date <= endOfWeek)
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.IsCompleted)
                .ThenByDescending(t => t.Priority);
            
            foreach (var todo in weekTodos)
            {
                FilteredTodos.Add(todo);
            }
            
            TodoListTitle.Text = "本周任务";
            
            var pendingCount = DataService.Instance.Todos.Count(t => !t.IsCompleted);
            TodoCountBadge.Text = pendingCount.ToString();
            EmptyTodoState.Visibility = FilteredTodos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TodoListBox.Visibility = FilteredTodos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return date.AddDays(-diff).Date;
        }

        private void UpdateViewButtons()
        {
            // 重置样式
            ViewDay.Background = Brushes.Transparent;
            ViewDay.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
            ViewWeek.Background = Brushes.Transparent;
            ViewWeek.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));

            // 设置选中样式
            var activeView = _currentView == "Week" ? ViewWeek : ViewDay;
            activeView.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            activeView.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
        }

        private void GoToday_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = DateTime.Now;
            _filterDate = DateTime.Today;
            RefreshAll();
        }
        
        private void ShowAllTodos_Click(object sender, RoutedEventArgs e)
        {
            _filterDate = null;
            _currentView = "Month";
            UpdateViewButtons();
            
            FilteredTodos.Clear();
            var allPending = DataService.Instance.Todos
                .Where(t => !t.IsCompleted)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.DueDate);
            
            foreach (var todo in allPending)
            {
                FilteredTodos.Add(todo);
            }
            
            TodoListTitle.Text = "全部待办";
            var pendingCount = DataService.Instance.Todos.Count(t => !t.IsCompleted);
            TodoCountBadge.Text = pendingCount.ToString();
            EmptyTodoState.Visibility = FilteredTodos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TodoListBox.Visibility = FilteredTodos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            RenderCalendar();
        }

        #endregion

        #region 日历渲染

        private void RenderCalendar()
        {
            CalendarGrid.Children.Clear();
            
            if (_currentView == "Week")
            {
                RenderWeekView();
            }
            else
            {
                RenderMonthView();
            }
        }
        
        private void RenderMonthView()
        {
            // 设置为6行7列
            CalendarGrid.Rows = 6;
            
            MonthLabel.Text = $"{_currentMonth.Year}年{_currentMonth.Month}月";

            var firstDay = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int startOffset = ((int)firstDay.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

            var prevMonth = _currentMonth.AddMonths(-1);
            int daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

            for (int i = 0; i < 42; i++)
            {
                DateTime cellDate;
                bool isCurrentMonth;

                if (i < startOffset)
                {
                    cellDate = new DateTime(prevMonth.Year, prevMonth.Month, daysInPrevMonth - startOffset + 1 + i);
                    isCurrentMonth = false;
                }
                else if (i >= startOffset + daysInMonth)
                {
                    var nextMonth = _currentMonth.AddMonths(1);
                    cellDate = new DateTime(nextMonth.Year, nextMonth.Month, i - startOffset - daysInMonth + 1);
                    isCurrentMonth = false;
                }
                else
                {
                    cellDate = new DateTime(_currentMonth.Year, _currentMonth.Month, i - startOffset + 1);
                    isCurrentMonth = true;
                }

                var cell = CreateDateCell(cellDate, isCurrentMonth, false);
                CalendarGrid.Children.Add(cell);
            }
        }
        
        private void RenderWeekView()
        {
            // 设置为1行7列，但每个格子更大
            CalendarGrid.Rows = 1;
            
            var startOfWeek = GetStartOfWeek(_currentMonth);
            MonthLabel.Text = $"{startOfWeek:M月d日} - {startOfWeek.AddDays(6):M月d日}";

            for (int i = 0; i < 7; i++)
            {
                var cellDate = startOfWeek.AddDays(i);
                var cell = CreateWeekDayCell(cellDate);
                CalendarGrid.Children.Add(cell);
            }
        }
        
        private Border CreateWeekDayCell(DateTime date)
        {
            bool isToday = date.Date == DateTime.Today;
            bool isSelected = _filterDate.HasValue && date.Date == _filterDate.Value.Date;
            bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            
            var todosOnDate = DataService.Instance.Todos
                .Where(t => t.DueDate?.Date == date.Date)
                .OrderBy(t => t.IsCompleted)
                .ThenByDescending(t => t.Priority)
                .ToList();

            // 背景颜色
            Brush bgBrush = new SolidColorBrush(Color.FromArgb(8, 255, 255, 255));
            if (isSelected)
            {
                bgBrush = new SolidColorBrush(Color.FromArgb(30, 139, 92, 246));
            }
            else if (isToday)
            {
                bgBrush = new SolidColorBrush(Color.FromArgb(20, 59, 130, 246));
            }

            var cell = new Border
            {
                Tag = date,
                Cursor = Cursors.Hand,
                Margin = new Thickness(2),
                Background = bgBrush,
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(139, 92, 246)) 
                            : isToday ? new SolidColorBrush(Color.FromArgb(80, 59, 130, 246)) 
                            : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 2 : isToday ? 1.5 : 0),
                CornerRadius = new CornerRadius(8)
            };

            var mainStack = new StackPanel { Margin = new Thickness(8, 6, 8, 6) };

            // 日期头部
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            
            string[] weekdays = { "日", "一", "二", "三", "四", "五", "六" };
            var weekdayText = new TextBlock
            {
                Text = $"周{weekdays[(int)date.DayOfWeek]}",
                FontSize = 11,
                Foreground = isWeekend ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) : new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                Margin = new Thickness(0, 0, 6, 0)
            };
            headerStack.Children.Add(weekdayText);
            
            var dateText = new TextBlock
            {
                Text = date.Day.ToString(),
                FontSize = 16,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isToday ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) : Brushes.White
            };
            headerStack.Children.Add(dateText);
            
            mainStack.Children.Add(headerStack);

            // 任务列表（周视图显示更多任务）- 隐藏滚动条但保留滚动功能
            var scrollViewer = new ScrollViewer 
            { 
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                MaxHeight = 280,
                Padding = new Thickness(0, 0, 0, 0)
            };
            var todoStack = new StackPanel();
            
            foreach (var todo in todosOnDate.Take(8))
            {
                var todoItem = new Border
                {
                    Background = todo.IsCompleted 
                        ? new SolidColorBrush(Color.FromArgb(30, 34, 197, 94))
                        : GetPriorityBrush(todo.Priority),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 2, 0, 0),
                    Padding = new Thickness(6, 4, 6, 4)
                };
                
                var todoText = new TextBlock
                {
                    Text = todo.Title,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextDecorations = todo.IsCompleted ? TextDecorations.Strikethrough : null,
                    Opacity = todo.IsCompleted ? 0.6 : 1
                };
                todoItem.Child = todoText;
                todoStack.Children.Add(todoItem);
            }
            
            // 如果有更多任务
            if (todosOnDate.Count > 8)
            {
                var moreText = new TextBlock
                {
                    Text = $"+{todosOnDate.Count - 8} 更多",
                    Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                    FontSize = 10,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                todoStack.Children.Add(moreText);
            }
            
            scrollViewer.Content = todoStack;
            mainStack.Children.Add(scrollViewer);

            cell.Child = mainStack;

            // 点击选中
            cell.MouseLeftButtonUp += (s, e) =>
            {
                _filterDate = date;
                ShowWeekTodos();
                RefreshHabits(); // 刷新习惯显示对应日期
                RenderCalendar();
                e.Handled = true;
            };

            return cell;
        }

        private Border CreateDateCell(DateTime date, bool isCurrentMonth, bool isWeekView = false)
        {
            bool isToday = date.Date == DateTime.Today;
            bool isSelected = _filterDate.HasValue && date.Date == _filterDate.Value.Date;
            bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            
            var todosOnDate = DataService.Instance.Todos
                .Where(t => t.DueDate?.Date == date.Date && !t.IsCompleted)
                .OrderByDescending(t => t.Priority)
                .Take(3)
                .ToList();

            // 背景和边框颜色
            Brush bgBrush = Brushes.Transparent;
            Brush borderBrush = Brushes.Transparent;
            double borderWidth = 0;
            
            if (isSelected)
            {
                bgBrush = new SolidColorBrush(Color.FromArgb(30, 139, 92, 246));
                borderBrush = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                borderWidth = 2;
            }
            else if (isToday)
            {
                bgBrush = new SolidColorBrush(Color.FromArgb(20, 59, 130, 246));
                borderBrush = new SolidColorBrush(Color.FromArgb(80, 59, 130, 246));
                borderWidth = 1.5;
            }

            var cell = new Border
            {
                Tag = date,
                Cursor = Cursors.Hand,
                Margin = new Thickness(1),
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(borderWidth),
                CornerRadius = new CornerRadius(6)
            };

            var mainStack = new StackPanel { Margin = new Thickness(4, 3, 4, 3) };

            // 日期行
            var dateRow = new Grid();
            
            var dateText = new TextBlock
            {
                Text = date.Day.ToString(),
                FontSize = 14,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isToday 
                    ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
                    : isCurrentMonth 
                        ? (isWeekend ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) : Brushes.White)
                        : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
            };
            dateRow.Children.Add(dateText);

            // 待办数量指示器
            if (todosOnDate.Count > 0)
            {
                var indicator = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    CornerRadius = new CornerRadius(8),
                    Width = 16,
                    Height = 16,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top
                };
                indicator.Child = new TextBlock
                {
                    Text = todosOnDate.Count.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dateRow.Children.Add(indicator);
            }

            mainStack.Children.Add(dateRow);

            // 节日/农历
            string lunarText = GetSimpleLunar(date);
            var lunarLabel = new TextBlock
            {
                Text = lunarText,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                Margin = new Thickness(0, 1, 0, 2)
            };
            mainStack.Children.Add(lunarLabel);

            // 显示待办事项
            foreach (var todo in todosOnDate)
            {
                var todoBar = new Border
                {
                    Background = GetPriorityBrush(todo.Priority),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(4, 2, 4, 2)
                };
                todoBar.Child = new TextBlock
                {
                    Text = todo.Title,
                    Foreground = Brushes.White,
                    FontSize = 9,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                mainStack.Children.Add(todoBar);
            }

            cell.Child = mainStack;

            // 悬停效果
            var originalBg = cell.Background;
            cell.MouseEnter += (s, e) =>
            {
                if (!isToday && !isSelected)
                    cell.Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
            };
            cell.MouseLeave += (s, e) =>
            {
                if (!isToday && !isSelected)
                    cell.Background = originalBg;
            };

            // 点击日期显示当天任务
            cell.MouseLeftButtonUp += (s, e) =>
            {
                _filterDate = date;
                RefreshTodoList();
                RefreshHabits(); // 刷新习惯显示对应日期
                RenderCalendar(); // 重新渲染以更新选中状态
                e.Handled = true;
            };

            return cell;
        }

        private string GetSimpleLunar(DateTime date)
        {
            // 公历节日
            if (date.Month == 1 && date.Day == 1) return "元旦";
            if (date.Month == 2 && date.Day == 14) return "情人节";
            if (date.Month == 3 && date.Day == 8) return "妇女节";
            if (date.Month == 4 && date.Day == 1) return "愚人节";
            if (date.Month == 5 && date.Day == 1) return "劳动节";
            if (date.Month == 5 && date.Day == 4) return "青年节";
            if (date.Month == 6 && date.Day == 1) return "儿童节";
            if (date.Month == 7 && date.Day == 1) return "建党节";
            if (date.Month == 8 && date.Day == 1) return "建军节";
            if (date.Month == 9 && date.Day == 10) return "教师节";
            if (date.Month == 10 && date.Day == 1) return "国庆节";
            if (date.Month == 12 && date.Day == 25) return "圣诞节";
            
            // 简化农历（实际应用中需要完整的农历算法）
            string[] lunarDays = { "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
                                   "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
                                   "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十" };
            return lunarDays[(date.Day - 1) % 30];
        }

        private Brush GetPriorityBrush(Priority priority)
        {
            return priority switch
            {
                Priority.High => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Priority.Medium => new SolidColorBrush(Color.FromRgb(249, 115, 22)),
                _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))
            };
        }

        #endregion

        #region 日历导航

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            if (_currentView == "Week")
            {
                _currentMonth = _currentMonth.AddDays(-7);
                ShowWeekTodos();
            }
            else
            {
                _currentMonth = _currentMonth.AddMonths(-1);
            }
            RenderCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            if (_currentView == "Week")
            {
                _currentMonth = _currentMonth.AddDays(7);
                ShowWeekTodos();
            }
            else
            {
                _currentMonth = _currentMonth.AddMonths(1);
            }
            RenderCalendar();
        }

        #endregion

        #region 待办弹窗

        private void AddTodo_Click(object sender, RoutedEventArgs e)
        {
            // 使用当前选中的日期，如果没有选中则使用今天
            _selectedDate = _filterDate ?? DateTime.Today;
            
            // 更新弹窗标题显示选中的日期
            if (_selectedDate.Value.Date == DateTime.Today)
            {
                PopupTitle.Text = "添加今日待办";
            }
            else if (_selectedDate.Value.Date == DateTime.Today.AddDays(1))
            {
                PopupTitle.Text = "添加明日待办";
            }
            else
            {
                PopupTitle.Text = $"添加 {_selectedDate.Value:M月d日} 待办";
            }
            
            AddTodoPopup.Visibility = Visibility.Visible;
            TodoInput.Focus();
        }

        private void ClosePopup_Click(object sender, RoutedEventArgs e)
        {
            AddTodoPopup.Visibility = Visibility.Collapsed;
            TodoInput.Clear();
            _selectedPriority = Priority.Low;
            UpdatePriorityButtons();
        }

        private void Priority_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is string priorityStr && Enum.TryParse<Priority>(priorityStr, out var priority))
            {
                _selectedPriority = priority;
                UpdatePriorityButtons();
            }
        }

        private void UpdatePriorityButtons()
        {
            HighPriorityBtn.Background = Brushes.Transparent;
            MediumPriorityBtn.Background = Brushes.Transparent;

            switch (_selectedPriority)
            {
                case Priority.High:
                    HighPriorityBtn.Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68));
                    break;
                case Priority.Medium:
                    MediumPriorityBtn.Background = new SolidColorBrush(Color.FromArgb(40, 249, 115, 22));
                    break;
            }
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SubmitTodo();
            else if (e.Key == Key.Escape)
                ClosePopup_Click(sender, e);
        }

        private void SubmitTodo_Click(object sender, RoutedEventArgs e) => SubmitTodo();

        private void SubmitTodo()
        {
            var title = TodoInput.Text.Trim();
            if (!string.IsNullOrEmpty(title))
            {
                DataService.Instance.AddTodo(title, _selectedPriority, _selectedDate);
                RefreshAll();
            }

            AddTodoPopup.Visibility = Visibility.Collapsed;
            TodoInput.Clear();
            _selectedPriority = Priority.Low;
            UpdatePriorityButtons();
        }

        #endregion

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowMainWindow();
        }
        
        private void OpenMainWindow_Click(object sender, MouseButtonEventArgs e)
        {
            ((App)Application.Current).ShowMainWindow();
            e.Handled = true;
        }
        
        private double _currentOpacity = 0.55;
        private Color _currentThemeColor = Colors.Black;
        
        private void SetOpacity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string val && double.TryParse(val, out var opacity))
            {
                _currentOpacity = opacity;
                UpdateBackground();
            }
        }
        
        private void SetTheme_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string theme)
            {
                _currentThemeColor = theme switch
                {
                    "Blue" => Color.FromRgb(15, 40, 70),
                    "Purple" => Color.FromRgb(35, 20, 55),
                    "Green" => Color.FromRgb(15, 40, 30),
                    _ => Colors.Black // Dark
                };
                UpdateBackground();
            }
        }
        
        private void UpdateBackground()
        {
            WidgetBorder.Background = new SolidColorBrush(_currentThemeColor) { Opacity = _currentOpacity };
        }

        protected override void OnClosed(EventArgs e)
        {
            _clockTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
