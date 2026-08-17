using TsmServer.Data;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Systems;

namespace TsmServer.App.GM;

public class GmCommandProcessor
{
    private readonly IResponseSender _responseSender;
    private readonly WorldManager _worldManager;
    private readonly InventorySystem _inventorySystem;
    private readonly ICharacterRepository _charRepo;
    private readonly GameDataManager _dataManager;

    public GmCommandProcessor(
        IResponseSender responseSender,
        WorldManager worldManager,
        InventorySystem inventorySystem,
        ICharacterRepository charRepo,
        GameDataManager dataManager)
    {
        _responseSender = responseSender;
        _worldManager = worldManager;
        _inventorySystem = inventorySystem;
        _charRepo = charRepo;
        _dataManager = dataManager;
    }

    public async ValueTask ExecuteAsync(IGameSession session, string input)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string cmd = parts[0].ToLower();
        var args = parts.Skip(1).ToArray();

        try
        {
            switch (cmd)
            {
                case "/additem" or "/เสกไอเทม" or "/เพิ่มไอเทม":
                    if (args.Length >= 1 && int.TryParse(args[0], out int itemId))
                    {
                        int count = args.Length >= 2 && int.TryParse(args[1], out int c) ? c : 1;
                        await _inventorySystem.AddItemAsync(session.CharacterId, itemId, count);
                        var items = await _inventorySystem.GetBagItemsAsync(session.CharacterId);
                        await _responseSender.SendInventoryAsync(session, items);
                        await _responseSender.SendSystemNoticeAsync(session, $"[GM] ได้รับไอเทมรหัส: {itemId} x{count}");
                    }
                    break;

                case "/setgold" or "/เสกเงิน" or "/ปรับเงิน":
                    if (args.Length >= 1 && long.TryParse(args[0], out long gold))
                    {
                        await _charRepo.UpdateGoldAsync(session.CharacterId, gold);
                        if (session.PlayerData != null) session.PlayerData = session.PlayerData with { Gold = gold };
                        await _responseSender.SendGoldUpdateAsync(session, gold);
                        await _responseSender.SendSystemNoticeAsync(session, $"[GM] ปรับยอดเงินทองเป็น: {gold:N0}");
                    }
                    break;

                case "/tp" or "/teleport" or "/warp" or "/วาร์ป":
                    if (args.Length >= 1 && int.TryParse(args[0], out int mapId))
                    {
                        int x = args.Length >= 2 && int.TryParse(args[1], out int tx) ? tx : 500;
                        int y = args.Length >= 3 && int.TryParse(args[2], out int ty) ? ty : 500;
                        _worldManager.EnterMap(mapId, session);
                        session.X = x;
                        session.Y = y;
                        if (session.PlayerData != null) session.PlayerData = session.PlayerData with { MapId = mapId, X = x, Y = y };
                        await _charRepo.UpdatePositionAsync(session.CharacterId, mapId, x, y);
                        var others = _worldManager.GetPlayersInMap(mapId, session.SessionId);
                        await _responseSender.SendSceneInfoAsync(session, mapId, others);
                        await _responseSender.SendSystemNoticeAsync(session, $"[GM] วาร์ปไปยังแผนที่: {mapId} ({x}, {y}) เรียบร้อย");
                    }
                    break;

                case "/level" or "/setlevel" or "/ปรับเลเวล":
                    if (args.Length >= 1 && int.TryParse(args[0], out int lv))
                    {
                        if (session.PlayerData != null)
                        {
                            session.PlayerData = session.PlayerData with { Level = lv };
                            await _charRepo.SavePlayerDataAsync(session.PlayerData);
                            await _responseSender.SendEnterGameAsync(session, session.PlayerData);
                            await _responseSender.SendSystemNoticeAsync(session, $"[GM] ปรับเลเวลเป็น Lv.{lv} เรียบร้อย");
                        }
                    }
                    break;

                case "/info" or "/ข้อมูล":
                    await _responseSender.SendSystemNoticeAsync(session, $"[ข้อมูล GM] ตัวละคร: {session.CharacterName} (รหัส: {session.CharacterId}) | แผนที่: {session.CurrentMapId} ({session.X},{session.Y}) | สิทธิ์ GM: {session.GmLevel}");
                    break;

                default:
                    await _responseSender.SendSystemNoticeAsync(session, $"[GM] ไม่พบคำสั่ง: {cmd}");
                    break;
            }
        }
        catch (Exception ex)
        {
            await _responseSender.SendSystemNoticeAsync(session, $"[ข้อผิดพลาด GM] {ex.Message}");
        }
    }
}
