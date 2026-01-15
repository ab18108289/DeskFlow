using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

string iconPath = Path.Combine("..", "DesktopCalendar", "Assets", "app.ico");

// 创建多尺寸图标
int[] sizes = { 16, 32, 48, 256 };

using var ms = new MemoryStream();
using var bw = new BinaryWriter(ms);

// ICO 文件头
bw.Write((short)0);      // Reserved
bw.Write((short)1);      // Type: 1 = ICO
bw.Write((short)sizes.Length);  // Number of images

var imageDataList = new List<byte[]>();
int offset = 6 + (sizes.Length * 16); // Header + directory entries

// 写入目录条目
foreach (int size in sizes)
{
    using var bitmap = CreateIcon(size);
    using var pngStream = new MemoryStream();
    bitmap.Save(pngStream, ImageFormat.Png);
    byte[] imageData = pngStream.ToArray();
    imageDataList.Add(imageData);
    
    bw.Write((byte)(size == 256 ? 0 : size));  // Width
    bw.Write((byte)(size == 256 ? 0 : size));  // Height
    bw.Write((byte)0);    // Color palette
    bw.Write((byte)0);    // Reserved
    bw.Write((short)1);   // Color planes
    bw.Write((short)32);  // Bits per pixel
    bw.Write(imageData.Length);  // Image size
    bw.Write(offset);     // Image offset
    
    offset += imageData.Length;
}

// 写入图像数据
foreach (var data in imageDataList)
{
    bw.Write(data);
}

File.WriteAllBytes(iconPath, ms.ToArray());
Console.WriteLine($"图标已生成: {Path.GetFullPath(iconPath)}");

static Bitmap CreateIcon(int size)
{
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bitmap);
    
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
    g.Clear(Color.Transparent);
    
    // 紫蓝渐变背景
    var rect = new Rectangle(0, 0, size, size);
    using var gradientBrush = new LinearGradientBrush(
        rect,
        Color.FromArgb(139, 92, 246),   // 紫色 #8B5CF6
        Color.FromArgb(59, 130, 246),   // 蓝色 #3B82F6
        LinearGradientMode.ForwardDiagonal);
    
    // 圆角矩形
    int margin = Math.Max(1, size / 16);
    int radius = Math.Max(2, size / 6);
    var innerRect = new Rectangle(margin, margin, size - margin * 2, size - margin * 2);
    
    using var path = new GraphicsPath();
    path.AddArc(innerRect.X, innerRect.Y, radius * 2, radius * 2, 180, 90);
    path.AddArc(innerRect.Right - radius * 2, innerRect.Y, radius * 2, radius * 2, 270, 90);
    path.AddArc(innerRect.Right - radius * 2, innerRect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
    path.AddArc(innerRect.X, innerRect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
    path.CloseFigure();
    g.FillPath(gradientBrush, path);
    
    // D 字母
    float fontSize = size * 0.55f;
    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
    using var textBrush = new SolidBrush(Color.White);
    var format = new StringFormat
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };
    g.DrawString("D", font, textBrush, new RectangleF(0, 0, size, size), format);
    
    return bitmap;
}
