using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace ToolQL.App;

/// <summary>Chép <see cref="SKBitmap"/> (BGRA, alpha nhân sẵn: kiểu điểm ảnh mặc định của Skia trên Windows) vào <see cref="WriteableBitmap"/> của WPF.</summary>
public static class SkiaBridge
{
    /// <summary>Ghi vào <paramref name="target"/> nếu cùng kích thước, nếu không thì tạo bitmap mới. Trả bitmap dùng để hiển thị.</summary>
    public static WriteableBitmap CopyTo(SKBitmap source, WriteableBitmap? target)
    {
        if (source.ColorType != SKColorType.Bgra8888)
            throw new InvalidOperationException("Ảnh vẽ không phải BGRA8888: " + source.ColorType);
        if (target is null || target.PixelWidth != source.Width || target.PixelHeight != source.Height)
            target = new WriteableBitmap(source.Width, source.Height, 96, 96, PixelFormats.Pbgra32, null);
        target.WritePixels(new Int32Rect(0, 0, source.Width, source.Height), source.GetPixels(), source.RowBytes * source.Height, source.RowBytes);
        return target;
    }
}
