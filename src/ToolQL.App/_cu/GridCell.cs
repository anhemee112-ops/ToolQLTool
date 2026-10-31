using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ToolQL.Render;

namespace ToolQL.App;

/// <summary>
/// Một ô của lưới xem nhiều acc: bộ vẽ riêng (camera theo nhân vật của acc đó), ảnh và nhãn. Dùng chung bộ ô lát với cửa sổ chính
/// nên không đóng nó khi hủy. Chỉ gọi từ luồng giao diện.
/// </summary>
public sealed class GridCell : IDisposable
{
    private WriteableBitmap? _bitmap;
    private string? _label;

    public GridCell(AccountRow row, TileCache? tiles, Viewport viewport, Action<AccountRow> onDoubleClick)
    {
        Row = row;
        Viewer = new MapViewer(tiles, viewport, ownsTiles: false);

        Image = new Image { Stretch = Stretch.None };
        RenderOptions.SetBitmapScalingMode(Image, BitmapScalingMode.NearestNeighbor);
        Header = new TextBlock
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            FontSize = 12,
        };

        var content = new Grid();
        content.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = Image });
        content.Children.Add(Header);
        Root = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x12, 0x19)),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(3),
            ClipToBounds = true,
            Child = content,
            ToolTip = "Bấm đúp để xem riêng acc này",
        };
        Root.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) onDoubleClick(Row);
        };
        UpdateLabel();
    }

    public AccountRow Row { get; }
    public MapViewer Viewer { get; }
    public Border Root { get; }
    private Image Image { get; }
    private TextBlock Header { get; }

    /// <summary>Ghi ảnh mới vào ô.</summary>
    public void Show(SkiaSharp.SKBitmap bitmap)
    {
        _bitmap = SkiaBridge.CopyTo(bitmap, _bitmap);
        Image.Source = _bitmap;
    }

    public void UpdateLabel()
    {
        string label = $"{Row.User}  ·  {Row.StateText}";
        if (label == _label) return;
        _label = label;
        Header.Text = label;
    }

    public void Dispose() => Viewer.Dispose();
}
