using System.Windows;
using System.Windows.Media;

namespace TsmClient.Rendering;

public sealed class GameSurface : FrameworkElement
{
    private const double WorldWidth = 1600;
    private const double WorldHeight = 1200;
    private Point _playerPosition = new(570, 770);
    private IReadOnlyList<(int Id, string Name, Point Position)> _otherPlayers = [];
    private IReadOnlyList<(int Id, string Name, Point Position)> _npcs = [];
    private string _sceneAssetStatus = "Fallback graphic";
    private int _mapId;
    public event Action<short, short>? MoveRequested;

    public GameSurface()
    {
        Focusable = true;
        ClipToBounds = true;
        MouseLeftButtonDown += (_, e) =>
        {
            Point screen = e.GetPosition(this);
            Point world = ScreenToWorld(screen);
            short x = (short)Math.Clamp(Math.Round(world.X), 0, WorldWidth);
            short y = (short)Math.Clamp(Math.Round(world.Y), 0, WorldHeight);
            MoveRequested?.Invoke(x, y);
        };
    }


    public void SetWorld(short x, short y, IEnumerable<(int Id, string Name, short X, short Y)> players)
    {
        _playerPosition = new Point(x, y);
        _otherPlayers = players.Select(p => (p.Id, p.Name, new Point(p.X, p.Y))).ToArray();
        InvalidateVisual();
    }
    public void SetNpcs(IEnumerable<(int Id, string Name, short X, short Y)> npcs)
    {
        _npcs = npcs.Select(n => (n.Id, n.Name, new Point(n.X, n.Y))).ToArray();
        InvalidateVisual();
    }

    public void SetSceneAsset(int mapId, string? relativePath, bool nativeSupported)
    {
        _mapId = mapId;
        _sceneAssetStatus = relativePath is null
            ? $"Map {mapId}: แผนที่ 2D จากข้อมูล Server"
            : nativeSupported ? $"Map {mapId}: {relativePath}" : $"Map {mapId}: แผนที่ 2D + ข้อมูล {relativePath}";
        InvalidateVisual();
    }

    private Vector CameraOffset => new(Math.Clamp(_playerPosition.X - ActualWidth / 2, 0, Math.Max(0, WorldWidth - ActualWidth)),
        Math.Clamp(_playerPosition.Y - ActualHeight / 2, 0, Math.Max(0, WorldHeight - ActualHeight)));
    private Point ScreenToWorld(Point screen) => screen + CameraOffset;
    private Point WorldToScreen(Point world) => world - CameraOffset;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 62, 48)), null, new Rect(RenderSize));
        DrawMap(dc);
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(55, 115, 190, 165)), 1);
        Vector camera = CameraOffset;
        for (double x = -(camera.X % 40); x < ActualWidth; x += 40) dc.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
        for (double y = -(camera.Y % 40); y < ActualHeight; y += 40) dc.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));

        foreach (var player in _otherPlayers)
        {
            Point p = WorldToScreen(player.Position);
            dc.DrawEllipse(Brushes.CornflowerBlue, new Pen(Brushes.White, 1), p, 12, 12);
            DrawText(dc, player.Name, p + new Vector(16, -9), 13, Brushes.White);
        }
        foreach (var npc in _npcs)
        {
            Point p = WorldToScreen(npc.Position);
            dc.DrawEllipse(Brushes.OrangeRed, new Pen(Brushes.Gold, 3), p, 14, 14);
            DrawText(dc, $"NPC {npc.Name}", p + new Vector(18, -10), 14, Brushes.LightYellow);
        }
        Point local = WorldToScreen(_playerPosition);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 193, 7)), new Pen(Brushes.White, 2), local, 15, 15);
        var text = new FormattedText("คลิกบนแผนที่เพื่อเดิน • กล้องติดตามตัวละคร", System.Globalization.CultureInfo.GetCultureInfo("th-TH"),
            FlowDirection.LeftToRight, new Typeface("Leelawadee UI"), 16, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(20, 20));
        DrawText(dc, _sceneAssetStatus, new Point(20, 48), 13, Brushes.LightSteelBlue);
    }

    private void DrawMap(DrawingContext dc)
    {
        Vector camera = CameraOffset;
        Rect W(double x, double y, double width, double height) => new(x - camera.X, y - camera.Y, width, height);
        var road = new SolidColorBrush(Color.FromRgb(166, 137, 92));
        var water = new SolidColorBrush(Color.FromRgb(45, 116, 145));
        var building = new SolidColorBrush(Color.FromRgb(112, 71, 52));
        dc.DrawRectangle(water, null, W(0, 1000, WorldWidth, 200));
        dc.DrawRectangle(road, null, W(0, 710, WorldWidth, 90));
        dc.DrawRectangle(road, null, W(540, 0, 90, WorldHeight));
        foreach (var rect in new[] { W(410, 590, 110, 95), W(655, 590, 130, 100), W(700, 825, 150, 110), W(330, 835, 140, 100) })
            dc.DrawRoundedRectangle(building, new Pen(Brushes.SandyBrown, 2), rect, 8, 8);
        string mapName = _mapId switch { 10801 => "จัวจวิ้น — เมืองเริ่มต้น", 10802 => "ทุ่งฝึกยุทธ์", _ => $"แผนที่ {_mapId}" };
        DrawText(dc, mapName, new Point(Math.Max(20, ActualWidth - 260), 20), 18, Brushes.Gold);
    }


    private static void DrawText(DrawingContext dc, string text, Point position, double size, Brush brush) =>
        dc.DrawText(new FormattedText(text, System.Globalization.CultureInfo.GetCultureInfo("th-TH"), FlowDirection.LeftToRight,
            new Typeface("Leelawadee UI"), size, brush, 1), position);
}
