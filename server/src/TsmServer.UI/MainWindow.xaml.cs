using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using TsmServer.App.GM;
using TsmServer.App.Handlers;
using TsmServer.App.Response;
using TsmServer.Data;
using TsmServer.Domain.Constants;
using TsmServer.Domain.Data;
using TsmServer.Domain.Enums;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Domain.Models;
using TsmServer.Domain.ValueObjects;
using TsmServer.GameLogic.Systems;
using TsmServer.Network;
using TsmServer.Persistence;
using TsmServer.Persistence.Repositories;
using TsmServer.Persistence.Services;

namespace TsmServer.UI;

public partial class MainWindow : Window
{
    private PipelinesTcpServer? _tcpServer;
    private SessionManager? _sessionManager;
    private WorldManager? _worldManager;
    private InventorySystem? _inventorySystem;
    private ICharacterRepository? _charRepo;
    private IResponseSender? _responseSender;
    private GmCommandProcessor? _gmProcessor;
    private GameDataManager? _dataManager;
    private InMemoryDataStore? _dataStore;
    private readonly DatabaseBackupService _backupService = new();
    private readonly ServerConfigService _configService = new();

    private DispatcherTimer? _refreshTimer;
    private DateTime? _serverStartTime;
    private readonly StringBuilder _logBuffer = new();
    private bool _isRunning = false;

    // Database browser cache
    private List<ItemViewModel> _allItems = new();
    private List<NpcViewModel> _allNpcs = new();
    private List<FashionViewModel> _allFashions = new();
    private List<GameFileViewModel> _allFiles = new();

    public MainWindow()
    {
        InitializeComponent();
        InitializeServerComponents();
        LoadConfigIntoUI();
        LoadDataBrowser();
        SetupTimer();
        Log("[ระบบ] โปรแกรมบริหารจัดการเซิร์ฟเวอร์ TS Dark World เริ่มทำงานเรียบร้อยแล้ว (ธีมสบายตา ชัดเจน 100%)");
    }

    private void InitializeServerComponents()
    {
        string dataPath = CfgDataPath?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(dataPath) || dataPath == "gamedata")
        {
            dataPath = @"DATA\Data";
            if (CfgDataPath != null) CfgDataPath.Text = dataPath;
        }

        _dataManager = new GameDataManager(dataPath);
        _dataManager.Initialize();

        _dataStore = new InMemoryDataStore();
        _charRepo = _dataStore;
        _inventorySystem = new InventorySystem(_dataStore);
        _worldManager = new WorldManager();
        _sessionManager = new SessionManager();
        _responseSender = new ResponseSenderImpl(_dataManager);

        Log($"[คลังข้อมูลเกม] โหลดไฟล์ทั้งหมด {_dataManager.TotalFilesLoaded} ไฟล์จาก {_dataManager.ActiveDataPath} และ {_dataManager.ActiveEvePath} สำเร็จเรียบร้อย");

        _gmProcessor = new GmCommandProcessor(
            _responseSender,
            _worldManager,
            _inventorySystem,
            _charRepo,
            _dataManager
        );

        var dispatcher = new PacketDispatcher();
        dispatcher.Register(new VersionCheckHandler(_responseSender));
        dispatcher.Register(new ServerTimeHandler());
        dispatcher.Register(new KeepaliveHandler());
        dispatcher.Register(new LoginHandler(_dataStore, _charRepo, _responseSender));
        dispatcher.Register(new CreateCharHandler(_charRepo, _responseSender));
        dispatcher.Register(new SelectCharHandler(_charRepo, _dataStore, _worldManager, _sessionManager, _responseSender));
        dispatcher.Register(new MoveHandler(_worldManager, _charRepo));
        dispatcher.Register(new WarpHandler(_worldManager, _charRepo, _dataManager, _responseSender));
        dispatcher.Register(new ItemOperationHandler(_inventorySystem, _charRepo, _dataStore, _dataManager, _responseSender));
        dispatcher.Register(new NpcTalkHandler(_dataManager, _charRepo, _responseSender));
        dispatcher.Register(new NpcEventHandler(_dataManager, _responseSender));
        dispatcher.Register(new PetManagementHandler(_dataStore, _responseSender));
        dispatcher.Register(new BattleCommandHandler(_charRepo, _dataManager, _responseSender, _dataStore));
        dispatcher.Register(new StatAllocationHandler(_charRepo, _responseSender));
        dispatcher.Register(new TeamHandler(_worldManager, _charRepo, _responseSender));
        dispatcher.Register(new ChatHandler(_worldManager, _sessionManager, _gmProcessor, _responseSender));

