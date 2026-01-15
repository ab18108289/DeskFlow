# DeskFlow - 桌面效率工具

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows-blue?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/Framework-.NET%208-purple?style=flat-square" alt="Framework">
  <img src="https://img.shields.io/badge/License-MIT-green?style=flat-square" alt="License">
</p>

一款简洁美观的 Windows 桌面任务管理工具，帮助你高效管理每日待办事项。

## ✨ 功能特色

### 📋 任务管理
- 快速添加待办事项
- 支持设置优先级（高/中/低）
- 支持设置截止日期
- 子任务支持
- 任务分类管理

### 📊 数据统计
- 今日/本周/本月/全年统计
- 完成趋势图表
- 效率洞察分析
- 分类完成占比

### 🎨 界面设计
- 现代深色主题
- 多种主题颜色可选
- 流畅的动画效果
- 桌面小部件

### ⌨️ 快捷操作
- `~` 键：显示/隐藏桌面小部件
- 系统托盘常驻
- 开机自启动（可选）

## 📥 下载安装

### 方式一：直接下载
从 [Releases](../../releases) 页面下载最新版本的 `DeskFlow_v1.0.0_Windows_x64.zip`，解压后运行 `DesktopCalendar.exe` 即可。

### 方式二：从源码构建
```bash
git clone https://github.com/YOUR_USERNAME/DeskFlow.git
cd DeskFlow/DesktopCalendar
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 🖥️ 系统要求
- Windows 10/11 (64位)
- 无需安装 .NET 运行时（已内置）

## 📸 截图

| 主界面 | 数据统计 |
|--------|----------|
| 简洁的任务列表 | 多维度数据分析 |

## 🛠️ 技术栈
- WPF (.NET 8)
- MVVM 架构
- 本地 JSON 数据存储

## 📝 开发计划
- [ ] 云同步功能
- [ ] 番茄钟
- [ ] 日历视图
- [ ] 数据导出
- [ ] 多语言支持

## 🤝 贡献
欢迎提交 Issue 和 Pull Request！

## 📄 许可证
MIT License

## ☕ 支持作者
如果这个项目对你有帮助，欢迎请作者喝杯咖啡~

---

**DeskFlow** - 让每一天都井井有条 ✨

