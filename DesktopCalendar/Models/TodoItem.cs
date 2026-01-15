using System;
using System.ComponentModel;

namespace DesktopCalendar.Models
{
    public enum Priority
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// 自定义分组
    /// </summary>
    public class TodoGroup : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = string.Empty;
        private string _icon = "📁";
        private string _color = "#6366F1";
        private int _order = 0;
        private DateTime _createdAt = DateTime.Now;

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

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(nameof(Order)); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TodoItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = string.Empty;
        private bool _isCompleted = false;
        private Priority _priority = Priority.Low;
        private string? _groupId; // 改为分组ID（可空）
        private DateTime? _dueDate;
        private string? _dueTime;
        private string? _note;
        private DateTime _createdAt = DateTime.Now;
        private DateTime _updatedAt = DateTime.Now;
        private DateTime? _completedAt;
        private DateTime? _originalDueDate;  // 原始截止日期（顺延前）
        
        // 子任务相关
        private string? _parentId;  // 父任务ID（如果是子任务）
        private bool _isExpanded = true;  // 是否展开子任务
        private int _subTaskTotal = 0;  // 子任务总数
        private int _subTaskCompleted = 0;  // 已完成子任务数

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
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsOverdue));
            }
        }

        public Priority Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(nameof(Priority)); }
        }

        public string? GroupId
        {
            get => _groupId;
            set { _groupId = value; OnPropertyChanged(nameof(GroupId)); }
        }

        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                _dueDate = value;
                OnPropertyChanged(nameof(DueDate));
                OnPropertyChanged(nameof(IsOverdue));
            }
        }

        public string? DueTime
        {
            get => _dueTime;
            set { _dueTime = value; OnPropertyChanged(nameof(DueTime)); }
        }

        public string? Note
        {
            get => _note;
            set { _note = value; OnPropertyChanged(nameof(Note)); }
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

        public DateTime? CompletedAt
        {
            get => _completedAt;
            set { _completedAt = value; OnPropertyChanged(nameof(CompletedAt)); }
        }

        /// <summary>
        /// 原始截止日期（顺延前的日期）
        /// </summary>
        public DateTime? OriginalDueDate
        {
            get => _originalDueDate;
            set { _originalDueDate = value; OnPropertyChanged(nameof(OriginalDueDate)); OnPropertyChanged(nameof(IsPostponed)); }
        }

        /// <summary>
        /// 是否已顺延
        /// </summary>
        public bool IsPostponed
        {
            get => _originalDueDate.HasValue && _originalDueDate.Value.Date < DateTime.Today;
        }

        /// <summary>
        /// 父任务ID（如果这是一个子任务）
        /// </summary>
        public string? ParentId
        {
            get => _parentId;
            set { _parentId = value; OnPropertyChanged(nameof(ParentId)); OnPropertyChanged(nameof(IsSubTask)); }
        }

        /// <summary>
        /// 是否是子任务
        /// </summary>
        public bool IsSubTask => !string.IsNullOrEmpty(_parentId);

        /// <summary>
        /// 是否展开显示子任务
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        /// <summary>
        /// 子任务总数
        /// </summary>
        public int SubTaskTotal
        {
            get => _subTaskTotal;
            set { _subTaskTotal = value; OnPropertyChanged(nameof(SubTaskTotal)); OnPropertyChanged(nameof(SubTaskProgress)); OnPropertyChanged(nameof(HasSubTasks)); }
        }

        /// <summary>
        /// 已完成子任务数
        /// </summary>
        public int SubTaskCompleted
        {
            get => _subTaskCompleted;
            set { _subTaskCompleted = value; OnPropertyChanged(nameof(SubTaskCompleted)); OnPropertyChanged(nameof(SubTaskProgress)); }
        }

        /// <summary>
        /// 是否有子任务
        /// </summary>
        public bool HasSubTasks => _subTaskTotal > 0;

        /// <summary>
        /// 子任务进度显示（如 "2/5"）
        /// </summary>
        public string SubTaskProgress => _subTaskTotal > 0 ? $"{_subTaskCompleted}/{_subTaskTotal}" : "";

        // 计算属性：是否已逾期（考虑时间）
        public bool IsOverdue
        {
            get
            {
                if (!DueDate.HasValue || IsCompleted) return false;
                
                if (!string.IsNullOrEmpty(DueTime) && TimeSpan.TryParse(DueTime, out var time))
                {
                    var deadline = DueDate.Value.Date.Add(time);
                    return DateTime.Now > deadline;
                }
                return DueDate.Value.Date < DateTime.Today;
            }
        }
        
        // 格式化显示截止时间
        public string DueDateDisplay
        {
            get
            {
                if (!DueDate.HasValue) return "";
                var dateStr = DueDate.Value.ToString("M月d日");
                if (!string.IsNullOrEmpty(DueTime)) dateStr += $" {DueTime}";
                return dateStr;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 复盘记录模型
    /// </summary>
    public class ReviewNote
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = "Day"; // Day, Week, Month, Year
        public DateTime Date { get; set; } = DateTime.Today; // 复盘的日期
        public string Title { get; set; } = string.Empty; // 复盘标题
        public string Content { get; set; } = string.Empty; // 复盘内容
        public string? Reflection { get; set; } // 反思与总结
        public string? NextPlan { get; set; } // 下一步计划
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
