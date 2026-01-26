using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DesktopCalendar.Models;
using Newtonsoft.Json;

namespace DesktopCalendar.Services
{
    public class DataService
    {
        private static DataService? _instance;
        public static DataService Instance => _instance ??= new DataService();
        
        /// <summary>
        /// 习惯数据变化事件（用于同步桌面小部件和主界面）
        /// </summary>
        public event EventHandler? HabitsChanged;

        private readonly string _dataPath;
        private readonly string _reviewPath;
        private readonly string _habitsPath;
        private readonly string _habitRecordsPath;
        private readonly string _groupsPath;
        private readonly string _projectsPath;
        private readonly string _backupFolder;
        
        public ObservableCollection<TodoItem> Todos { get; private set; }
        public ObservableCollection<ReviewNote> Reviews { get; private set; }
        public ObservableCollection<HabitItem> Habits { get; private set; }
        public ObservableCollection<HabitRecord> HabitRecords { get; private set; }
        public ObservableCollection<TodoGroup> Groups { get; private set; }
        public ObservableCollection<Project> Projects { get; private set; }
        
        public event EventHandler? GroupsChanged;
        public event EventHandler? ProjectsChanged;

        private DataService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "DesktopCalendar");
            Directory.CreateDirectory(appFolder);
            _dataPath = Path.Combine(appFolder, "todos.json");
            _reviewPath = Path.Combine(appFolder, "reviews.json");
            _habitsPath = Path.Combine(appFolder, "habits.json");
            _habitRecordsPath = Path.Combine(appFolder, "habit_records.json");
            _groupsPath = Path.Combine(appFolder, "groups.json");
            _projectsPath = Path.Combine(appFolder, "projects.json");
            _backupFolder = Path.Combine(appFolder, "backups");
            Directory.CreateDirectory(_backupFolder);
            
            Todos = new ObservableCollection<TodoItem>();
            Reviews = new ObservableCollection<ReviewNote>();
            Habits = new ObservableCollection<HabitItem>();
            HabitRecords = new ObservableCollection<HabitRecord>();
            Groups = new ObservableCollection<TodoGroup>();
            Projects = new ObservableCollection<Project>();
            
            Load();
            LoadReviews();
            LoadHabits();
            LoadProjects();
            LoadHabitRecords();
            LoadGroups();
            
            // 同步分类和分组（确保每个分类都有对应的分组）
            SyncProjectsAndGroups();
            
