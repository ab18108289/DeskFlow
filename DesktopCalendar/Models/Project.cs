using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace DesktopCalendar.Models
{
    /// <summary>
    /// 项目模型
    /// </summary>
    public class Project : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = string.Empty;
        private string _icon = "📁";
        private string _color = "#3B82F6";
        private string? _description;
        private DateTime _createdAt = DateTime.Now;
        private DateTime _updatedAt = DateTime.Now;
        private bool _isArchived = false;
        private string? _linkedGroupId;  // 关联的分组ID

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

        public string? Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); }
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { _updatedAt = value; OnPropertyChanged(nameof(UpdatedAt)); }
        }

        /// <summary>
        /// 是否已归档
        /// </summary>
        public bool IsArchived
        {
            get => _isArchived;
            set { _isArchived = value; OnPropertyChanged(nameof(IsArchived)); }
        }

        /// <summary>
        /// 关联的分组ID（创建项目时自动创建）
        /// </summary>
        public string? LinkedGroupId
        {
            get => _linkedGroupId;
            set { _linkedGroupId = value; OnPropertyChanged(nameof(LinkedGroupId)); }
        }

        /// <summary>
        /// 项目任务列表
        /// </summary>
        public ObservableCollection<ProjectTask> Tasks { get; set; } = new ObservableCollection<ProjectTask>();

        #region 计算属性

        /// <summary>
        /// 总任务数
        /// </summary>
        public int TotalTasks => Tasks.Count;

        /// <summary>
        /// 已完成任务数
        /// </summary>
        public int CompletedTasks => Tasks.Count(t => t.IsCompleted);

        /// <summary>
        /// 完成进度文本 (如 "8/12")
        /// </summary>
        public string ProgressText => $"{CompletedTasks}/{TotalTasks}";

        /// <summary>
        /// 连续推进天数
        /// </summary>
        public int ConsecutiveDays
        {
            get
            {
                var completedDates = Tasks
                    .Where(t => t.IsCompleted && t.CompletedAt.HasValue)
                    .Select(t => t.CompletedAt!.Value.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                if (completedDates.Count == 0) return 0;

                int count = 0;
                var checkDate = DateTime.Today;

                foreach (var date in completedDates)
                {
                    if (date == checkDate || date == checkDate.AddDays(-1))
                    {
                        count++;
                        checkDate = date;
                    }
                    else
                    {
                        break;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 最近完成的任务
        /// </summary>
        public ProjectTask? LatestCompletedTask => Tasks
            .Where(t => t.IsCompleted && t.CompletedAt.HasValue)
            .OrderByDescending(t => t.CompletedAt)
            .FirstOrDefault();

        /// <summary>
        /// 成长记录（按日期分组的完成任务）
        /// </summary>
        public IEnumerable<GrowthRecord> GrowthRecords => Tasks
            .Where(t => t.IsCompleted && t.CompletedAt.HasValue)
            .GroupBy(t => t.CompletedAt!.Value.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new GrowthRecord
            {
                Date = g.Key,
                Tasks = g.OrderByDescending(t => t.CompletedAt).ToList()
            });

        #endregion

        /// <summary>
        /// 刷新计算属性
        /// </summary>
        public void RefreshStats()
        {
            OnPropertyChanged(nameof(TotalTasks));
            OnPropertyChanged(nameof(CompletedTasks));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ConsecutiveDays));
            OnPropertyChanged(nameof(LatestCompletedTask));
            OnPropertyChanged(nameof(GrowthRecords));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 项目任务
    /// </summary>
    public class ProjectTask : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = string.Empty;
        private bool _isCompleted = false;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _completedAt;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                _isCompleted = value;
                if (value && !_completedAt.HasValue)
                {
                    _completedAt = DateTime.Now;
                }
                else if (!value)
                {
                    _completedAt = null;
                }
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(CompletedAt));
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); }
        }

        public DateTime? CompletedAt
        {
            get => _completedAt;
            set { _completedAt = value; OnPropertyChanged(nameof(CompletedAt)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 成长记录（某天完成的任务）
    /// </summary>
    public class GrowthRecord
    {
        public DateTime Date { get; set; }
        public List<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();

        public string DateDisplay
        {
            get
            {
                if (Date.Date == DateTime.Today) return "今天";
                if (Date.Date == DateTime.Today.AddDays(-1)) return "昨天";
                return Date.ToString("M月d日");
            }
        }
    }
}

