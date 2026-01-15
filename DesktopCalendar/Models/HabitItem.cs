using System;
using System.ComponentModel;

namespace DesktopCalendar.Models
{
    /// <summary>
    /// 习惯打卡频率
    /// </summary>
    public enum HabitFrequency
    {
        Daily,      // 每日
        Weekdays,   // 工作日
        Weekends,   // 周末
        Custom      // 自定义
    }

    /// <summary>
    /// 习惯项
    /// </summary>
    public class HabitItem : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _icon = "✨";
        private string _color = "#3B82F6";
        private bool _isActive = true;
        private HabitFrequency _frequency = HabitFrequency.Daily;
        private string _targetDays = "1,2,3,4,5,6,0"; // 周一到周日
        private DateTime _createdAt = DateTime.Now;
        private int _currentStreak = 0;
        private int _longestStreak = 0;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(nameof(Icon)); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(nameof(Color)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public HabitFrequency Frequency
        {
            get => _frequency;
            set { _frequency = value; OnPropertyChanged(nameof(Frequency)); }
        }

        /// <summary>
        /// 目标打卡日，逗号分隔的数字 (0=周日, 1=周一, ...)
        /// </summary>
        public string TargetDays
        {
            get => _targetDays;
            set { _targetDays = value; OnPropertyChanged(nameof(TargetDays)); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); }
        }

        /// <summary>
        /// 当前连续打卡天数
        /// </summary>
        public int CurrentStreak
        {
            get => _currentStreak;
            set { _currentStreak = value; OnPropertyChanged(nameof(CurrentStreak)); }
        }

        /// <summary>
        /// 最长连续打卡天数
        /// </summary>
        public int LongestStreak
        {
            get => _longestStreak;
            set { _longestStreak = value; OnPropertyChanged(nameof(LongestStreak)); }
        }

        private bool _isCheckedToday;
        /// <summary>
        /// 今天是否已打卡（用于UI绑定，运行时更新）
        /// </summary>
        public bool IsCheckedToday
        {
            get => _isCheckedToday;
            set { _isCheckedToday = value; OnPropertyChanged(nameof(IsCheckedToday)); }
        }

        /// <summary>
        /// 检查指定日期是否是目标打卡日
        /// </summary>
        public bool IsTargetDay(DateTime date)
        {
            if (Frequency == HabitFrequency.Daily) return true;
            if (Frequency == HabitFrequency.Weekdays)
                return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
            if (Frequency == HabitFrequency.Weekends)
                return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

            // Custom
            var days = TargetDays.Split(',');
            var dayNum = ((int)date.DayOfWeek).ToString();
            return Array.Exists(days, d => d.Trim() == dayNum);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 习惯打卡记录
    /// </summary>
    public class HabitRecord : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _habitId = string.Empty;
        private DateTime _date;
        private bool _isCompleted;
        private DateTime? _completedAt;
        private string _note = string.Empty;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string HabitId
        {
            get => _habitId;
            set { _habitId = value; OnPropertyChanged(nameof(HabitId)); }
        }

        public DateTime Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(nameof(Date)); }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); }
        }

        public DateTime? CompletedAt
        {
            get => _completedAt;
            set { _completedAt = value; OnPropertyChanged(nameof(CompletedAt)); }
        }

        public string Note
        {
            get => _note;
            set { _note = value; OnPropertyChanged(nameof(Note)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

