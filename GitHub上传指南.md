# DeskFlow GitHub 上传完整指南

## 📋 准备工作

### 1. 确认已安装 Git
打开 PowerShell，输入：
```powershell
git --version
```
如果显示版本号（如 `git version 2.xx.x`），说明已安装。

如果未安装，请下载安装：https://git-scm.com/download/win

### 2. 配置 Git 用户信息（首次使用需要）
```powershell
git config --global user.name "你的GitHub用户名"
git config --global user.email "你的邮箱@example.com"
```

---

## 🚀 第一步：创建 GitHub 仓库

1. 打开浏览器，访问 https://github.com
2. 登录你的 GitHub 账号
3. 点击右上角 **+** 号 → **New repository**
4. 填写仓库信息：
   - **Repository name**: `DeskFlow`
   - **Description**: `一款简洁美观的 Windows 桌面任务管理工具`
   - **选择**: Public（公开）
   - **不要勾选** "Add a README file"（我们已经有了）
   - **不要勾选** "Add .gitignore"（我们已经有了）
   - **License**: 选择 None（我们已经有了）
5. 点击 **Create repository**

创建完成后，你会看到一个页面，记住你的仓库地址，格式如：
```
https://github.com/你的用户名/DeskFlow.git
```

---

## 🚀 第二步：初始化本地仓库并推送

打开 PowerShell，依次执行以下命令：

### 2.1 进入项目目录
```powershell
cd "C:\Users\Administrator\Desktop\DesktopCalendarWPF"
```

### 2.2 初始化 Git 仓库
```powershell
git init
```
输出：`Initialized empty Git repository in ...`

### 2.3 添加所有文件到暂存区
```powershell
git add .
```
（这一步没有输出是正常的）

### 2.4 创建第一次提交
```powershell
git commit -m "Initial commit: DeskFlow v1.0.0 - 桌面效率工具"
```
输出会显示添加了多少文件

### 2.5 重命名分支为 main
```powershell
git branch -M main
```

### 2.6 添加远程仓库地址
**⚠️ 注意：把下面的 `你的用户名` 替换成你的 GitHub 用户名！**
```powershell
git remote add origin https://github.com/你的用户名/DeskFlow.git
```

### 2.7 推送到 GitHub
```powershell
git push -u origin main
```

**首次推送可能会弹出登录窗口：**
- 如果弹出浏览器，点击授权即可
- 如果要求输入用户名密码，输入你的 GitHub 用户名和 **Personal Access Token**

---

## 🔑 如果需要 Personal Access Token

GitHub 现在不支持密码登录，需要使用 Token：

1. 打开 https://github.com/settings/tokens
2. 点击 **Generate new token** → **Generate new token (classic)**
3. 填写：
   - **Note**: `DeskFlow`
   - **Expiration**: 选择 90 days 或 No expiration
   - **勾选权限**: `repo`（整个 repo 部分都勾上）
4. 点击 **Generate token**
5. **立即复制 Token**（只显示一次！）
6. 在 PowerShell 要求输入密码时，粘贴这个 Token

---

## 🚀 第三步：创建 Release 发布版本

代码推送成功后，创建正式发布：

1. 打开你的仓库页面：`https://github.com/你的用户名/DeskFlow`
2. 点击右侧的 **Releases**
3. 点击 **Create a new release**
4. 填写发布信息：

   **Choose a tag**: 输入 `v1.0.0`，然后点击 "Create new tag"
   
   **Release title**: 
   ```
   DeskFlow v1.0.0 - 首个正式版
   ```
   
   **描述内容**（复制以下内容）:
   ```markdown
   ## ✨ DeskFlow v1.0.0 - 首个正式版
   
   一款简洁美观的 Windows 桌面任务管理工具，帮助你高效管理每日待办事项。
   
   ### 🎯 主要功能
   - 📋 任务管理：快速添加、优先级设置、截止日期、子任务
   - 📊 数据统计：今日/本周/本月/全年完成趋势
   - 🎨 多主题：深色主题 + 多种主题色
   - 🖥️ 桌面小部件：按 ~ 键快速显示/隐藏
   - 📁 分类管理：项目分类管理任务
   
   ### 💻 系统要求
   - Windows 10/11 (64位)
   - 无需安装 .NET 运行时
   
   ### 📥 下载
   下载下方的 `DeskFlow_v1.0.0_Windows_x64.zip`，解压后运行 `DesktopCalendar.exe` 即可使用。
   
   ---
   如果觉得好用，欢迎 ⭐ Star 支持！
   ```

5. **上传发布包**：
   - 在 "Attach binaries" 区域
   - 拖入文件：`C:\Users\Administrator\Desktop\DesktopCalendarWPF\DeskFlow_v1.0.0_Windows_x64.zip`
   - 或点击选择文件上传

6. 点击 **Publish release**

---

## ✅ 完成！

恭喜！你的 DeskFlow 已经发布到 GitHub！

### 分享链接
- **仓库地址**: `https://github.com/你的用户名/DeskFlow`
- **下载地址**: `https://github.com/你的用户名/DeskFlow/releases`

### 后续更新流程
当你修改代码后，执行：
```powershell
cd "C:\Users\Administrator\Desktop\DesktopCalendarWPF"
git add .
git commit -m "描述你的修改内容"
git push
```

---

## ❓ 常见问题

### Q: git push 报错 "failed to push"
可能是远程有变化，先拉取再推送：
```powershell
git pull origin main --rebase
git push
```

### Q: 提示 "Permission denied"
检查是否登录了正确的 GitHub 账号，或重新生成 Token。

### Q: 想要撤销 git add
```powershell
git reset
```

### Q: 查看当前状态
```powershell
git status
```

---

**祝发布顺利！🎉**



