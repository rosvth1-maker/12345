using TsmServer.App.Response;
using TsmServer.Data;

namespace TsmServer.Tests;

public class SceneNpcCatalogTests
{
    [Fact]
    public void BuiltInScene_ContainsExpectedNpcs()
    {
        var data = new GameDataManager("missing-test-data");
        data.Initialize();

        var town = SceneNpcCatalog.Resolve(data, 10801);
        var field = SceneNpcCatalog.Resolve(data, 10802);

        Assert.Contains(town, x => x.NpcId == 10001);
        Assert.Contains(field, x => x.NpcId == 11001);
        Assert.Contains(town, x => x.NpcId == 10003 && x.Type == SceneNpcType.Shop);
        Assert.Contains(field, x => x.NpcId == 11001 && x.Type == SceneNpcType.Monster);
    }

    [Theory]
    [InlineData(570, 770, 530, 730, true)]
    [InlineData(0, 0, 530, 730, false)]
    public void TalkRange_IsValidated(int playerX, int playerY, int npcX, int npcY, bool expected)
    {
        var npc = new SceneNpcInfo(10001, "NPC", npcX, npcY, 5001);
        Assert.Equal(expected, SceneNpcCatalog.IsWithinTalkRange(playerX, playerY, npc));
    }
}
