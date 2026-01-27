using System;
using System.ComponentModel;

namespace DesktopCalendar.Models
{
    /// <summary>
    /// 日记条目 - 记录每日想法
    /// </summary>
    public class DiaryEntry : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _content = "";
        private string? _mood;
        private DateTime _createdAt = DateTime.Now;
        private DateTime _updatedAt = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        /// <summary>
        /// 日记内容（支持长文本）
        /// </summary>
        public string Content
        {
            get => _content;
            set 
            { 
                _content = value; 
                OnPropertyChanged(nameof(Content));
                OnPropertyChanged(nameof(Preview));
                OnPropertyChanged(nameof(WordCount));
            }
        }

        /// <summary>
        /// 心情图标（可选）
        /// </summary>
        public string? Mood
        {
            get => _mood;
            set { _mood = value; OnPropertyChanged(nameof(Mood)); }
        }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set 
            { 
                _createdAt = value; 
                OnPropertyChanged(nameof(CreatedAt));
                OnPropertyChanged(nameof(DateKey));
                OnPropertyChanged(nameof(TimeDisplay));
                OnPropertyChanged(nameof(DateDisplay));
            }
        }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { _updatedAt = value; OnPropertyChanged(nameof(UpdatedAt)); }
        }

        #region 计算属性

        /// <summary>
        /// 日期分组键（yyyy-MM-dd）
        /// </summary>
        public string DateKey => CreatedAt.ToString("yyyy-MM-dd");

        /// <summary>
        /// 时间显示（HH:mm）
        /// </summary>
        public string TimeDisplay => CreatedAt.ToString("HH:mm");

        /// <summary>
        /// 日期显示（M月d日 dddd）
        /// </summary>
        public string DateDisplay => CreatedAt.ToString("M月d日 dddd");

        /// <summary>
        /// 内容预览（最多显示100字符）
        /// </summary>
        public string Preview
        {
            get
            {
                if (string.IsNullOrEmpty(Content)) return "";
                var text = Content.Replace("\r\n", " ").Replace("\n", " ");
                return text.Length > 100 ? text.Substring(0, 100) + "..." : text;
            }
        }

        /// <summary>
        /// 字数统计
        /// </summary>
        public int WordCount => Content?.Length ?? 0;

        /// <summary>
        /// 是否是今天的日记
        /// </summary>
        public bool IsToday => CreatedAt.Date == DateTime.Today;

        /// <summary>
        /// 友好的日期显示
        /// </summary>
        public string FriendlyDateDisplay
        {
            get
            {
                var today = DateTime.Today;
                var date = CreatedAt.Date;
                
                if (date == today) return "今天";
                if (date == today.AddDays(-1)) return "昨天";
                if (date == today.AddDays(-2)) return "前天";
                if (date.Year == today.Year) return CreatedAt.ToString("M月d日 dddd");
                return CreatedAt.ToString("yyyy年M月d日 dddd");
            }
        }

        #endregion
    }

    /// <summary>
    /// 日记分组（按天）
    /// </summary>
    public class DiaryGroup
    {
        public string DateKey { get; set; } = "";
        public string DateDisplay { get; set; } = "";
        public System.Collections.Generic.List<DiaryEntry> Entries { get; set; } = new();
    }
}