        _tcpServer = new PipelinesTcpServer(_sessionManager, dispatcher, onDisconnect: session =>
        {
            Dispatcher.Invoke(() => Log($"[การเชื่อมต่อ] ผู้เล่นออกจากเกม: {session.CharacterName} (รหัส: {session.CharacterId})"));
            return ValueTask.CompletedTask;
        });
    }

    private void LoadDataBrowser()
    {
        if (_dataManager == null) return;
        _allItems = _dataManager.Items.Values.Select(i => new ItemViewModel(i)).OrderBy(i => i.Id).ToList();
        _allNpcs = _dataManager.Npcs.Values.Select(n => new NpcViewModel(n)).OrderBy(n => n.Id).ToList();
        _allFashions = _dataManager.Fashions.Values.Select(f => new FashionViewModel(f)).OrderBy(f => f.Id).ToList();

        FilterItems();
        FilterNpcs();
        FilterFashions();
        LoadGameFiles();
    }

    private void FilterItems()
    {
        string q = TxtSearchItem?.Text?.Trim()?.ToLower() ?? "";
        int catIndex = CmbItemCategory?.SelectedIndex ?? 0;

        var query = _allItems.AsEnumerable();
        if (!string.IsNullOrEmpty(q))
        {
            query = query.Where(i => i.Id.ToString().Contains(q) || i.Name.ToLower().Contains(q) || i.KindStr.ToLower().Contains(q));
        }

        query = catIndex switch
        {
            1 => query.Where(i => i.Slot == 3 || i.RawDef.Slot == 3 || i.KindStr.Contains("อาวุธ")),
            2 => query.Where(i => i.RawDef.Kind == 2 && i.Slot != 3),
            3 => query.Where(i => i.RawDef.Kind == 1 || i.Hp > 0 || i.Sp > 0),
            4 => query.Where(i => i.RawDef.Kind >= 3 || i.Slot == 6),
            _ => query
        };

        var list = query.ToList();
        if (GridItems != null) GridItems.ItemsSource = list;
        if (TxtItemCountDisplay != null) TxtItemCountDisplay.Text = $"แสดง {list.Count:N0} รายการ (จากทั้งหมด {_allItems.Count:N0})";
    }

    private void FilterNpcs()
    {
        string q = TxtSearchNpc?.Text?.Trim()?.ToLower() ?? "";
        int elemIndex = CmbNpcElementFilter?.SelectedIndex ?? 0;

        var query = _allNpcs.AsEnumerable();
        if (!string.IsNullOrEmpty(q))
        {
            query = query.Where(n => n.Id.ToString().Contains(q) || n.Name.ToLower().Contains(q) || n.SkillsStr.ToLower().Contains(q));
        }

        query = elemIndex switch
        {
            1 => query.Where(n => n.Element == 1),
            2 => query.Where(n => n.Element == 2),
            3 => query.Where(n => n.Element == 3),
            4 => query.Where(n => n.Element == 4),
            _ => query
        };

        var list = query.ToList();
        if (GridNpcs != null) GridNpcs.ItemsSource = list;
        if (TxtNpcCountDisplay != null) TxtNpcCountDisplay.Text = $"แสดง {list.Count:N0} รายการ (จากทั้งหมด {_allNpcs.Count:N0})";
    }

    private void FilterFashions()
    {
        string q = TxtSearchFashion?.Text?.Trim()?.ToLower() ?? "";
        int catIndex = CmbFashionCategory?.SelectedIndex ?? 0;

        var query = _allFashions.AsEnumerable();
        if (!string.IsNullOrEmpty(q))
        {
            query = query.Where(f => f.Id.ToString().Contains(q) || f.Name.ToLower().Contains(q) || f.Category.ToLower().Contains(q) || f.SetBonusDesc.ToLower().Contains(q));
        }

        query = catIndex switch
        {
            1 => query.Where(f => f.Category.Contains("ชุดคลุม") || f.Slot == 8),
            2 => query.Where(f => f.Category.Contains("หมวก") || f.Slot == 7),
            3 => query.Where(f => f.Category.Contains("อาวุธ") || f.Slot == 9),
            4 => query.Where(f => f.Category.Contains("ปีก") || f.Category.Contains("ผ้าคลุม") || f.Slot == 100),
            5 => query.Where(f => f.Category.Contains("เทศกาล")),
            _ => query
        };

        var list = query.ToList();
        if (GridFashions != null) GridFashions.ItemsSource = list;
        if (TxtFashionCountDisplay != null) TxtFashionCountDisplay.Text = $"แสดง {list.Count:N0} รายการ (จากทั้งหมด {_allFashions.Count:N0})";
    }

    private void LoadGameFiles()
    {
        _allFiles.Clear();
        string dataDir = _dataManager?.ActiveDataPath ?? @"DATA\Data";
        string eveDir = _dataManager?.ActiveEvePath ?? @"DATA\Data\Eve";

        void Scan(string dir, string label)
        {
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var fi = new FileInfo(f);
                    string ext = fi.Extension.ToLower();
                    string type = ext switch
                    {
                        ".dat" => "📊 ข้อมูลเกมไบนารี (DAT)",
                        ".emg" => "🎬 เหตุการณ์เควสต์ (EMG)",
                        ".lua" => "📜 สคริปต์เควสต์/AI (LUA)",
                        ".mmg" => "🗺️ ข้อมูลแผนที่ (MMG)",
                        ".json" => "⚙️ ข้อมูลกำหนดค่า (JSON)",
                        _ => "📄 ไฟล์ข้อมูลเกม"
                    };

                    string sizeStr = fi.Length > 1024 * 1024
                        ? $"{(fi.Length / (1024.0 * 1024.0)):F2} MB"
                        : $"{(fi.Length / 1024.0):F1} KB";

                    _allFiles.Add(new GameFileViewModel
                    {
                        FileName = fi.Name,
                        FileSizeStr = sizeStr,
                        FileType = type,
                        Directory = label,
                        Status = "✅ โหลดพร้อมใช้งาน"
                    });
                }
            }
        }

        Scan(dataDir, @"DATA\Data");
        Scan(eveDir, @"DATA\Data\Eve");

        FilterFiles();
    }

    private void FilterFiles()
    {
        string q = TxtSearchDataFile?.Text?.Trim()?.ToLower() ?? "";
        var list = string.IsNullOrEmpty(q)
            ? _allFiles
            : _allFiles.Where(f => f.FileName.ToLower().Contains(q) || f.FileType.ToLower().Contains(q)).ToList();

        if (GridDataFiles != null) GridDataFiles.ItemsSource = list;
        if (TxtFilesCountDisplay != null) TxtFilesCountDisplay.Text = $"โหลดแล้ว {list.Count:N0} ไฟล์ (จากทั้งหมด {_allFiles.Count:N0} ไฟล์)";
    }

    private void SetupTimer()
    {
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (s, e) => UpdateDashboardMetrics();
        _refreshTimer.Start();
    }

    private void UpdateDashboardMetrics()
    {
        // 1. RAM Usage
        long memoryBytes = Process.GetCurrentProcess().WorkingSet64;
        TxtRamUsage.Text = $"{(memoryBytes / (1024.0 * 1024.0)):F1} MB";

        // 2. Uptime
        if (_isRunning && _serverStartTime.HasValue)
        {
            var uptime = DateTime.Now - _serverStartTime.Value;
            TxtUptime.Text = uptime.ToString(@"hh\:mm\:ss");
        }
        else
        {
            TxtUptime.Text = "00:00:00";
        }

        // 3. Online Players Count & Filtered Grid
        if (_sessionManager != null)
        {
            var inGameSessions = _sessionManager.InGameSessions.ToList();
            TxtOnlineCount.Text = $"{inGameSessions.Count} คน";

            string search = TxtSearchPlayer?.Text?.Trim()?.ToLower() ?? "";
            var playerViewModels = inGameSessions
                .Where(s => s.PlayerData != null)
                .Where(s => string.IsNullOrEmpty(search) ||
                            s.CharacterName.ToLower().Contains(search) ||
                            s.AccountName.ToLower().Contains(search))
                .Select(s => new PlayerViewModel(s.PlayerData!))
                .ToList();

            GridPlayers.ItemsSource = playerViewModels;
        }
    }

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string line = $"[{timestamp}] {message}\n";
        _logBuffer.Append(line);

        if (TxtLogs != null)
        {
            TxtLogs.Text = _logBuffer.ToString();
            if (ChkAutoScroll?.IsChecked == true)
            {
                TxtLogs.ScrollToEnd();
            }
        }
    }

    // --- Server Actions ---
    private void BtnStartServer_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        string host = CfgHost.Text.Trim();
        int port = int.TryParse(CfgPort.Text.Trim(), out int p) ? p : 6613;

        try
        {
            _tcpServer?.Start(host, port);
            _isRunning = true;
            _serverStartTime = DateTime.Now;

            TxtServerStatus.Text = "🟢 กำลังทำงาน";
            TxtServerStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessColor");
            TxtPort.Text = $"เกม {port} | แพตช์ 8443";

            BtnStartServer.IsEnabled = false;
            BtnStopServer.IsEnabled = true;
            BtnRestartServer.IsEnabled = true;

            Log($"[เซิร์ฟเวอร์] TS Dark World เริ่มต้นสำเร็จ เปิดรับการเชื่อมต่อที่ {host}:{port}");
            TxtFooterStatus.Text = $"เซิร์ฟเวอร์ TS Dark World กำลังทำงานที่พอร์ต {port}";
        }
        catch (Exception ex)
        {
            Log($"[ข้อผิดพลาด] ไม่สามารถเริ่มเซิร์ฟเวอร์ได้: {ex.Message}");
            MessageBox.Show($"ไม่สามารถเปิดเซิร์ฟเวอร์ได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnStopServer_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning) return;

        _tcpServer?.Stop();
        _isRunning = false;
        _serverStartTime = null;

        TxtServerStatus.Text = "🔴 ปิดทำงานอยู่";
        TxtServerStatus.Foreground = (System.Windows.Media.Brush)FindResource("DangerColor");

        BtnStartServer.IsEnabled = true;
        BtnStopServer.IsEnabled = false;
        BtnRestartServer.IsEnabled = false;

        Log("[เซิร์ฟเวอร์] สั่งหยุดการทำงานของเซิร์ฟเวอร์ TS Dark World เรียบร้อยแล้ว");
        TxtFooterStatus.Text = "เซิร์ฟเวอร์ TS Dark World หยุดทำงานแล้ว";
    }

    private void BtnRestartServer_Click(object sender, RoutedEventArgs e)
    {
        BtnStopServer_Click(sender, e);
        Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(() => BtnStartServer_Click(sender, e)));
    }

    // --- Player Management Actions ---
    private void TxtSearchPlayer_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateDashboardMetrics();
    }

    private void BtnRefreshPlayers_Click(object sender, RoutedEventArgs e)
    {
        UpdateDashboardMetrics();
    }

    private void BtnKickPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null)
            {
                _ = session.CloseAsync();
                Log($"[คำสั่ง GM] เตะผู้เล่นออกจากเซิร์ฟเวอร์: {p.Name} (รหัส: {p.CharacterId})");
            }
        }
    }

    private async void BtnAddGoldPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _charRepo != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null && session.PlayerData != null)
            {
                long newGold = session.PlayerData.Gold + 1000000;
                await _charRepo.UpdateGoldAsync(session.CharacterId, newGold);
                session.PlayerData = session.PlayerData with { Gold = newGold };
                await _responseSender.SendGoldUpdateAsync(session, newGold);
                await _responseSender.SendSystemNoticeAsync(session, "[ระบบ GM] คุณได้รับเงิน 1,000,000 ทองจากผู้ดูแลระบบ!");
                Log($"[คำสั่ง GM] เพิ่มเงิน 1,000,000 ทองให้ผู้เล่น {p.Name}");
            }
        }
    }

    private async void BtnWarpPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _worldManager != null && _charRepo != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null)
            {
                _worldManager.EnterMap(Opcodes.DefaultStartMap, session);
                session.X = Opcodes.DefaultStartX;
                session.Y = Opcodes.DefaultStartY;
                if (session.PlayerData != null)
                {
                    session.PlayerData = session.PlayerData with { MapId = Opcodes.DefaultStartMap, X = session.X, Y = session.Y };
                }
                await _charRepo.UpdatePositionAsync(session.CharacterId, Opcodes.DefaultStartMap, session.X, session.Y);
                var others = _worldManager.GetPlayersInMap(Opcodes.DefaultStartMap, session.SessionId);
                await _responseSender.SendSceneInfoAsync(session, Opcodes.DefaultStartMap, others);
                await _responseSender.SendSystemNoticeAsync(session, "[ระบบ GM] คุณถูกวาร์ปกลับสู่เมืองเริ่มต้น!");
                Log($"[คำสั่ง GM] วาร์ปผู้เล่น {p.Name} กลับสู่เมืองเริ่มต้นเรียบร้อยแล้ว");
            }
        }
    }

    private async void BtnSetLevelPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _charRepo != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null && session.PlayerData != null)
            {
                session.PlayerData = session.PlayerData with { Level = 100 };
                await _charRepo.SavePlayerDataAsync(session.PlayerData);
                await _responseSender.SendEnterGameAsync(session, session.PlayerData);
                await _responseSender.SendSystemNoticeAsync(session, "[ระบบ GM] เลเวลของคุณถูกปรับเป็น Lv.100 เรียบร้อยแล้ว!");
                Log($"[คำสั่ง GM] ปรับเลเวลของตัวละคร {p.Name} เป็น 100");
            }
        }
    }

    private async void BtnGiveStarterKit_Click(object sender, RoutedEventArgs e)
    {
        if (GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _inventorySystem != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null)
            {
                await _inventorySystem.AddItemAsync(p.CharacterId, 10001, 50); // ซาลาเปา x50
                await _inventorySystem.AddItemAsync(p.CharacterId, 20001, 1);  // ดาบเหล็ก x1
                await _inventorySystem.AddItemAsync(p.CharacterId, 20002, 1);  // เสื้อผ้าธรรมดา x1
                var items = await _inventorySystem.GetBagItemsAsync(p.CharacterId);
                await _responseSender.SendInventoryAsync(session, items);
                await _responseSender.SendSystemNoticeAsync(session, "[ระบบ GM] คุณได้รับชุดอุปกรณ์เริ่มต้น (ดาบเหล็ก, ชุดผ้า, ซาลาเปา x50)!");
                Log($"[คำสั่ง GM] มอบชุดอุปกรณ์เริ่มต้นให้ผู้เล่น {p.Name}");
            }
        }
    }

    private async void BtnWhisperPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null)
            {
                await _responseSender.SendChatMessageAsync(session, (int)ChatChannel.Private, "[ผู้ดูแลระบบ]", "ยินดีต้อนรับสู่ TS Dark World ขอให้สนุกกับการเล่นเกมครับ!");
                Log($"[คำสั่ง GM] ส่งข้อความกระซิบหาผู้เล่น {p.Name}");
            }
        }
    }

    // --- Data Browser Search, Filtering & Actions ---
    private void TxtSearchItem_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterItems();
    }

    private void CmbItemCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterItems();
    }

    private void BtnRefreshItems_Click(object sender, RoutedEventArgs e)
    {
        LoadDataBrowser();
    }

    private void GridItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridItems.SelectedItem is ItemViewModel item && TxtSelectedItemInfo != null)
        {
            TxtSelectedItemInfo.Text = $"🎒 ไอเทมที่เลือก: [{item.Id}] {item.Name} ({item.KindStr}) • เลเวล {item.Level} • โจมตี +{item.Atk} • ป้องกัน +{item.Def} • ราคา {item.Price:N0} ทอง";
        }
    }

    private void BtnCopyItemGm_Click(object sender, RoutedEventArgs e)
    {
        if (GridItems.SelectedItem is ItemViewModel item)
        {
            string cmd = $"/additem {item.Id} 1";
            Clipboard.SetText(cmd);
            MessageBox.Show($"คัดลอกคำสั่ง GM เรียบร้อยแล้ว:\n{cmd}", "คัดลอกสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("กรุณาเลือกไอเทมในตารางก่อนกดคัดลอก!", "คำแนะนำ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnSendSelectedItem_Click(object sender, RoutedEventArgs e)
    {
        if (GridItems.SelectedItem is ItemViewModel item && GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _inventorySystem != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null)
            {
                await _inventorySystem.AddItemAsync(p.CharacterId, item.Id, 1);
                var items = await _inventorySystem.GetBagItemsAsync(p.CharacterId);
                await _responseSender.SendInventoryAsync(session, items);
                await _responseSender.SendSystemNoticeAsync(session, $"[ระบบ GM] คุณได้รับไอเทม: {item.Name} (รหัส: {item.Id})!");
                Log($"[คำสั่ง GM] ส่งไอเทม {item.Name} (รหัส: {item.Id}) ให้ผู้เล่น {p.Name}");
                MessageBox.Show($"ส่งไอเทม {item.Name} ให้ผู้เล่น {p.Name} สำเร็จแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("กรุณาเลือกผู้เล่นในแท็บ 'จัดการผู้เล่นออนไลน์' และเลือกไอเทมในตารางก่อนกดส่ง!", "คำแนะนำ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void TxtSearchNpc_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterNpcs();
    }

    private void CmbNpcElementFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterNpcs();
    }

    private void BtnRefreshNpcs_Click(object sender, RoutedEventArgs e)
    {
        LoadDataBrowser();
    }

    private void GridNpcs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridNpcs.SelectedItem is NpcViewModel npc && TxtSelectedNpcInfo != null)
        {
            TxtSelectedNpcInfo.Text = $"🐾 ขุนพลที่เลือก: [{npc.Id}] {npc.Name} • เลเวล {npc.Level} • ธาตุ: {npc.ElementStr} • HP: {npc.Hp:N0} • ATK: {npc.Atk} • DEF: {npc.Def}";
        }
    }

    private void BtnCopyNpcGm_Click(object sender, RoutedEventArgs e)
    {
        if (GridNpcs.SelectedItem is NpcViewModel npc)
        {
            string cmd = $"/addpet {npc.Id}";
            Clipboard.SetText(cmd);
            MessageBox.Show($"คัดลอกคำสั่ง GM เรียบร้อยแล้ว:\n{cmd}", "คัดลอกสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("กรุณาเลือกขุนพลในตารางก่อนกดคัดลอก!", "คำแนะนำ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnSendSelectedPet_Click(object sender, RoutedEventArgs e)
    {
        if (GridNpcs.SelectedItem is NpcViewModel npc && GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _dataStore != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null)
            {
                var newPet = new FollowNpcData(
                    Slot: 1,
                    NpcId: npc.Id,
                    CustomName: npc.Name,
                    Level: npc.Level,
                    Exp: 0,
                    Hp: npc.Hp,
                    MaxHp: npc.Hp,
                    Sp: npc.Sp,
                    MaxSp: npc.Sp,
                    IntVal: npc.Matk,
                    AtkVal: npc.Atk,
                    DefVal: npc.Def,
                    HpaVal: 10,
                    SpaVal: 10,
                    AgiVal: npc.Agi,
                    Loyalty: 100,
                    IsDeployed: true
                );
                await _dataStore.AddPetAsync(p.CharacterId, newPet);
                await _responseSender.SendSystemNoticeAsync(session, $"[ระบบ GM] คุณได้รับขุนพลร่วมทัพ: {npc.Name} (รหัส: {npc.Id})!");
                Log($"[คำสั่ง GM] มอบขุนพล {npc.Name} (รหัส: {npc.Id}) ให้ผู้เล่น {p.Name}");
                MessageBox.Show($"มอบขุนพล {npc.Name} ให้ผู้เล่น {p.Name} เรียบร้อยแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("กรุณาเลือกผู้เล่นในแท็บ 'จัดการผู้เล่นออนไลน์' และเลือกขุนพลในตารางก่อนกดส่ง!", "คำแนะนำ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void TxtSearchFashion_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterFashions();
    }

    private void CmbFashionCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterFashions();
    }

    private void BtnRefreshFashion_Click(object sender, RoutedEventArgs e)
    {
        LoadDataBrowser();
    }

    private void GridFashions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridFashions.SelectedItem is FashionViewModel f && TxtSelectedFashionInfo != null)
        {
            TxtSelectedFashionInfo.Text = $"👗 ชุดแฟชั่นที่เลือก: [{f.Id}] {f.Name} ({f.Category}) • {f.AppearanceDesc} • {f.SetBonusDesc} • ราคา {f.Price:N0} ทอง";
        }
    }

    private void BtnCopyFashionGm_Click(object sender, RoutedEventArgs e)
    {
        if (GridFashions.SelectedItem is FashionViewModel f)
        {
            string cmd = $"/additem {f.Id} 1";
            Clipboard.SetText(cmd);
            MessageBox.Show($"คัดลอกคำสั่ง GM เสกชุดแฟชั่นเรียบร้อยแล้ว:\n{cmd}", "คัดลอกสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("กรุณาเลือกชุดแฟชั่นในตารางก่อนกดคัดลอก!", "คำแนะนำ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnSendSelectedFashion_Click(object sender, RoutedEventArgs e)
    {
        if (GridFashions.SelectedItem is FashionViewModel f && GridPlayers.SelectedItem is PlayerViewModel p && _sessionManager != null && _inventorySystem != null && _responseSender != null)
        {
            var session = _sessionManager.GetByCharacterId(p.CharacterId);
            if (session != null)
            {
                await _inventorySystem.AddItemAsync(p.CharacterId, f.Id, 1);
                var items = await _inventorySystem.GetBagItemsAsync(p.CharacterId);
                await _responseSender.SendInventoryAsync(session, items);
                await _responseSender.SendSystemNoticeAsync(session, $"[ระบบ GM] คุณได้รับชุดแฟชั่นพิเศษ: {f.Name} (รหัส: {f.Id})!");
                Log($"[คำสั่ง GM] มอบชุดแฟชั่น {f.Name} (รหัส: {f.Id}) ให้ผู้เล่น {p.Name}");
                MessageBox.Show($"มอบชุดแฟชั่น {f.Name} ให้ผู้เล่น {p.Name} สำเร็จแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("กรุณาเลือกผู้เล่นในแท็บ 'จัดการผู้เล่นออนไลน์' และเลือกชุดแฟชั่นในตารางก่อนกดส่ง!", "คำแนะนำ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void TxtSearchDataFile_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterFiles();
    }

    private void BtnOpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        string dir = _dataManager?.ActiveDataPath ?? @"DATA\Data";
        if (Directory.Exists(dir))
        {
            try { Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(dir), UseShellExecute = true }); } catch { }
        }
    }

    private void BtnOpenEveFolder_Click(object sender, RoutedEventArgs e)
    {
        string dir = _dataManager?.ActiveEvePath ?? @"DATA\Data\Eve";
        if (Directory.Exists(dir))
        {
            try { Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(dir), UseShellExecute = true }); } catch { }
        }
    }

    // --- GM Global Actions ---
    private async void BtnSendBroadcast_Click(object sender, RoutedEventArgs e)
    {
        string msg = TxtBroadcastMsg.Text.Trim();
        if (string.IsNullOrEmpty(msg) || _sessionManager == null || _responseSender == null) return;

        foreach (var s in _sessionManager.InGameSessions)
        {
            await _responseSender.SendChatMessageAsync(s, (int)ChatChannel.Horn, "📢 ประกาศจากเซิร์ฟเวอร์", msg);
        }

        Log($"[ประกาศทั้งเซิร์ฟเวอร์] {msg}");
    }

    private async void BtnGiveGoldAll_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionManager == null || _charRepo == null || _responseSender == null) return;

        int count = 0;
        foreach (var s in _sessionManager.InGameSessions)
        {
            if (s.PlayerData != null)
            {
                long newGold = s.PlayerData.Gold + 500000;
                await _charRepo.UpdateGoldAsync(s.CharacterId, newGold);
                s.PlayerData = s.PlayerData with { Gold = newGold };
                await _responseSender.SendGoldUpdateAsync(s, newGold);
                await _responseSender.SendSystemNoticeAsync(s, "[กิจกรรมออนไลน์] คุณได้รับรางวัล 500,000 ทองจากระบบ!");
                count++;
            }
        }
        Log($"[คำสั่ง GM] แจกเงิน 500,000 ทองให้ผู้เล่นออนไลน์ทั้งหมดจำนวน {count} คน");
    }

    private async void BtnWarpAllToStart_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionManager == null || _worldManager == null || _charRepo == null || _responseSender == null) return;

        foreach (var s in _sessionManager.InGameSessions)
        {
            _worldManager.EnterMap(Opcodes.DefaultStartMap, s);
            s.X = Opcodes.DefaultStartX;
            s.Y = Opcodes.DefaultStartY;
            if (s.PlayerData != null)
            {
                s.PlayerData = s.PlayerData with { MapId = Opcodes.DefaultStartMap, X = s.X, Y = s.Y };
            }
            await _charRepo.UpdatePositionAsync(s.CharacterId, Opcodes.DefaultStartMap, s.X, s.Y);
            var others = _worldManager.GetPlayersInMap(Opcodes.DefaultStartMap, s.SessionId);
            await _responseSender.SendSceneInfoAsync(s, Opcodes.DefaultStartMap, others);
            await _responseSender.SendSystemNoticeAsync(s, "[ระบบ GM] ผู้เล่นทุกคนถูกรวมตัวกลับสู่เมืองเริ่มต้น!");
        }
        Log("[คำสั่ง GM] ดึงผู้เล่นออนไลน์ทุกคนกลับสู่เมืองเริ่มต้น (10801)");
    }

    private async void BtnHealAll_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionManager == null || _charRepo == null || _responseSender == null) return;

        foreach (var s in _sessionManager.InGameSessions)
        {
            if (s.PlayerData != null)
            {
                s.PlayerData = s.PlayerData with { Hp = s.PlayerData.MaxHp, Sp = s.PlayerData.MaxSp };
                await _charRepo.SavePlayerDataAsync(s.PlayerData);
                await _responseSender.SendEnterGameAsync(s, s.PlayerData);
                await _responseSender.SendSystemNoticeAsync(s, "[ระบบ GM] พลังชีวิตและมานาของคุณถูกฟื้นฟูเต็ม 100%!");
            }
        }
        Log("[คำสั่ง GM] ฟื้นฟูเลือดและมานาเต็มให้ผู้เล่นทุกคนในเซิร์ฟเวอร์");
    }

    private async void BtnMaintenanceCountdown_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionManager == null || _responseSender == null) return;

        foreach (var s in _sessionManager.InGameSessions)
        {
            await _responseSender.SendChatMessageAsync(s, (int)ChatChannel.Horn, "⚠️ แจ้งเตือนปิดปรับปรุง", "เซิร์ฟเวอร์ TS Dark World จะปิดปรับปรุงในอีก 60 วินาที กรุณาเตรียมตัวออกจากเกม!");
        }
        Log("[แจ้งเตือนปิดปรับปรุง] ส่งข้อความนับถอยหลัง 60 วินาทีให้ผู้เล่นทุกคนเรียบร้อยแล้ว");
    }

    private async void BtnExecuteGmCommand_Click(object sender, RoutedEventArgs e)
    {
        string cmd = TxtGmCommand.Text.Trim();
        if (string.IsNullOrEmpty(cmd) || _gmProcessor == null || _sessionManager == null) return;

        var firstSession = _sessionManager.InGameSessions.FirstOrDefault();
        if (firstSession != null)
        {
            await _gmProcessor.ExecuteAsync(firstSession, cmd);
            Log($"[ดำเนินการคำสั่ง GM] {cmd}");
        }
        else
        {
            Log($"[คำสั่ง GM] ไม่มีผู้เล่นออนไลน์ในขณะนี้ คำสั่งถูกบันทึก: {cmd}");
        }
    }

    // --- Account Management ---
    private async void BtnCreateAccount_Click(object sender, RoutedEventArgs e)
    {
        string account = TxtNewAccount.Text.Trim();
        string password = TxtNewPassword.Text.Trim();
        int gmLevel = CmbNewGmLevel.SelectedIndex switch { 1 => 5, 2 => 10, _ => 0 };

        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password) || _dataStore == null)
        {
            MessageBox.Show("กรุณากรอกชื่อบัญชีและรหัสผ่านให้ครบถ้วน!", "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string hash = BCrypt.Net.BCrypt.HashPassword(password);
        int newId = await _dataStore.CreateAccountAsync(account, hash);
        Log($"[จัดการบัญชี] สร้าง/อัปเดตบัญชี [{account}] (รหัส: {newId}, สิทธิ์ GM: {gmLevel}) สำเร็จเรียบร้อย");
        MessageBox.Show($"สร้าง/อัปเดตบัญชี [{account}] เรียบร้อยแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnUnstuckChar_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtUnstuckCharId.Text.Trim(), out int charId) && _charRepo != null)
        {
            await _charRepo.UpdatePositionAsync(charId, Opcodes.DefaultStartMap, Opcodes.DefaultStartX, Opcodes.DefaultStartY);
            Log($"[ช่วยเหลือตัวละคร] รีเซ็ตพิกัดตัวละครรหัส {charId} กลับสู่เมืองเริ่มต้น (10801)");
            MessageBox.Show($"รีเซ็ตพิกัดตัวละครรหัส {charId} กลับสู่เมืองเริ่มต้นเรียบร้อยแล้ว!", "ช่วยเหลือสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // --- Logs & Configuration ---
    private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _logBuffer.Clear();
        TxtLogs.Text = string.Empty;
    }

    private void BtnExportLogs_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "ไฟล์ข้อความ (*.txt)|*.txt|ไฟล์ทั้งหมด (*.*)|*.*",
            FileName = $"ts_darkworld_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            File.WriteAllText(sfd.FileName, _logBuffer.ToString(), Encoding.UTF8);
            MessageBox.Show("ส่งออกไฟล์บันทึกการทำงานเรียบร้อยแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RbIpMode_Changed(object sender, RoutedEventArgs e)
    {
        if (CfgHost == null || CfgVpsIp == null || RbLocalMode == null) return;

        if (RbLocalMode.IsChecked == true)
        {
            CfgHost.Text = "127.0.0.1";
            CfgVpsIp.Text = "127.0.0.1";
            Log("[โหมดการเชื่อมต่อ] เปลี่ยนเป็น: เล่นคนเดียวในเครื่อง (Localhost: 127.0.0.1)");
        }
        else if (RbVpsMode?.IsChecked == true)
        {
            CfgHost.Text = "0.0.0.0";
            Log("[โหมดการเชื่อมต่อ] เปลี่ยนเป็น: เปิดออนไลน์บน VPS Server (Bind: 0.0.0.0)");
        }
    }

    private async void BtnDetectPublicIp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            string ip = (await client.GetStringAsync("https://api.ipify.org")).Trim();
            CfgVpsIp.Text = ip;
            Log($"[การเชื่อมต่อ] ตรวจพบไอพีสาธารณะ (Public IP): {ip}");
            MessageBox.Show($"ตรวจพบไอพีเครื่องภายนอก: {ip}", "ไอพีสาธารณะ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            // Fallback to local machine IP
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            var localIp = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
            CfgVpsIp.Text = localIp;
            Log($"[การเชื่อมต่อ] ตรวจพบไอพี Local Network: {localIp}");
            MessageBox.Show($"ตรวจพบไอพีเครื่อง (Local IP): {localIp}", "ไอพีเครื่อง", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnGenerateClientConfig_Click(object sender, RoutedEventArgs e)
    {
        string targetIp = RbLocalMode.IsChecked == true ? "127.0.0.1" : CfgVpsIp.Text.Trim();
        string port = CfgPort.Text.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("==================================================================");
        sb.AppendLine("     คู่มือและข้อมูลการเชื่อมต่อเซิร์ฟเวอร์ TS Dark World");
        sb.AppendLine("==================================================================");
        sb.AppendLine();
        sb.AppendLine($"ชื่อเซิร์ฟเวอร์: TS Dark World");
        sb.AppendLine($"โหมดการเชื่อมต่อ: {(RbLocalMode.IsChecked == true ? "Localhost (เล่นคนเดียว)" : "VPS Server (ออนไลน์)")}");
        sb.AppendLine($"Server IP: {targetIp}");
        sb.AppendLine($"Game Port: {port} (TCP)");
        sb.AppendLine($"CDN Version Port: 8443 (HTTP)");
        sb.AppendLine();
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine("วิธีตั้งค่าในตัวเกมสำหรับผู้เล่น:");
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine($"1. ตั้งค่า Server IP ในตัวเกมหรือ Patch เป็น: {targetIp}");
        sb.AppendLine($"2. Game TCP Port: {port}");
        sb.AppendLine($"3. ไม่ต้องผ่าน API ภายนอก เชื่อมต่อตรงเข้าสู่ Socket พอร์ต 6613");
        sb.AppendLine();
        sb.AppendLine("ไอดีสำหรับเข้าทดสอบ:");
        sb.AppendLine("- Development Admin: ตั้งค่า TSM_DEV_ADMIN_PASSWORD ก่อนเปิด Server หากต้องการ seed account");
        sb.AppendLine("- บัญชีผู้เล่นทั่วไป: สมัครใหม่จาก Client ได้ทันที");
        sb.AppendLine("==================================================================");

        string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_connection_info.txt");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);

        Log($"[การเชื่อมต่อ] สร้างไฟล์ข้อมูลการเชื่อมต่อ ({outputPath}) สำหรับไอพี {targetIp} เรียบร้อยแล้ว");
        MessageBox.Show($"สร้างไฟล์ข้อมูลการเชื่อมต่อเรียบร้อยแล้ว!\n\nไฟล์อยู่ที่:\n{outputPath}", "สร้างไฟล์สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnTestDb_Click(object sender, RoutedEventArgs e)
    {
        string connStr = CfgDbConn.Text.Trim();
        var factory = new DatabaseFactory(connStr);
        Log("[ฐานข้อมูล] กำลังทดสอบการเชื่อมต่อไปยัง MySQL Server...");

        var result = await factory.TestConnectionAsync();
        if (result.Success)
        {
            Log($"[ฐานข้อมูล] {result.Message}");
            MessageBox.Show(result.Message, "การเชื่อมต่อสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            Log($"[ข้อผิดพลาดฐานข้อมูล] {result.Message}");
            MessageBox.Show(result.Message, "การเชื่อมต่อล้มเหลว", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnInitDb_Click(object sender, RoutedEventArgs e)
    {
        string connStr = CfgDbConn.Text.Trim();
        var factory = new DatabaseFactory(connStr);
        Log("[ฐานข้อมูล] กำลังเตรียมสร้างและอัปเดตตารางฐานข้อมูล MySQL...");

        // Trigger automatic backup before initializing if enabled
        if (ChkAutoBackup?.IsChecked == true)
        {
            var backupRes = await _backupService.CreateAutoBackupAsync(connStr, "ก่อนดำเนินการสร้างหรืออัปเดตโครงสร้างฐานข้อมูล");
            if (backupRes.Success)
            {
                Log($"[แบล็กอัปอัตโนมัติ] {backupRes.Message}");
            }
        }

        string sqlPath = Path.Combine(_backupService.SqlBaseDirectory, "tsm_database_schema.sql");
        if (!File.Exists(sqlPath))
        {
            sqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tsm_database_schema.sql");
        }
        string? script = File.Exists(sqlPath) ? File.ReadAllText(sqlPath, Encoding.UTF8) : null;

        var result = await factory.InitializeDatabaseAndTablesAsync(script);
        if (result.Success)
        {
            Log($"[ฐานข้อมูล] {result.Message}");
            MessageBox.Show(result.Message, "สร้างฐานข้อมูลสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            Log($"[ข้อผิดพลาดฐานข้อมูล] {result.Message}");
            MessageBox.Show(result.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnBackupDb_Click(object sender, RoutedEventArgs e)
    {
        string connStr = CfgDbConn.Text.Trim();
        Log("[แบล็กอัป] กำลังสำรองข้อมูล SQL ลงโฟลเดอร์ SQL\\Backups...");

        var result = await _backupService.CreateAutoBackupAsync(connStr, "สำรองข้อมูลแบบ Manual โดยผู้ดูแลระบบ");
        if (result.Success)
        {
            Log($"[แบล็กอัป] สำรองข้อมูลสำเร็จ: {result.FilePath}");
            MessageBox.Show($"สำรองข้อมูล SQL สำเร็จเรียบร้อยแล้ว!\n\nไฟล์อยู่ที่:\n{result.FilePath}", "สำรองข้อมูลสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            Log($"[ข้อผิดพลาดแบล็กอัป] {result.Message}");
            MessageBox.Show(result.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOpenBackupDir_Click(object sender, RoutedEventArgs e)
    {
        string dir = _backupService.BackupDirectory;
        if (Directory.Exists(dir))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show($"โฟลเดอร์ Backup อยู่ที่:\n{dir}", "ที่อยู่โฟลเดอร์ Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("ยังไม่มีโฟลเดอร์ Backup", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOpenSqlFile_Click(object sender, RoutedEventArgs e)
    {
        string sqlPath = Path.Combine(_backupService.SqlBaseDirectory, "tsm_database_schema.sql");
        if (!File.Exists(sqlPath))
        {
            sqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tsm_database_schema.sql");
        }

        if (File.Exists(sqlPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = sqlPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show($"ไฟล์ SQL อยู่ที่:\n{sqlPath}", "ที่อยู่ไฟล์ SQL", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("ไม่พบไฟล์ tsm_database_schema.sql ในโฟลเดอร์ SQL", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private void LoadConfigIntoUI()
    {
        var cfg = _configService.CurrentConfig;
        if (cfg == null) return;

        if (CfgHost != null) CfgHost.Text = cfg.Server.Host;
        if (CfgPort != null) CfgPort.Text = cfg.Server.Port.ToString();
        if (CfgVpsIp != null) CfgVpsIp.Text = cfg.Server.Host;
        if (CfgDbConn != null) CfgDbConn.Text = cfg.Database.ConnectionString;
        if (CfgDataPath != null) CfgDataPath.Text = cfg.GameData.DataDirectory;

        if (CfgExpRate != null) CfgExpRate.Text = cfg.Rates.ExpMultiplier.ToString();
        if (CfgDropRate != null) CfgDropRate.Text = cfg.Rates.DropMultiplier.ToString();
        if (CfgGoldRate != null) CfgGoldRate.Text = cfg.Rates.GoldMultiplier.ToString();
        if (CfgPetExpRate != null) CfgPetExpRate.Text = cfg.Rates.PetExpMultiplier.ToString();

        if (TxtConfigJson != null) TxtConfigJson.Text = _configService.GetFormattedJson();
        if (TxtConfigPathDisplay != null) TxtConfigPathDisplay.Text = $"📁 ตำแหน่งไฟล์: {_configService.ConfigFilePath}";
    }

    private void BtnReloadConfig_Click(object sender, RoutedEventArgs e)
    {
        _configService.LoadConfig();
        LoadConfigIntoUI();
        Log($"[การตั้งค่า] โหลดการตั้งค่าจาก {_configService.ConfigFilePath} สำเร็จเรียบร้อย");
        MessageBox.Show("โหลดข้อมูลการตั้งค่าจากไฟล์ Config.JSON เรียบร้อยแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSaveJsonEditor_Click(object sender, RoutedEventArgs e)
    {
        string jsonText = TxtConfigJson.Text;
        if (_configService.SaveFromJsonString(jsonText, out var parsed, out var error))
        {
            LoadConfigIntoUI();
            Log("[การตั้งค่า] บันทึกและปรับใช้ข้อมูลจากตัวแก้ไข JSON ลงไฟล์ Config.JSON สำเร็จเรียบร้อย");
            MessageBox.Show("บันทึกการตั้งค่าลงไฟล์ Config.JSON เรียบร้อยแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            Log($"[ข้อผิดพลาดการตั้งค่า] {error}");
            MessageBox.Show($"รูปแบบ JSON ไม่ถูกต้อง:\n{error}", "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnResetDefaultConfig_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("คุณต้องการรีเซ็ตการตั้งค่าทั้งหมดกลับสู่ค่าเริ่มต้นใช่หรือไม่?", "ยืนยันการรีเซ็ต", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _configService.SaveConfig(new ServerConfiguration());
            LoadConfigIntoUI();
            Log("[การตั้งค่า] รีเซ็ตการตั้งค่าทั้งหมดกลับสู่ค่าเริ่มต้นเรียบร้อยแล้ว");
            MessageBox.Show("รีเซ็ตการตั้งค่ากลับสู่ค่าเริ่มต้นเรียบร้อยแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnOpenConfigDir_Click(object sender, RoutedEventArgs e)
    {
        string? dir = Path.GetDirectoryName(_configService.ConfigFilePath);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show($"โฟลเดอร์ Config อยู่ที่:\n{dir}", "ที่อยู่โฟลเดอร์", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        string host = CfgHost.Text.Trim();
        int port = int.TryParse(CfgPort.Text.Trim(), out int p) ? p : 6613;
        string dbConn = CfgDbConn.Text.Trim();
        string dataPath = CfgDataPath.Text.Trim();

        double exp = double.TryParse(CfgExpRate.Text.Trim(), out double er) ? er : 1.0;
        double drop = double.TryParse(CfgDropRate.Text.Trim(), out double dr) ? dr : 1.0;
        double gold = double.TryParse(CfgGoldRate?.Text?.Trim() ?? "1", out double gr) ? gr : 1.0;
        double petExp = double.TryParse(CfgPetExpRate?.Text?.Trim() ?? "1", out double pr) ? pr : 1.0;

        string targetIp = RbLocalMode.IsChecked == true ? "127.0.0.1" : CfgVpsIp.Text.Trim();

        // Update Configuration model
        var cfg = _configService.CurrentConfig;
        cfg.Server.Host = host;
        cfg.Server.Port = port;
        cfg.Database.ConnectionString = dbConn;
        cfg.GameData.DataDirectory = dataPath;
        cfg.Rates.ExpMultiplier = exp;
        cfg.Rates.DropMultiplier = drop;
        cfg.Rates.GoldMultiplier = gold;
        cfg.Rates.PetExpMultiplier = petExp;

        _configService.SaveConfig(cfg);
        if (TxtConfigJson != null) TxtConfigJson.Text = _configService.GetFormattedJson();

        TxtRates.Text = $"EXP x{exp} • ดรอป x{drop} • เงิน x{gold}";
        Log($"[ตั้งค่า] บันทึกการตั้งค่าลง Config.JSON สำเร็จ: ไอพี {targetIp}, EXP x{exp}, ดรอป x{drop}, เงิน x{gold}");
        MessageBox.Show($"บันทึกการตั้งค่าลง Config.JSON และปรับใช้กับเซิร์ฟเวอร์เรียบร้อยแล้ว!\n(โหมดไอพี: {targetIp})", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public class PlayerViewModel
{
    private readonly PlayerDataDto _dto;

    public PlayerViewModel(PlayerDataDto dto)
    {
        _dto = dto;
    }

    public int CharacterId => _dto.CharacterId;
    public string Account => _dto.Account;
    public string Name => _dto.Name;
    public int Level => _dto.Level;
    public string ElementStr => _dto.Element switch
    {
        1 => "🌍 ธาตุดิน",
        2 => "💧 ธาตุน้ำ",
        3 => "🔥 ธาตุไฟ",
        4 => "💨 ธาตุลม",
        _ => "ไร้ธาตุ"
    };
    public int MapId => _dto.MapId;
    public string PositionStr => $"({_dto.X}, {_dto.Y})";
    public string HpSpStr => $"{_dto.Hp}/{_dto.MaxHp} | {_dto.Sp}/{_dto.MaxSp}";
    public long Gold => _dto.Gold;
    public int GmLevel => _dto.GmLevel;
}

public class ItemViewModel
{
    private readonly ItemDef _def;
    public ItemViewModel(ItemDef def) { _def = def; }

    public int Id => _def.Id;
    public string Name => _def.Name;
    public int Kind => _def.Kind;
    public string KindStr => _def.Kind switch
    {
        1 => "🧪 ฟื้นฟู/กดใช้",
        2 => _def.Slot switch
        {
            1 => "👑 หมวก/ศีรษะ",
            2 => "🥋 เสื้อผ้า/เกราะ",
            3 => "⚔️ อาวุธหลัก",
            4 => "🥊 ปลอกแขน",
            5 => "👢 รองเท้า",
            6 => "🐎 สัตว์ขี่/พาหนะ",
            _ => "🛡️ สวมใส่"
        },
        3 => "🐎 ม้าศึก/พาหนะ",
        4 => "📜 ของเควสต์/พิเศษ",
        _ => "📦 ทั่วไป"
    };
    public int Slot => _def.Slot;
    public int Atk => _def.Atk;
    public int Def => _def.Def;
    public int Matk => _def.Matk;
    public int Mdef => _def.Mdef;
    public int Agi => _def.Agi;
    public int Hp => _def.Hp;
    public int Sp => _def.Sp;
    public int Level => _def.Level;
    public int Price => _def.Price;
    public ItemDef RawDef => _def;
}

public class NpcViewModel
{
    private readonly NpcDef _def;
    public NpcViewModel(NpcDef def) { _def = def; }

    public int Id => _def.Id;
    public string Name => _def.Name;
    public int Level => _def.Level;
    public int Element => _def.Element;
    public string ElementStr => _def.Element switch
    {
        1 => "🌍 ธาตุดิน",
        2 => "💧 ธาตุน้ำ",
        3 => "🔥 ธาตุไฟ",
        4 => "💨 ธาตุลม",
        _ => "⚪ ไร้ธาตุ"
    };
    public int Hp => _def.Hp;
    public int Sp => _def.Sp;
    public int Atk => _def.Atk;
    public int Def => _def.Def;
    public int Matk => _def.Matk;
    public int Mdef => _def.Mdef;
    public int Agi => _def.Agi;
    public string SkillsStr => string.Join(", ", _def.Skills.Select(s => s switch
    {
        101 => "⚔️ โจมตีธรรมดา",
        102 => "🪨 ขว้างหินถล่ม",
        103 => "💧 น้ำพุพุ่งสลาย",
        104 => "🔥 เพลิงเผาผลาญ",
        105 => "💨 คมมีดวายุคลั่ง",
        106 => "🐉 มังกรเขียวสะท้านภพ",
        107 => "⚡ ค่ายกลแปดทิศพิสดาร",
        108 => "❄️ คลื่นน้ำแข็งเสียดฟ้า",
        109 => "🛡️ กำแพงพสุธาไร้พ่าย",
        _ => $"สกิล #{s}"
    }));
    public NpcDef RawDef => _def;
}

public class GameFileViewModel
{
    public string FileName { get; set; } = string.Empty;
    public string FileSizeStr { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public string Status { get; set; } = "✅ โหลดพร้อมใช้งาน";
}

public class FashionViewModel
{
    private readonly FashionDef _def;
    public FashionViewModel(FashionDef def) { _def = def; }

    public int Id => _def.Id;
    public string Name => _def.Name;
    public string Category => _def.Category;
    public int Slot => _def.Slot;
    public int BonusHp => _def.BonusHp;
    public int BonusSp => _def.BonusSp;
    public int BonusAtk => _def.BonusAtk;
    public int BonusDef => _def.BonusDef;
    public int BonusMatk => _def.BonusMatk;
    public int BonusMdef => _def.BonusMdef;
    public int BonusAgi => _def.BonusAgi;
    public string SetBonusDesc => _def.SetBonusDesc;
    public string AppearanceDesc => _def.AppearanceDesc;
    public int Price => _def.Price;
    public FashionDef RawDef => _def;
}
