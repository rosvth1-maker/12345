using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using TsmClient.Data;
using TsmClient.Domain;
using TsmClient.GameLogic;
using TsmClient.Network;
using TsmClient.Protocol;

namespace TsmClient.App;

public partial class MainWindow : Window
{
    private readonly TcpGameClient _client = new();
    private readonly ClientGameState _state = new();
    private readonly ClientStorage _storage;
    private readonly ClientUpdateService _updateService = new();
    private ClientSettings _settings = new();
    private readonly DispatcherTimer _keepaliveTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private AssetCatalog? _assetCatalog;
    private NpcDialog? _activeDialog;

    public MainWindow()
    {
        InitializeComponent();
        _storage = new ClientStorage(AppContext.BaseDirectory);
        _client.Log += message => Dispatcher.Invoke(() => AddLog(message));
        _client.PacketReceived += packet => Dispatcher.Invoke(() => HandlePacket(packet));
        _client.Disconnected += () => Dispatcher.Invoke(() => SetConnected(false));
        _keepaliveTimer.Tick += async (_, _) => await SendKeepaliveAsync();
        GameView.MoveRequested += async (x, y) => await MoveAsync(x, y);
        ShowMapPreview(10801);
        Loaded += async (_, _) => await LoadSettingsAsync();
        Closed += async (_, _) => await _client.DisposeAsync();
        AddLog("เปิดตัวเกม TS Dark World Windows Client");
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _storage.LoadSettingsAsync();
        HostBox.Text = _settings.Host;
        PortBox.Text = _settings.Port.ToString();
        VersionBox.Text = _settings.ClientVersion.ToString();
        ServerDirectoryBox.Text = _settings.ServerDirectory;
        ProfileBox.ItemsSource = _settings.Profiles;
        ProfileBox.DisplayMemberPath = nameof(ConnectionProfile.Name);
        ProfileBox.SelectedItem = _settings.Profiles.FirstOrDefault(x => x.Name.Equals(_settings.ActiveProfile, StringComparison.OrdinalIgnoreCase)) ?? _settings.Profiles.FirstOrDefault();
        CdnUrlBox.Text = _settings.CdnBaseUrl;
        RenderQualitySlider.Value = _settings.RenderQuality;
        VolumeSlider.Value = _settings.MasterVolume;
        FullscreenBox.IsChecked = _settings.Fullscreen;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings)) return;
        SetBusy(true);
        try
        {
            _settings = settings;
            await _storage.SaveSettingsAsync(settings);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            _state.SetConnectionState(ClientConnectionState.Connecting);
            await _client.ConnectAsync(settings.Host, settings.Port, timeout.Token);
            SetConnected(true);
            await _client.SendAsync(ClientPackets.Handshake((ushort)settings.ClientVersion));
            AddLog($"ส่ง Handshake เวอร์ชัน {settings.ClientVersion} แล้ว");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            AddLog($"เชื่อมต่อไม่สำเร็จ: {ex.Message}");
            SetConnected(false);
        }
        finally { SetBusy(false); }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        string account = AccountBox.Text.Trim();
        string password = PasswordInput.Password;
        if (account.Length is < 3 or > 20 || password.Length is < 3 or > 20)
        {
            MessageBox.Show("บัญชีและรหัสผ่านต้องมี 3–20 ตัวอักษร", "ตรวจข้อมูล", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            await _client.SendAsync(ClientPackets.Login(account, password));
            AddLog("ส่งคำขอเข้าสู่ระบบแล้ว (ไม่บันทึกบัญชีและรหัสผ่าน)");
        }
        catch (Exception ex) { AddLog($"ส่งข้อมูลเข้าสู่ระบบไม่สำเร็จ: {ex.Message}"); }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await _client.DisconnectAsync();
        SetConnected(false);
        AddLog("ตัดการเชื่อมต่อแล้ว");
    }

    private async void SelectCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterListBox.SelectedItem is not CharacterSummary character)
        {
            MessageBox.Show("กรุณาเลือกตัวละคร", "เลือกตัวละคร", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            await _client.SendAsync(ClientPackets.SelectCharacter(character.Id));
            SelectCharacterButton.IsEnabled = false;
            AddLog($"ส่งคำขอเข้าเกมด้วยตัวละครรหัส {character.Id}");
        }
        catch (Exception ex) { AddLog($"ส่งคำขอเข้าเกมไม่สำเร็จ: {ex.Message}"); }
    }

    private async void CreateCharacter_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            byte gender = (byte)GenderBox.SelectedIndex;
            byte element = (byte)ElementBox.SelectedIndex;
            if (!byte.TryParse(HairBox.Text, out byte hair) || hair > 9 ||
                !byte.TryParse(HairColorBox.Text, out byte hairColor) || hairColor > 9 ||
                !byte.TryParse(SkinColorBox.Text, out byte skinColor) || skinColor > 9)
                throw new ArgumentException("ทรงผม สีผม และสีผิวต้องอยู่ระหว่าง 0–9");

            var creation = new CharacterCreation(CreateNameBox.Text, gender, element, hair, hairColor, skinColor);
            await _client.SendAsync(ClientPackets.CreateCharacter(creation));
            CreateCharacterButton.IsEnabled = false;
            CharacterFormStatus.Text = "ส่งคำขอสร้างตัวละครแล้ว กำลังรอรายชื่อใหม่...";
            AddLog("ส่งคำขอสร้างตัวละครแล้ว");
        }
        catch (Exception ex)
        {
            CharacterFormStatus.Text = ex.Message;
            AddLog($"สร้างตัวละครไม่ได้: {ex.Message}");
        }
    }

    private void HandlePacket(GamePacket packet)
    {
        AddLog($"รับแพ็กเก็ต main={packet.MainKind} sub={packet.SubKind} ขนาด={packet.Payload.Length} ไบต์");
        if (packet.MainKind == 1 && packet.SubKind == 16)
        {
            _state.SetConnectionState(ClientConnectionState.HandshakeComplete);
            FooterStatus.Text = "Handshake สำเร็จ — พร้อมเข้าสู่ระบบ";
            LoginButton.IsEnabled = true;
            if (!_keepaliveTimer.IsEnabled) _keepaliveTimer.Start();
            AddLog("เซิร์ฟเวอร์ตอบรับ Handshake แล้ว");
        }
        else if (packet.MainKind == 1 && packet.SubKind == 1 && packet.Payload.Length >= 33)
        {
            var reader = new PacketReader(packet.Payload);
            bool success = reader.ReadByte() == 1;
            string message = reader.ReadFixedString(32);
            AddLog(message);
            if (success)
            {
                _state.SetConnectionState(ClientConnectionState.Authenticated);
                HeaderStatus.Text = "● เข้าสู่ระบบแล้ว";
                HeaderStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                FooterStatus.Text = "เข้าสู่ระบบสำเร็จ — รอรายชื่อตัวละคร";
            }
        }
        else if (packet.MainKind == ClientPackets.CharacterMain && packet.SubKind == 1)
        {
            ParseCharacterList(packet.Payload);
        }
        else if (packet.MainKind == ClientPackets.CharacterMain && packet.SubKind == 2)
        {
            ParseEnterGame(packet.Payload);
        }
        else if (packet.MainKind == ClientPackets.KeepaliveMain && packet.SubKind == 1)
        {
            AddLog("ได้รับ Keepalive จากเซิร์ฟเวอร์");
        }
        else if (packet.MainKind == ClientPackets.SceneMain && packet.SubKind == 1)
        {
            ParseScene(packet.Payload);
        }
        else if (packet.MainKind == ClientPackets.MoveMain && packet.SubKind == 1)
        {
            ParseMovement(packet.Payload);
        }
        else if (packet.MainKind == ClientPackets.MapNpcMain && packet.SubKind == 2) ParseNpcDialog(packet.Payload);
        else if (packet.MainKind == ClientPackets.MissionMain && packet.SubKind == 1) ParseQuest(packet.Payload);
        else if (packet.MainKind == ClientPackets.BattleStatusMain && packet.SubKind == 1) ParseBattleStart(packet.Payload);
        else if (packet.MainKind == 51 && packet.SubKind == 2) ParseBattleAction(packet.Payload);
        else if (packet.MainKind == ClientPackets.BattleStatusMain && packet.SubKind == 2) ParseBattleReward(packet.Payload);
        else if (packet.MainKind == ClientPackets.BattleStatusMain && packet.SubKind == 3) ParseBattleEscape();
        else if (packet.MainKind == ClientPackets.InventoryMain && packet.SubKind == 1) ParseInventory(packet.Payload);
        else if (packet.MainKind == 100 && packet.SubKind == 1) ParseCoreSystem(packet.Payload);
        else if (packet.MainKind == 15 && packet.SubKind == 1) AddCoreResult(ServerPackets.ReadPet(packet.Payload).Name);
        else if (packet.MainKind == 13 && packet.SubKind == 1) AddCoreResult($"ปาร์ตี้: {ServerPackets.ReadTeam(packet.Payload).Name}");
        else if (packet.MainKind == 2 && packet.SubKind == 1) { var chat = ServerPackets.ReadChat(packet.Payload); ChatOutput.Text = $"{chat.Sender}: {chat.Message}"; }
    }

    private async Task SendKeepaliveAsync()
    {
        if (!_client.IsConnected) { _keepaliveTimer.Stop(); return; }
        try { await _client.SendAsync(ClientPackets.Keepalive()); }
        catch (Exception ex) { AddLog($"ส่ง Keepalive ไม่สำเร็จ: {ex.Message}"); }
    }

    private void ParseCharacterList(byte[] payload)
    {
        try
        {
            var reader = new PacketReader(payload);
            int count = reader.ReadByte();
            var characters = new List<CharacterSummary>();
            for (int i = 0; i < count; i++)
            {
                characters.Add(new CharacterSummary(reader.ReadInt32(), reader.ReadFixedString(16), reader.ReadByte(),
                    reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadInt32()));
            }
            _state.SetCharacters(characters);
            CharacterListBox.ItemsSource = characters;
            if (characters.Count > 0) CharacterListBox.SelectedIndex = 0;
            SelectCharacterButton.IsEnabled = characters.Count > 0;
            CreateCharacterButton.IsEnabled = true;
            CharacterFormStatus.Text = characters.Count == 0 ? "บัญชีนี้ยังไม่มีตัวละคร สามารถสร้างใหม่ได้" : $"พบตัวละคร {characters.Count} ตัว";
            CharacterInfo.Text = count == 0 ? "บัญชีนี้ยังไม่มีตัวละคร" : string.Join("\n", characters.Select(c => $"{c.Name}  Lv.{c.Level}  แผนที่ {c.MapId}"));
            AddLog($"ได้รับรายชื่อตัวละคร {count} ตัว");
            MainTabs.SelectedIndex = 1;
        }
        catch (Exception ex) { AddLog($"อ่านรายชื่อตัวละครไม่สำเร็จ: {ex.Message}"); }
    }

    private void ParseEnterGame(byte[] payload)
    {
        try
        {
            var reader = new PacketReader(payload);
            int characterId = reader.ReadInt32();
            string name = reader.ReadFixedString(16);
            byte level = reader.ReadByte();
            byte element = reader.ReadByte();
            int hp = reader.ReadInt32();
            int maxHp = reader.ReadInt32();
            int sp = reader.ReadInt32();
            int maxSp = reader.ReadInt32();
            for (int i = 0; i < 8; i++) reader.ReadInt32();
            long exp = reader.ReadInt64();
            long gold = reader.ReadInt64();
            int mapId = reader.ReadInt32();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            _state.SetConnectionState(ClientConnectionState.InGame);
            _state.EnterWorld(characterId, name, mapId, x, y);
            RefreshWorldView();
            WarpButton.IsEnabled = true;
            StartBattleButton.IsEnabled = true;
            CharacterInfo.Text = $"{name}  Lv.{level}\nHP {hp:N0}/{maxHp:N0}\nSP {sp:N0}/{maxSp:N0}\nEXP {exp:N0}  เงิน {gold:N0}\nแผนที่ {mapId}  ({x}, {y})\nรหัสตัวละคร {characterId}  ธาตุ {element}";
            HeaderStatus.Text = "● อยู่ในเกม";
            FooterStatus.Text = $"{name} — แผนที่ {mapId}";
            AddLog($"เข้าเกมสำเร็จ: {name} แผนที่ {mapId} ({x}, {y})");
            MainTabs.SelectedIndex = 2;
        }
        catch (Exception ex) { AddLog($"อ่านข้อมูลเข้าเกมไม่สำเร็จ: {ex.Message}"); }
    }

    private void ParseScene(byte[] payload)
    {
        try
        {
            SceneSnapshot scene = ServerPackets.ReadScene(payload);
            _state.SetScene(scene);
            ApplySceneAsset(scene.MapId);
            NpcListBox.ItemsSource = _state.Npcs;
            RefreshWorldView();
            SceneInfo.Text = $"แผนที่ {scene.MapId}\nตำแหน่ง {_state.Position.X}, {_state.Position.Y}\nผู้เล่นอื่น {scene.Players.Count} คน\nNPC {scene.Npcs.Count} ตัว";
            FooterStatus.Text = $"{_state.LocalCharacterName} — แผนที่ {scene.MapId} — ผู้เล่น {scene.Players.Count + 1} คน";
            AddLog($"รับ Scene แผนที่ {scene.MapId}: ผู้เล่นอื่น {scene.Players.Count} คน, NPC {_state.Npcs.Count} ตัว");
        }
        catch (Exception ex) { AddLog($"อ่าน Scene ไม่สำเร็จ: {ex.Message}"); }
    }

    private void ParseMovement(byte[] payload)
    {
        try
        {
            PlayerMovement movement = ServerPackets.ReadMovement(payload);
            _state.ApplyMovement(movement);
            RefreshWorldView();
            SceneInfo.Text = $"แผนที่ {_state.Position.MapId}\nตำแหน่ง {_state.Position.X}, {_state.Position.Y}\nผู้เล่นอื่น {_state.ScenePlayers.Count} คน";
        }
        catch (Exception ex) { AddLog($"อ่านตำแหน่งผู้เล่นไม่สำเร็จ: {ex.Message}"); }
    }

    private async Task MoveAsync(short x, short y)
    {
        if (_state.ConnectionState != ClientConnectionState.InGame) return;
        try
        {
            await _client.SendAsync(ClientPackets.Move(x, y));
            _state.SetLocalPosition(x, y);
            RefreshWorldView();
            SceneInfo.Text = $"แผนที่ {_state.Position.MapId}\nตำแหน่ง {x}, {y}\nผู้เล่นอื่น {_state.ScenePlayers.Count} คน";
            AddLog($"ส่งตำแหน่งเดิน ({x}, {y})");
        }
        catch (Exception ex) { AddLog($"ส่งตำแหน่งเดินไม่สำเร็จ: {ex.Message}"); }
    }

    private async void Warp_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WarpMapBox.Text, out int mapId) || !short.TryParse(WarpXBox.Text, out short x) || !short.TryParse(WarpYBox.Text, out short y) || x < 0 || y < 0)
        {
            MessageBox.Show("กรุณาระบุ Map ID, X และ Y ให้ถูกต้อง", "ข้อมูลวาร์ปไม่ถูกต้อง", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            await _client.SendAsync(ClientPackets.Warp(mapId, x, y));
            AddLog($"ส่งคำขอวาร์ปไปแผนที่ {mapId} ({x}, {y})");
        }
        catch (Exception ex) { AddLog($"ส่งคำขอวาร์ปไม่สำเร็จ: {ex.Message}"); }
    }

    private void RefreshWorldView()
    {
        GameView.SetWorld(_state.Position.X, _state.Position.Y, _state.ScenePlayers.Values.Select(p => (p.CharacterId, p.Name, p.X, p.Y)));
        GameView.SetNpcs(_state.Npcs.Select(n => (n.NpcId, n.Name, n.X, n.Y)));
    }

    private void ShowMapPreview(int mapId)
    {
        var npcs = SceneNpcRegistry.ForMap(mapId);
        GameView.SetSceneAsset(mapId, null, false);
        GameView.SetWorld(570, 770, []);
        GameView.SetNpcs(npcs.Select(n => (n.NpcId, n.Name, n.X, n.Y)));
        NpcListBox.ItemsSource = npcs;
        SceneInfo.Text = $"ตัวอย่างแผนที่ {mapId}\nเชื่อมต่อและเข้าตัวละครเพื่อรับข้อมูลจริงจาก Server\nNPC {npcs.Count} ตัว";
    }

    private void NpcList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        TalkNpcButton.IsEnabled = NpcListBox.SelectedItem is NpcPlacement && _state.ConnectionState == ClientConnectionState.InGame;

    private async void TalkNpc_Click(object sender, RoutedEventArgs e)
    {
        if (NpcListBox.SelectedItem is NpcPlacement npc) await _client.SendAsync(ClientPackets.TalkToNpc(npc.NpcId));
    }

    private void ParseNpcDialog(byte[] payload)
    {
        try
        {
            _activeDialog = ServerPackets.ReadNpcDialog(payload);
            NpcDialogText.Text = $"{_activeDialog.NpcName}: {_activeDialog.Text}\nตัวเลือก: {string.Join(" / ", _activeDialog.Options)}";
            AcceptQuestButton.IsEnabled = _activeDialog.QuestId > 0;
            CompleteQuestButton.IsEnabled = false;
            AddLog($"สนทนากับ {_activeDialog.NpcName}");
        }
        catch (Exception ex) { AddLog($"อ่านบทสนทนาไม่สำเร็จ: {ex.Message}"); }
    }

    private async void AcceptQuest_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDialog is { QuestId: > 0 } d) await _client.SendAsync(ClientPackets.Mission(d.QuestId, 1));
    }
    private async void CompleteQuest_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDialog is { QuestId: > 0 } d) await _client.SendAsync(ClientPackets.Mission(d.QuestId, 2));
    }
    private void ParseQuest(byte[] payload)
    {
        try
        {
            QuestState quest = ServerPackets.ReadQuest(payload); _state.SetQuest(quest);
            QuestListBox.ItemsSource = _state.Quests.Values.ToArray();
            NpcDialogText.Text = $"{quest.Title}: {quest.Message}";
            CompleteQuestButton.IsEnabled = quest.Status == 1;
            AcceptQuestButton.IsEnabled = false;
            AddLog($"ภารกิจ {quest.MissionId}: {quest.Message}");
        }
        catch (Exception ex) { AddLog($"อ่านสถานะภารกิจไม่สำเร็จ: {ex.Message}"); }
    }

    private async Task SendBattleCommandAsync(byte action, ushort skillId = 101)
    {
        try { await _client.SendAsync(ClientPackets.BattleCommand(action, 0, 0, skillId)); }
        catch (Exception ex) { AddLog($"ส่งคำสั่งต่อสู้ไม่สำเร็จ: {ex.Message}"); }
    }
    private async void StartBattle_Click(object sender, RoutedEventArgs e) => await SendBattleCommandAsync(0);
    private async void Attack_Click(object sender, RoutedEventArgs e) => await SendBattleCommandAsync(1);
    private async void Skill_Click(object sender, RoutedEventArgs e) => await SendBattleCommandAsync(2, 104);
    private async void Guard_Click(object sender, RoutedEventArgs e) => await SendBattleCommandAsync(3);
    private async void Escape_Click(object sender, RoutedEventArgs e) => await SendBattleCommandAsync(4);
    private async void BattleItem_Click(object sender, RoutedEventArgs e) => await SendBattleCommandAsync(5);

    private void ParseBattleStart(byte[] payload)
    {
        BattleState battle = ServerPackets.ReadBattleStart(payload); _state.StartBattle(battle);
        BattlePlayerText.Text = $"{_state.LocalCharacterName}\nHP {battle.PlayerHp:N0}/{battle.PlayerMaxHp:N0}\nSP {battle.PlayerSp:N0}/{battle.PlayerMaxSp:N0}";
        BattleEnemyText.Text = $"{battle.EnemyName}\nHP {battle.EnemyHp:N0}/{battle.EnemyMaxHp:N0}";
        CompanionText.Text = battle.CompanionPresent ? "ขุนพลร่วมต่อสู้: บาโตวเยา" : "ขุนพลร่วมต่อสู้: —";
        EnemyHpBar.Maximum = battle.EnemyMaxHp; EnemyHpBar.Value = battle.EnemyHp;
        SetBattleControls(true); StartBattleButton.IsEnabled = false; MainTabs.SelectedIndex = 3;
        BattleResultText.Text = "เริ่มการต่อสู้แล้ว กรุณาเลือกคำสั่ง";
    }
    private void ParseBattleAction(byte[] payload)
    {
        BattleActionResult result = ServerPackets.ReadBattleAction(payload); _state.ApplyBattleAction(result);
        EnemyHpBar.Value = result.EnemyHp;
        if (_state.Battle is { } b) BattleEnemyText.Text = $"{b.EnemyName}\nHP {b.EnemyHp:N0}/{b.EnemyMaxHp:N0}";
        BattleResultText.Text = $"{result.ActionName}: สร้างความเสียหาย {result.Damage:N0}";
    }
    private void ParseBattleReward(byte[] payload)
    {
        BattleReward reward = ServerPackets.ReadBattleReward(payload); _state.EndBattle(reward); SetBattleControls(false);
        StartBattleButton.IsEnabled = true; EnemyHpBar.Value = 0;
        BattleResultText.Text = $"ชนะการต่อสู้!\nEXP +{reward.Exp:N0}\nเงิน +{reward.Gold:N0}\nไอเทม {reward.ItemId}\nEXP รวม {reward.TotalExp:N0}";
    }
    private void ParseBattleEscape()
    {
        _state.EndBattle(); SetBattleControls(false); StartBattleButton.IsEnabled = true;
        BattleResultText.Text = "หนีสำเร็จ กลับสู่สถานะปลอดภัย";
    }
    private void SetBattleControls(bool enabled) => AttackButton.IsEnabled = SkillButton.IsEnabled = ItemButton.IsEnabled = GuardButton.IsEnabled = EscapeButton.IsEnabled = enabled;

    private void ParseInventory(byte[] payload)
    {
        try { _state.SetInventory(ServerPackets.ReadInventory(payload)); InventoryListBox.ItemsSource = _state.Inventory; }
        catch (Exception ex) { AddLog($"อ่านกระเป๋าไม่สำเร็จ: {ex.Message}"); }
    }
    private async Task ItemOp(byte op) { if (InventoryListBox.SelectedItem is InventoryItem item) await _client.SendAsync(ClientPackets.ItemOperation(op, item.Slot)); }
    private async void UseItem_Click(object sender, RoutedEventArgs e) => await ItemOp(1);
    private async void EquipItem_Click(object sender, RoutedEventArgs e) => await ItemOp(2);
    private async void UnequipItem_Click(object sender, RoutedEventArgs e) => await ItemOp(3);
    private async void DiscardItem_Click(object sender, RoutedEventArgs e) => await ItemOp(4);
    private void ParseCoreSystem(byte[] payload) { var result = ServerPackets.ReadCoreSystem(payload); _state.AddSystemResult(result); CoreSystemList.ItemsSource = null; CoreSystemList.ItemsSource = _state.SystemHistory; AddLog(result.Message); }
    private void AddCoreResult(string message) { _state.AddSystemResult(new CoreSystemResult(0, true, 0, message)); CoreSystemList.ItemsSource = null; CoreSystemList.ItemsSource = _state.SystemHistory; }
    private Task Core(byte action, int value = 0, short count = 1, string? text = null, long amount = 0) => _client.SendAsync(ClientPackets.CoreSystem(action, value, count, text, amount));
    private async void UpgradeSkill_Click(object s, RoutedEventArgs e) => await Core(1, 101);
    private async void PetRoster_Click(object s, RoutedEventArgs e) => await Core(2);
    private async void DeployPet_Click(object s, RoutedEventArgs e) => await _client.SendAsync(ClientPackets.PetCommand(1, 1));
    private async void InviteTeam_Click(object s, RoutedEventArgs e) => await _client.SendAsync(ClientPackets.TeamCommand(1, 1));
    private async void BuyItem_Click(object s, RoutedEventArgs e) => await Core(3, 10001, 1);
    private async void Deposit_Click(object s, RoutedEventArgs e) => await Core(4, amount: 100);
    private async void Withdraw_Click(object s, RoutedEventArgs e) => await Core(4, amount: -50);
    private async void AddFriend_Click(object s, RoutedEventArgs e) => await Core(5, 1);
    private async void SendMail_Click(object s, RoutedEventArgs e) => await Core(6, text: "จดหมายทดสอบภาษาไทย");
    private async void Trade_Click(object s, RoutedEventArgs e) => await Core(7, 10001, 1);
    private async void Guild_Click(object s, RoutedEventArgs e) => await Core(8, text: "กิลด์โลกมืด");
    private async void Leaderboard_Click(object s, RoutedEventArgs e) => await Core(9);
    private async void SendChat_Click(object s, RoutedEventArgs e) => await _client.SendAsync(ClientPackets.Chat(0, ChatInput.Text));

    private async void ScanAssets_Click(object sender, RoutedEventArgs e)
    {
        string path = AssetPathBox.Text.Trim();
        ScanAssetsButton.IsEnabled = false;
        var progress = new Progress<(int Done, int Total, string File)>(p =>
        {
            if (p.Done == 1 || p.Done == p.Total || p.Done % 50 == 0)
                AssetReportBox.Text = $"กำลังคำนวณ SHA-256... {p.Done:N0}/{p.Total:N0}\n{p.File}";
        });
        try
        {
            _assetCatalog = await _storage.BuildAssetCatalogAsync(path, progress);
            var report = new StringBuilder()
                .AppendLine("ASSET CATALOG — READ ONLY")
                .AppendLine($"ต้นฉบับ: {_assetCatalog.SourceRoot}")
                .AppendLine($"Cache: {_storage.AssetCacheDirectory}")
                .AppendLine($"จำนวนไฟล์: {_assetCatalog.FileCount:N0}")
                .AppendLine($"ขนาดรวม: {_assetCatalog.TotalBytes / 1024d / 1024d / 1024d:N2} GB")
                .AppendLine($"ฉากที่ตรวจพบ: {_assetCatalog.Scenes.Count:N0}")
                .AppendLine($"ชื่อไทยตรง Server: {_assetCatalog.ThaiNames.Count:N0}")
                .AppendLine().AppendLine("ประเภทไฟล์:");
            foreach (var item in _assetCatalog.ExtensionCounts.OrderByDescending(x => x.Value)) report.AppendLine($"{item.Key,-15} {item.Value,8:N0}");
            report.AppendLine().AppendLine("ชื่อไทย Item/NPC/Skill (ตรงกับ Server):");
            foreach (var item in _assetCatalog.ThaiNames) report.AppendLine($"{item.Kind,-6} {item.Id,6}  {item.Name}");
            report.AppendLine().AppendLine("ฉากตัวอย่าง:");
            foreach (var scene in _assetCatalog.Scenes.Where(x => x.MapId is 10801 or 10802))
                report.AppendLine($"Map {scene.MapId}: {scene.RelativePath}  {scene.Length:N0} bytes  SHA-256 {scene.Sha256}");
            AssetReportBox.Text = report.ToString();
            ApplySceneAsset(_state.Position.MapId == 0 ? 10801 : _state.Position.MapId);
            AddLog($"สร้าง Asset Catalog พร้อม SHA-256 แล้ว: {_assetCatalog.FileCount:N0} ไฟล์ (ต้นฉบับ read-only)");
        }
        catch (Exception ex) { AssetReportBox.Text = $"สร้าง Catalog ไม่สำเร็จ: {ex.Message}"; AddLog(AssetReportBox.Text); }
        finally { ScanAssetsButton.IsEnabled = true; }
    }

    private void ApplySceneAsset(int mapId)
    {
        SceneAsset? scene = _assetCatalog?.Scenes.FirstOrDefault(x => x.MapId == mapId);
        GameView.SetSceneAsset(mapId, scene?.RelativePath, scene?.NativeRenderingSupported ?? false);
    }

    private void ProfileBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not ConnectionProfile profile) return;
        HostBox.Text = profile.Host; PortBox.Text = profile.Port.ToString(); CdnUrlBox.Text = profile.CdnBaseUrl;
    }

    private void SyncServerDirectory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var link = ServerLinkService.Read(ServerDirectoryBox.Text.Trim());
            HostBox.Text = link.Host; PortBox.Text = link.Port.ToString(); VersionBox.Text = link.ClientVersion.ToString(); CdnUrlBox.Text = link.CdnBaseUrl;
            var local = new ConnectionProfile("Local", link.Host, link.Port, link.CdnBaseUrl);
            int index = _settings.Profiles.FindIndex(x => x.Name == "Local"); if (index >= 0) _settings.Profiles[index] = local; else _settings.Profiles.Insert(0, local);
            ProfileBox.Items.Refresh(); ProfileBox.SelectedItem = local;
            QualityReportBox.Text = $"เชื่อมโยง Server สำเร็จ\nGame: {link.Host}:{link.Port}\nClient version: {link.ClientVersion}\nCDN: {link.CdnBaseUrl}\nServer data: {link.DataDirectory}\n\nไม่ได้อ่านหรือบันทึกข้อมูล Database credentials";
        }
        catch (Exception ex) { QualityReportBox.Text = $"เชื่อมโยง Server ไม่สำเร็จ: {ex.Message}"; }
    }

    private async void SaveQualitySettings_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings)) return;
        settings.ServerDirectory = ServerDirectoryBox.Text.Trim(); settings.ActiveProfile = (ProfileBox.SelectedItem as ConnectionProfile)?.Name ?? "Local";
        settings.CdnBaseUrl = CdnUrlBox.Text.Trim(); settings.RenderQuality = (int)RenderQualitySlider.Value; settings.MasterVolume = (int)VolumeSlider.Value;
        settings.Fullscreen = FullscreenBox.IsChecked == true; settings.Profiles = _settings.Profiles;
        _settings = settings; await _storage.SaveSettingsAsync(settings);
        WindowState = settings.Fullscreen ? WindowState.Maximized : WindowState.Normal;
        QualityReportBox.Text = "บันทึกการตั้งค่าแล้ว"; AddLog("บันทึกโปรไฟล์และการตั้งค่าคุณภาพแล้ว");
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            QualityReportBox.Text = "กำลังตรวจ manifest จาก CDN...";
            var result = await _updateService.CheckAsync(CdnUrlBox.Text.Trim(), AppContext.BaseDirectory);
            QualityReportBox.Text = $"เวอร์ชันบน CDN: {result.Version}\nไฟล์ที่ต้องอัปเดต: {result.RequiredFiles:N0}\nขนาดดาวน์โหลด: {result.DownloadBytes / 1024d / 1024d:N2} MB\n" + string.Join("\n", result.Files.Select(x => x.Path));
        }
        catch (Exception ex) { QualityReportBox.Text = $"ตรวจอัปเดตไม่สำเร็จ: {ex.Message}"; }
    }

    private void CapturePerformance_Click(object sender, RoutedEventArgs e)
    {
        var p = PerformanceSnapshot.Capture();
        QualityReportBox.Text = $"Performance Snapshot\nWorking Set: {p.WorkingSetMb:N2} MB\nManaged: {p.ManagedBytes / 1024d / 1024d:N2} MB\nCPU: {p.CpuTime.TotalSeconds:N2} s\nGC: Gen0={p.Gen0}, Gen1={p.Gen1}, Gen2={p.Gen2}";
    }

    private bool TryReadSettings(out ClientSettings settings)
    {
        settings = new ClientSettings();
        if (string.IsNullOrWhiteSpace(HostBox.Text) || !int.TryParse(PortBox.Text, out int port) || port is < 1 or > 65535 ||
            !int.TryParse(VersionBox.Text, out int version) || version is < 1 or > 65535)
        {
            MessageBox.Show("กรุณาตรวจ IP, พอร์ต และเวอร์ชัน Client", "ข้อมูลไม่ถูกต้อง", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        settings = new ClientSettings { Host = HostBox.Text.Trim(), Port = port, ClientVersion = version, AssetDirectory = AssetPathBox.Text.Trim(), ServerDirectory = ServerDirectoryBox.Text.Trim(), CdnBaseUrl = CdnUrlBox.Text.Trim(), Profiles = _settings.Profiles };
        return true;
    }

    private void SetBusy(bool busy) => TestButton.IsEnabled = !busy;
    private void SetConnected(bool connected)
    {
        if (!connected) _keepaliveTimer.Stop();
        if (!connected)
        {
            CharacterListBox.ItemsSource = null;
            SelectCharacterButton.IsEnabled = false;
            CreateCharacterButton.IsEnabled = false;
            WarpButton.IsEnabled = false;
            StartBattleButton.IsEnabled = false;
            SetBattleControls(false);
            _state.EndBattle();
            BattleResultText.Text = "การเชื่อมต่อสิ้นสุด — กลับสู่สถานะปลอดภัย";
            SceneInfo.Text = "ยังไม่ได้รับข้อมูล Scene";
            NpcListBox.ItemsSource = null;
            QuestListBox.ItemsSource = null;
            TalkNpcButton.IsEnabled = AcceptQuestButton.IsEnabled = CompleteQuestButton.IsEnabled = false;
        }
        _state.SetConnectionState(connected ? ClientConnectionState.Connected : ClientConnectionState.Disconnected);
        HeaderStatus.Text = connected ? "● เชื่อมต่อแล้ว" : "● ยังไม่เชื่อมต่อ";
        HeaderStatus.Foreground = connected ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.IndianRed;
        FooterStatus.Text = connected ? $"เชื่อมต่อ {_settings.Host}:{_settings.Port}" : "พร้อมใช้งาน — ยังไม่ได้เชื่อมต่อ";
        LoginButton.IsEnabled = false;
    }
    private void AddLog(string message)
    {
        LogList.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (LogList.Items.Count > 300) LogList.Items.RemoveAt(LogList.Items.Count - 1);
    }
}
