using System.Windows;
using System.Windows.Media;
using TsmClient.Domain;

namespace TsmClient.Rendering;

public sealed class GameSurface : FrameworkElement
{
    private const double WorldWidth = 1600;
    private const double WorldHeight = 1200;
    private Point _playerPosition = new(570, 770);
    private IReadOnlyList<(int Id, string Name, Point Position)> _otherPlayers = [];
    private IReadOnlyList<(int Id, string Name, Point Position, SceneNpcType Type)> _npcs = [];
    private string _sceneAssetStatus = "Fallback graphic";
    private int _mapId;
    public event Action<short, short>? MoveRequested;
    public event Action<int>? NpcSelected;
    private int? _selectedNpcId;

    public GameSurface()
    {
        Focusable = true;
        ClipToBounds = true;
        MouseLeftButtonDown += (_, e) =>
        {
            Point screen = e.GetPosition(this);
            Point world = ScreenToWorld(screen);
            var npc = _npcs
                .Select(n => (Npc: n, Distance: (n.Position - world).Length))
                .Where(x => x.Distance <= 28)
                .OrderBy(x => x.Distance)
                .Select(x => x.Npc)
                .FirstOrDefault();
            if (npc != default)
            {
                _selectedNpcId = npc.Id;
                InvalidateVisual();
                NpcSelected?.Invoke(npc.Id);
                return;
            }
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
    public void SetNpcs(IEnumerable<(int Id, string Name, short X, short Y, SceneNpcType Type)> npcs)
    {
        _npcs = npcs.Select(n => (n.Id, n.Name, new Point(n.X, n.Y), n.Type)).ToArray();
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
            bool selected = npc.Id == _selectedNpcId;
            Brush fill = npc.Type switch { SceneNpcType.Shop => Brushes.MediumSeaGreen, SceneNpcType.Monster => Brushes.IndianRed, SceneNpcType.QuestTurnIn => Brushes.MediumPurple, _ => Brushes.DarkOrange };
            string marker = npc.Type switch { SceneNpcType.QuestGiver => "!", SceneNpcType.QuestTurnIn => "?", SceneNpcType.Shop => "$", SceneNpcType.Monster => "⚔", _ => "•" };
            dc.DrawEllipse(fill, new Pen(selected ? Brushes.White : Brushes.Gold, selected ? 5 : 3), p,
                selected ? 18 : 14, selected ? 18 : 14);
            DrawText(dc, marker, p + new Vector(-5, -13), 16, Brushes.White);
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
        if (_mapId == 10802)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(83, 112, 55)), null, W(0, 0, WorldWidth, WorldHeight));
            Rect pond = W(1050, 50, 430, 310);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(65, 129, 155)), null,
                new Point(pond.X + pond.Width / 2, pond.Y + pond.Height / 2), pond.Width / 2, pond.Height / 2);
            dc.DrawRoundedRectangle(road, null, W(0, 360, 1100, 85), 20, 20);
            dc.DrawRoundedRectangle(road, null, W(620, 360, 85, 840), 20, 20);
            for (int i = 0; i < 12; i++)
            {
                double x = 80 + i * 125;
                dc.DrawEllipse(Brushes.DarkOliveGreen, new Pen(Brushes.ForestGreen, 3), new Point(x - camera.X, 170 + (i % 3) * 250 - camera.Y), 28, 28);
            }
        }
        else
        {
            dc.DrawRectangle(water, null, W(0, 1000, WorldWidth, 200));
            dc.DrawRectangle(road, null, W(0, 710, WorldWidth, 90));
            dc.DrawRectangle(road, null, W(540, 0, 90, WorldHeight));
            foreach (var rect in new[] { W(410, 590, 110, 95), W(655, 590, 130, 100), W(700, 825, 150, 110), W(330, 835, 140, 100) })
                dc.DrawRoundedRectangle(building, new Pen(Brushes.SandyBrown, 2), rect, 8, 8);
        }
        string mapName = _mapId switch { 10801 => "จัวจวิ้น — เมืองเริ่มต้น", 10802 => "ทุ่งฝึกยุทธ์", _ => $"แผนที่ {_mapId}" };
        DrawText(dc, mapName, new Point(Math.Max(20, ActualWidth - 260), 20), 18, Brushes.Gold);
    }


    private static void DrawText(DrawingContext dc, string text, Point position, double size, Brush brush) =>
        dc.DrawText(new FormattedText(text, System.Globalization.CultureInfo.GetCultureInfo("th-TH"), FlowDirection.LeftToRight,
            new Typeface("Leelawadee UI"), size, brush, 1), position);
}