            // 刷新所有子任务进度
            RefreshAllSubTaskProgress();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_dataPath))
                {
                    var json = File.ReadAllText(_dataPath);
                    var items = JsonConvert.DeserializeObject<ObservableCollection<TodoItem>>(json);
                    if (items != null)
                    {
                        Todos.Clear();
                        foreach (var item in items)
                        {
                            Todos.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load error: {ex.Message}");
            }
        }

        public void Save(bool notifyCloud = true)
        {
            try
            {
                var json = JsonConvert.SerializeObject(Todos, Formatting.Indented);
                File.WriteAllText(_dataPath, json);
                
                // 通知云服务数据已变更
                if (notifyCloud)
                {
                    CloudService.Instance.NotifyDataChanged();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建完整备份（在云端同步前调用）
        /// </summary>
        public string CreateFullBackup()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupDir = Path.Combine(_backupFolder, timestamp);
                Directory.CreateDirectory(backupDir);

                // 备份所有数据文件
                if (File.Exists(_dataPath))
                    File.Copy(_dataPath, Path.Combine(backupDir, "todos.json"), true);
                if (File.Exists(_groupsPath))
                    File.Copy(_groupsPath, Path.Combine(backupDir, "groups.json"), true);
                if (File.Exists(_projectsPath))
                    File.Copy(_projectsPath, Path.Combine(backupDir, "projects.json"), true);
                if (File.Exists(_reviewPath))
                    File.Copy(_reviewPath, Path.Combine(backupDir, "reviews.json"), true);

                // 只保留最近 10 个备份
                CleanOldBackups(10);

                return backupDir;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup error: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 从备份恢复数据
        /// </summary>
        public bool RestoreFromBackup(string backupDir)
        {
            try
            {
                if (!Directory.Exists(backupDir)) return false;

                var todosBackup = Path.Combine(backupDir, "todos.json");
                var groupsBackup = Path.Combine(backupDir, "groups.json");
                var projectsBackup = Path.Combine(backupDir, "projects.json");
                var reviewsBackup = Path.Combine(backupDir, "reviews.json");

                if (File.Exists(todosBackup))
                    File.Copy(todosBackup, _dataPath, true);
                if (File.Exists(groupsBackup))
                    File.Copy(groupsBackup, _groupsPath, true);
                if (File.Exists(projectsBackup))
                    File.Copy(projectsBackup, _projectsPath, true);
                if (File.Exists(reviewsBackup))
                    File.Copy(reviewsBackup, _reviewPath, true);

                // 重新加载数据
                Load();
                LoadGroups();
                LoadProjects();
                LoadReviews();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Restore error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有备份列表
        /// </summary>
        public List<string> GetBackupList()
        {
            try
            {
                if (!Directory.Exists(_backupFolder)) return new List<string>();
                return Directory.GetDirectories(_backupFolder)
                    .OrderByDescending(d => d)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 清理旧备份
        /// </summary>
        private void CleanOldBackups(int keepCount)
        {
            try
            {
                var backups = Directory.GetDirectories(_backupFolder)
                    .OrderByDescending(d => d)
                    .Skip(keepCount)
                    .ToList();

                foreach (var backup in backups)
                {
                    Directory.Delete(backup, true);
                }
            }
            catch { }
        }

        public void AddTodo(string title, Priority priority = Priority.Low, DateTime? dueDate = null, string? groupId = null)
        {
            var todo = new TodoItem
            {
                Title = title,
                Priority = priority,
                DueDate = dueDate,
                GroupId = groupId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            Todos.Insert(0, todo);
            Save();
        }

        public void UpdateTodo(string id, string title, Priority priority, DateTime? dueDate, string? dueTime, string? note, string? groupId = null)
        {
            var todo = Todos.FirstOrDefault(t => t.Id == id);
            if (todo != null)
            {
                todo.Title = title;
                todo.Priority = priority;
                todo.DueDate = dueDate;
                todo.DueTime = dueTime;
                todo.Note = note;
                todo.GroupId = groupId;
                todo.UpdatedAt = DateTime.Now;
                Save();
            }
        }

        public TodoItem? GetTodo(string id)
        {
            return Todos.FirstOrDefault(t => t.Id == id);
        }

        public void DeleteTodo(string id)
        {
            var todo = Todos.FirstOrDefault(t => t.Id == id);
            if (todo != null)
            {
                // 同时删除所有子任务
                var subTasks = Todos.Where(t => t.ParentId == id).ToList();
                foreach (var sub in subTasks)
                {
                    Todos.Remove(sub);
                }
                Todos.Remove(todo);
                Save();
                
                // 如果删除的是子任务，更新父任务的进度
                if (!string.IsNullOrEmpty(todo.ParentId))
                {
                    UpdateSubTaskProgress(todo.ParentId);
                }
            }
        }

        #region 子任务管理

        /// <summary>
        /// 添加子任务
        /// </summary>
        public void AddSubTask(string parentId, string title)
        {
            var parent = Todos.FirstOrDefault(t => t.Id == parentId);
            if (parent == null) return;

            var subTask = new TodoItem
            {
                Title = title,
                ParentId = parentId,
                Priority = parent.Priority, // 继承父任务优先级
                GroupId = parent.GroupId,   // 继承父任务分组
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            
            // 插入到父任务后面
            var parentIndex = Todos.IndexOf(parent);
            Todos.Insert(parentIndex + 1, subTask);
            Save();
            
            UpdateSubTaskProgress(parentId);
        }

        /// <summary>
        /// 获取任务的所有子任务
        /// </summary>
        public IEnumerable<TodoItem> GetSubTasks(string parentId)
        {
            return Todos.Where(t => t.ParentId == parentId).OrderBy(t => t.CreatedAt);
        }

        /// <summary>
        /// 更新父任务的子任务进度
        /// </summary>
        public void UpdateSubTaskProgress(string parentId)
        {
            var parent = Todos.FirstOrDefault(t => t.Id == parentId);
            if (parent == null) return;

            var subTasks = Todos.Where(t => t.ParentId == parentId).ToList();
            parent.SubTaskTotal = subTasks.Count;
            parent.SubTaskCompleted = subTasks.Count(t => t.IsCompleted);
            Save();
        }

        /// <summary>
        /// 更新所有任务的子任务进度（加载时调用）
        /// </summary>
        public void RefreshAllSubTaskProgress()
        {
            var parentIds = Todos.Where(t => !string.IsNullOrEmpty(t.ParentId))
                                 .Select(t => t.ParentId!)
                                 .Distinct();
            
            foreach (var parentId in parentIds)
            {
                var parent = Todos.FirstOrDefault(t => t.Id == parentId);
                if (parent != null)
                {
                    var subTasks = Todos.Where(t => t.ParentId == parentId).ToList();
                    parent.SubTaskTotal = subTasks.Count;
                    parent.SubTaskCompleted = subTasks.Count(t => t.IsCompleted);
                }
            }
        }

        #endregion

        public void ToggleComplete(string id)
        {
            var todo = Todos.FirstOrDefault(t => t.Id == id);
            if (todo != null)
            {
                todo.IsCompleted = !todo.IsCompleted;
                todo.CompletedAt = todo.IsCompleted ? DateTime.Now : null;
                todo.UpdatedAt = DateTime.Now;
                Save();
                
                // 如果是子任务，更新父任务进度
                if (!string.IsNullOrEmpty(todo.ParentId))
                {
                    UpdateSubTaskProgress(todo.ParentId);
                }
            }
        }

        /// <summary>
        /// 顺延任务到今天（保留原始截止日期，标记为已顺延）
        /// </summary>
        public void PostponeToToday(string id)
        {
            var todo = Todos.FirstOrDefault(t => t.Id == id);
            if (todo != null && todo.DueDate.HasValue && !todo.IsCompleted)
            {
                // 保存原始截止日期（如果还没保存过）
                if (!todo.OriginalDueDate.HasValue)
                {
                    todo.OriginalDueDate = todo.DueDate;
                }
                // 更新截止日期到今天
                todo.DueDate = DateTime.Today;
                todo.DueTime = null; // 清除时间
                todo.UpdatedAt = DateTime.Now;
                Save();
            }
        }

        public int GetTodoCountByDate(DateTime date)
        {
            return Todos.Count(t => t.DueDate?.Date == date.Date && !t.IsCompleted);
        }

        // 统计方法
        public int GetPendingCount() => Todos.Count(t => !t.IsCompleted);
        public int GetCompletedCount() => Todos.Count(t => t.IsCompleted);
        public int GetTodayCount() => Todos.Count(t => (t.DueDate?.Date == DateTime.Today || t.DueDate == null) && !t.IsCompleted);
        public int GetOverdueCount() => Todos.Count(t => t.DueDate?.Date < DateTime.Today && !t.IsCompleted);
        public int GetUrgentCount() => Todos.Count(t => t.Priority == Priority.High && !t.IsCompleted);

        public double GetTodayCompletionRate()
        {
            var todayTodos = Todos.Where(t => t.DueDate?.Date == DateTime.Today || 
                                               (t.CompletedAt?.Date == DateTime.Today)).ToList();
            if (todayTodos.Count == 0) return 0;
            return (double)todayTodos.Count(t => t.IsCompleted) / todayTodos.Count * 100;
        }

        // 导出功能
        public string ExportToText()
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("=== 待办事项导出 ===");
            lines.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm}");
            lines.AppendLine();

            var pending = Todos.Where(t => !t.IsCompleted).OrderByDescending(t => t.Priority);
            lines.AppendLine("【待办中】");
            foreach (var todo in pending)
            {
                var priority = todo.Priority switch { Priority.High => "紧急", Priority.Medium => "重要", _ => "普通" };
                var date = todo.DueDate?.ToString("M月d日") ?? "无日期";
                lines.AppendLine($"  □ [{priority}] {todo.Title} - {date}");
            }

            lines.AppendLine();
            var completed = Todos.Where(t => t.IsCompleted).OrderByDescending(t => t.CompletedAt);
            lines.AppendLine("【已完成】");
            foreach (var todo in completed)
            {
                lines.AppendLine($"  ✓ {todo.Title} - 完成于 {todo.CompletedAt:M月d日}");
            }

            return lines.ToString();
        }

        #region 复盘记录管理

        public void LoadReviews()
        {
            try
            {
                if (File.Exists(_reviewPath))
                {
                    var json = File.ReadAllText(_reviewPath);
                    var items = JsonConvert.DeserializeObject<ObservableCollection<ReviewNote>>(json);
                    if (items != null)
                    {
                        Reviews.Clear();
                        foreach (var item in items)
                        {
                            Reviews.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load reviews error: {ex.Message}");
            }
        }

        public void SaveReviews(bool notifyCloud = true)
        {
            try
            {
                var json = JsonConvert.SerializeObject(Reviews, Formatting.Indented);
                File.WriteAllText(_reviewPath, json);
                
                if (notifyCloud) CloudService.Instance.NotifyDataChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save reviews error: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定类型和日期的复盘记录
        /// </summary>
        public ReviewNote? GetReview(string type, DateTime date)
        {
            return type switch
            {
                "Day" => Reviews.FirstOrDefault(r => r.Type == type && r.Date.Date == date.Date),
                "Week" => Reviews.FirstOrDefault(r => r.Type == type && GetWeekStart(r.Date) == GetWeekStart(date)),
                "Month" => Reviews.FirstOrDefault(r => r.Type == type && r.Date.Year == date.Year && r.Date.Month == date.Month),
                "Year" => Reviews.FirstOrDefault(r => r.Type == type && r.Date.Year == date.Year),
                _ => null
            };
        }

        private DateTime GetWeekStart(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        /// <summary>
        /// 保存或更新复盘记录
        /// </summary>
        public void SaveReview(string type, DateTime date, string title, string content, string? reflection, string? nextPlan)
        {
            var existing = GetReview(type, date);
            
            if (existing != null)
            {
                existing.Title = title;
                existing.Content = content;
                existing.Reflection = reflection;
                existing.NextPlan = nextPlan;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                var review = new ReviewNote
                {
                    Type = type,
                    Date = date,
                    Title = title,
                    Content = content,
                    Reflection = reflection,
                    NextPlan = nextPlan,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                Reviews.Insert(0, review);
            }
            SaveReviews();
        }

        /// <summary>
        /// 获取历史复盘记录
        /// </summary>
        public IEnumerable<ReviewNote> GetReviewHistory(string type, int count = 10)
        {
            return Reviews
                .Where(r => r.Type == type)
                .OrderByDescending(r => r.Date)
                .Take(count);
        }

        #endregion

        #region 习惯管理

        public void LoadHabits()
        {
            try
            {
                if (File.Exists(_habitsPath))
                {
                    var json = File.ReadAllText(_habitsPath);
                    var items = JsonConvert.DeserializeObject<ObservableCollection<HabitItem>>(json);
                    if (items != null)
                    {
                        Habits.Clear();
                        foreach (var item in items)
                        {
                            Habits.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load habits error: {ex.Message}");
            }
        }

        public void SaveHabits()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Habits, Formatting.Indented);
                File.WriteAllText(_habitsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save habits error: {ex.Message}");
            }
        }

        public void LoadHabitRecords()
        {
            try
            {
                if (File.Exists(_habitRecordsPath))
                {
                    var json = File.ReadAllText(_habitRecordsPath);
                    var items = JsonConvert.DeserializeObject<ObservableCollection<HabitRecord>>(json);
                    if (items != null)
                    {
                        HabitRecords.Clear();
                        foreach (var item in items)
                        {
                            HabitRecords.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load habit records error: {ex.Message}");
            }
        }

        public void SaveHabitRecords()
        {
            try
            {
                var json = JsonConvert.SerializeObject(HabitRecords, Formatting.Indented);
                File.WriteAllText(_habitRecordsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save habit records error: {ex.Message}");
            }
        }

        /// <summary>
        /// 触发习惯变化事件
        /// </summary>
        private void NotifyHabitsChanged()
        {
            HabitsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 添加新习惯
        /// </summary>
        public void AddHabit(string name, string icon = "✨", string color = "#3B82F6", HabitFrequency frequency = HabitFrequency.Daily)
        {
            var habit = new HabitItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Icon = icon,
                Color = color,
                Frequency = frequency,
                CreatedAt = DateTime.Now,
                IsActive = true
            };
            Habits.Add(habit);
            SaveHabits();
            NotifyHabitsChanged();
        }

        /// <summary>
        /// 更新习惯
        /// </summary>
        public void UpdateHabit(string id, string name, string icon, string color, HabitFrequency frequency)
        {
            var habit = Habits.FirstOrDefault(h => h.Id == id);
            if (habit != null)
            {
                habit.Name = name;
                habit.Icon = icon;
                habit.Color = color;
                habit.Frequency = frequency;
                SaveHabits();
                NotifyHabitsChanged();
            }
        }

        /// <summary>
        /// 删除习惯
        /// </summary>
        public void DeleteHabit(string id)
        {
            var habit = Habits.FirstOrDefault(h => h.Id == id);
            if (habit != null)
            {
                Habits.Remove(habit);
                // 同时删除相关记录
                var records = HabitRecords.Where(r => r.HabitId == id).ToList();
                foreach (var record in records)
                {
                    HabitRecords.Remove(record);
                }
                SaveHabits();
                SaveHabitRecords();
                NotifyHabitsChanged();
            }
        }

        /// <summary>
        /// 打卡/取消打卡
        /// </summary>
        public void ToggleHabitCheck(string habitId, DateTime date)
        {
            var record = HabitRecords.FirstOrDefault(r => r.HabitId == habitId && r.Date.Date == date.Date);
            
            if (record != null)
            {
                // 取消打卡
                record.IsCompleted = !record.IsCompleted;
                record.CompletedAt = record.IsCompleted ? DateTime.Now : null;
            }
            else
            {
                // 新打卡
                record = new HabitRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    HabitId = habitId,
                    Date = date.Date,
                    IsCompleted = true,
                    CompletedAt = DateTime.Now
                };
                HabitRecords.Add(record);
            }
            
            SaveHabitRecords();
            UpdateHabitStreaks(habitId);
            NotifyHabitsChanged();
        }

        /// <summary>
        /// 检查某个习惯在某天是否已打卡
        /// </summary>
        public bool IsHabitChecked(string habitId, DateTime date)
        {
            return HabitRecords.Any(r => r.HabitId == habitId && r.Date.Date == date.Date && r.IsCompleted);
        }

        /// <summary>
        /// 获取今日需要打卡的习惯
        /// </summary>
        public IEnumerable<HabitItem> GetTodayHabits()
        {
            var today = DateTime.Today;
            return Habits.Where(h => h.IsActive && h.IsTargetDay(today));
        }

        /// <summary>
        /// 获取今日已打卡数量
        /// </summary>
        public int GetTodayCheckedCount()
        {
            var today = DateTime.Today;
            var todayHabits = GetTodayHabits().Select(h => h.Id);
            return HabitRecords.Count(r => r.Date.Date == today && r.IsCompleted && todayHabits.Contains(r.HabitId));
        }

        /// <summary>
        /// 获取今日习惯完成率
        /// </summary>
        public double GetTodayHabitRate()
        {
            var todayHabits = GetTodayHabits().ToList();
            if (todayHabits.Count == 0) return 0;
            return (double)GetTodayCheckedCount() / todayHabits.Count * 100;
        }

        /// <summary>
        /// 更新习惯的连续打卡天数
        /// </summary>
        private void UpdateHabitStreaks(string habitId)
        {
            var habit = Habits.FirstOrDefault(h => h.Id == habitId);
            if (habit == null) return;

            int currentStreak = 0;
            var checkDate = DateTime.Today;

            // 计算当前连续天数（从今天往回数）
            while (true)
            {
                // 跳过非目标日
                if (!habit.IsTargetDay(checkDate))
                {
                    checkDate = checkDate.AddDays(-1);
                    continue;
                }

                if (IsHabitChecked(habitId, checkDate))
                {
                    currentStreak++;
                    checkDate = checkDate.AddDays(-1);
                }
                else
                {
                    // 如果今天还没打卡，给一天宽限期
                    if (checkDate == DateTime.Today)
                    {
                        checkDate = checkDate.AddDays(-1);
                        continue;
                    }
                    break;
                }

                // 防止无限循环
                if ((DateTime.Today - checkDate).Days > 365) break;
            }

            habit.CurrentStreak = currentStreak;
            if (currentStreak > habit.LongestStreak)
            {
                habit.LongestStreak = currentStreak;
            }
            SaveHabits();
        }

        /// <summary>
        /// 获取习惯在指定周的打卡情况
        /// </summary>
        public Dictionary<DateTime, bool> GetWeekHabitStatus(string habitId, DateTime weekStart)
        {
            var result = new Dictionary<DateTime, bool>();
            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                result[date] = IsHabitChecked(habitId, date);
            }
            return result;
        }

        /// <summary>
        /// 获取习惯的周完成率
        /// </summary>
        public double GetWeekHabitRate(string habitId)
        {
            var habit = Habits.FirstOrDefault(h => h.Id == habitId);
            if (habit == null) return 0;

            var startOfWeek = GetWeekStart(DateTime.Today);
            int targetDays = 0;
            int checkedDays = 0;

            for (int i = 0; i < 7; i++)
            {
                var date = startOfWeek.AddDays(i);
                if (date > DateTime.Today) break; // 不计算未来的天
                
                if (habit.IsTargetDay(date))
                {
                    targetDays++;
                    if (IsHabitChecked(habitId, date))
                    {
                        checkedDays++;
                    }
                }
            }

            return targetDays == 0 ? 0 : (double)checkedDays / targetDays * 100;
        }

        /// <summary>
        /// 获取习惯的月完成率
        /// </summary>
        public double GetMonthHabitRate(string habitId)
        {
            var habit = Habits.FirstOrDefault(h => h.Id == habitId);
            if (habit == null) return 0;

            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            int targetDays = 0;
            int checkedDays = 0;

            for (var date = startOfMonth; date <= DateTime.Today; date = date.AddDays(1))
            {
                if (habit.IsTargetDay(date))
                {
                    targetDays++;
                    if (IsHabitChecked(habitId, date))
                    {
                        checkedDays++;
                    }
                }
            }

            return targetDays == 0 ? 0 : (double)checkedDays / targetDays * 100;
        }

        #endregion

        #region 分组管理

        public void LoadGroups()
        {
            try
            {
                if (File.Exists(_groupsPath))
                {
                    var json = File.ReadAllText(_groupsPath);
                    var items = JsonConvert.DeserializeObject<ObservableCollection<TodoGroup>>(json);
                    if (items != null)
                    {
                        Groups.Clear();
                        foreach (var item in items)
                        {
                            Groups.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load groups error: {ex.Message}");
            }
        }

        public void SaveGroups(bool notifyCloud = true)
        {
            try
            {
                var json = JsonConvert.SerializeObject(Groups, Formatting.Indented);
                File.WriteAllText(_groupsPath, json);
                
                if (notifyCloud) CloudService.Instance.NotifyDataChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save groups error: {ex.Message}");
            }
        }

        private void NotifyGroupsChanged()
        {
            GroupsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddGroup(string name, string icon, string color)
        {
            var group = new TodoGroup
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Icon = icon,
                Color = color,
                Order = Groups.Count,
                CreatedAt = DateTime.Now
            };
            Groups.Add(group);
            SaveGroups();
            NotifyGroupsChanged();
        }

        public void UpdateGroup(string id, string name, string icon, string color)
        {
            var group = Groups.FirstOrDefault(g => g.Id == id);
            if (group != null)
            {
                group.Name = name;
                group.Icon = icon;
                group.Color = color;
                SaveGroups();
                NotifyGroupsChanged();
            }
        }

        public void DeleteGroup(string id)
        {
            var group = Groups.FirstOrDefault(g => g.Id == id);
            if (group != null)
            {
                // 清除该分组下所有待办的分组ID
                foreach (var todo in Todos.Where(t => t.GroupId == id))
                {
                    todo.GroupId = null;
                }
                Save();
                
                Groups.Remove(group);
                SaveGroups();
                NotifyGroupsChanged();
            }
        }

        public TodoGroup? GetGroup(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Groups.FirstOrDefault(g => g.Id == id);
        }

        public string GetGroupName(string? groupId)
        {
            var group = GetGroup(groupId);
            return group != null ? $"{group.Icon} {group.Name}" : "未分组";
        }

        #endregion

        #region 项目管理

        private void LoadProjects()
        {
            try
            {
                if (File.Exists(_projectsPath))
                {
                    var json = File.ReadAllText(_projectsPath);
                    var items = JsonConvert.DeserializeObject<ObservableCollection<Project>>(json);
                    if (items != null)
                    {
                        Projects.Clear();
                        foreach (var item in items)
                        {
                            Projects.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load projects error: {ex.Message}");
            }
        }

        public void SaveProjects(bool notifyCloud = true)
        {
            try
            {
                var json = JsonConvert.SerializeObject(Projects, Formatting.Indented);
                File.WriteAllText(_projectsPath, json);
                
                if (notifyCloud) CloudService.Instance.NotifyDataChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save projects error: {ex.Message}");
            }
        }

        private void NotifyProjectsChanged()
        {
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddProject(string name, string icon = "📁", string color = "#8B5CF6", string? description = null)
        {
            // 先创建关联的分组
            var group = new TodoGroup
            {
                Name = name,
                Icon = icon,
                Color = color,
                CreatedAt = DateTime.Now
            };
            Groups.Add(group);
            SaveGroups();
            NotifyGroupsChanged();

            // 创建项目并关联分组
            var project = new Project
            {
                Name = name,
                Icon = icon,
                Color = color,
                Description = description,
                LinkedGroupId = group.Id,  // 关联分组
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            Projects.Insert(0, project);
            SaveProjects();
            NotifyProjectsChanged();
        }

        /// <summary>
        /// 同步分类和分组，确保每个分类都有对应的分组
        /// </summary>
        private void SyncProjectsAndGroups()
        {
            bool needSave = false;
            foreach (var project in Projects)
            {
                // 检查分类是否有关联的分组
                if (string.IsNullOrEmpty(project.LinkedGroupId) || 
                    !Groups.Any(g => g.Id == project.LinkedGroupId))
                {
                    // 创建对应的分组
                    var group = new TodoGroup
                    {
                        Name = project.Name,
                        Icon = project.Icon,
                        Color = project.Color,
                        CreatedAt = DateTime.Now
                    };
                    Groups.Add(group);
                    project.LinkedGroupId = group.Id;
                    needSave = true;
                }
            }
            
            if (needSave)
            {
                SaveGroups();
                SaveProjects();
            }
        }

        public Project? GetProject(string id)
        {
            return Projects.FirstOrDefault(p => p.Id == id);
        }

        public void UpdateProject(string id, string name, string icon, string color, string? description)
        {
            var project = GetProject(id);
            if (project != null)
            {
                project.Name = name;
                project.Icon = icon;
                project.Color = color;
                project.Description = description;
                project.UpdatedAt = DateTime.Now;
                SaveProjects();
                NotifyProjectsChanged();
            }
        }

        public void DeleteProject(string id, bool deleteLinkedGroup = false)
        {
            var project = GetProject(id);
            if (project != null)
            {
                // 如果需要删除关联分组
                if (deleteLinkedGroup && !string.IsNullOrEmpty(project.LinkedGroupId))
                {
                    var group = GetGroup(project.LinkedGroupId);
                    if (group != null)
                    {
                        // 清除该分组下所有待办的分组ID
                        foreach (var todo in Todos.Where(t => t.GroupId == project.LinkedGroupId))
                        {
                            todo.GroupId = null;
                        }
                        Save();
                        
                        Groups.Remove(group);
                        SaveGroups();
                        NotifyGroupsChanged();
                    }
                }

                Projects.Remove(project);
                SaveProjects();
                NotifyProjectsChanged();
            }
        }

        public void ArchiveProject(string id)
        {
            var project = GetProject(id);
            if (project != null)
            {
                project.IsArchived = true;
                project.UpdatedAt = DateTime.Now;
                SaveProjects();
                NotifyProjectsChanged();
            }
        }

        /// <summary>
        /// 添加项目任务
        /// </summary>
        public void AddProjectTask(string projectId, string title)
        {
            var project = GetProject(projectId);
            if (project != null)
            {
                var task = new ProjectTask
                {
                    Title = title,
                    CreatedAt = DateTime.Now
                };
                project.Tasks.Add(task);
                project.UpdatedAt = DateTime.Now;
                project.RefreshStats();
                SaveProjects();
                NotifyProjectsChanged();
            }
        }

        /// <summary>
        /// 切换项目任务完成状态
        /// </summary>
        public void ToggleProjectTask(string projectId, string taskId)
        {
            var project = GetProject(projectId);
            if (project != null)
            {
                var task = project.Tasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    task.IsCompleted = !task.IsCompleted;
                    project.UpdatedAt = DateTime.Now;
                    project.RefreshStats();
                    SaveProjects();
                    NotifyProjectsChanged();
                }
            }
        }

        /// <summary>
        /// 删除项目任务
        /// </summary>
        public void DeleteProjectTask(string projectId, string taskId)
        {
            var project = GetProject(projectId);
            if (project != null)
            {
                var task = project.Tasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    project.Tasks.Remove(task);
                    project.UpdatedAt = DateTime.Now;
                    project.RefreshStats();
                    SaveProjects();
                    NotifyProjectsChanged();
                }
            }
        }

        /// <summary>
        /// 获取进行中的项目数量
        /// </summary>
        public int GetActiveProjectCount()
        {
            return Projects.Count(p => !p.IsArchived);
        }

        /// <summary>
        /// 获取项目关联的待办任务
        /// </summary>
        public IEnumerable<TodoItem> GetProjectLinkedTodos(string projectId)
        {
            var project = GetProject(projectId);
            if (project == null || string.IsNullOrEmpty(project.LinkedGroupId))
            {
                return Enumerable.Empty<TodoItem>();
            }
            return Todos.Where(t => t.GroupId == project.LinkedGroupId && !t.IsSubTask);
        }

        #endregion
    }
}
