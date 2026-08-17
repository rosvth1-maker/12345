using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TsmServer.App.GM;
using TsmServer.App.Handlers;
using TsmServer.App.Response;
using TsmServer.Data;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Systems;
using TsmServer.Network;
using TsmServer.Persistence;
using TsmServer.Persistence.Repositories;

Console.WriteLine("==================================================");
Console.WriteLine("   TS Online Mobile C# .NET 9 Server (tsm_dodo)   ");
Console.WriteLine("==================================================");

var builder = Host.CreateApplicationBuilder(args);

// 1. Data Layer
string gamedataPath = builder.Configuration["Game:ServerDataPath"] ?? "gamedata";
var gameDataManager = new GameDataManager(gamedataPath);
gameDataManager.Initialize();
builder.Services.AddSingleton(gameDataManager);

// 2. Persistence Layer (In-Memory default with MySQL capability)
var inMemoryStore = new InMemoryDataStore();
builder.Services.AddSingleton<IAccountRepository>(inMemoryStore);
builder.Services.AddSingleton<ICharacterRepository>(inMemoryStore);
builder.Services.AddSingleton<IInventoryRepository>(inMemoryStore);
builder.Services.AddSingleton<IPetRepository>(inMemoryStore);
builder.Services.AddSingleton<IQuestRepository>(inMemoryStore);
builder.Services.AddSingleton<IBitFlagRepository>(inMemoryStore);

// 3. GameLogic Layer
builder.Services.AddSingleton<WorldManager>();
builder.Services.AddSingleton<InventorySystem>();

// 4. Network Layer
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<IResponseSender, ResponseSenderImpl>();
builder.Services.AddSingleton<GmCommandProcessor>();

// 5. Packet Handlers & Dispatcher
builder.Services.AddSingleton<VersionCheckHandler>();
builder.Services.AddSingleton<ServerTimeHandler>();
builder.Services.AddSingleton<KeepaliveHandler>();
builder.Services.AddSingleton<LoginHandler>();
builder.Services.AddSingleton<CreateCharHandler>();
builder.Services.AddSingleton<SelectCharHandler>();
builder.Services.AddSingleton<MoveHandler>();
builder.Services.AddSingleton<WarpHandler>();
builder.Services.AddSingleton<ItemOperationHandler>();
builder.Services.AddSingleton<NpcTalkHandler>();
builder.Services.AddSingleton<NpcEventHandler>();
builder.Services.AddSingleton<MissionHandler>();
builder.Services.AddSingleton<CoreGameSystemsHandler>();
builder.Services.AddSingleton<PetManagementHandler>();
builder.Services.AddSingleton<BattleCommandHandler>();
builder.Services.AddSingleton<StatAllocationHandler>();
builder.Services.AddSingleton<TeamHandler>();
builder.Services.AddSingleton<ChatHandler>();

builder.Services.AddSingleton<PacketDispatcher>(sp =>
{
    var dispatcher = new PacketDispatcher();
    dispatcher.Register(sp.GetRequiredService<VersionCheckHandler>());
    dispatcher.Register(sp.GetRequiredService<ServerTimeHandler>());
    dispatcher.Register(sp.GetRequiredService<KeepaliveHandler>());
    dispatcher.Register(sp.GetRequiredService<LoginHandler>());
    dispatcher.Register(sp.GetRequiredService<CreateCharHandler>());
    dispatcher.Register(sp.GetRequiredService<SelectCharHandler>());
    dispatcher.Register(sp.GetRequiredService<WarpHandler>());
    dispatcher.Register(sp.GetRequiredService<ItemOperationHandler>());
    dispatcher.Register(sp.GetRequiredService<NpcTalkHandler>());
    // Move and legacy NPC talk currently share 6/1. Movement is the active
    // phase-3 protocol; register it last so it is not silently overwritten.
    dispatcher.Register(sp.GetRequiredService<MoveHandler>());
    dispatcher.Register(sp.GetRequiredService<NpcEventHandler>());
    dispatcher.Register(sp.GetRequiredService<MissionHandler>());
    dispatcher.Register(sp.GetRequiredService<CoreGameSystemsHandler>());
    dispatcher.Register(sp.GetRequiredService<PetManagementHandler>());
    dispatcher.Register(sp.GetRequiredService<BattleCommandHandler>());
    dispatcher.Register(sp.GetRequiredService<StatAllocationHandler>());
    dispatcher.Register(sp.GetRequiredService<TeamHandler>());
    dispatcher.Register(sp.GetRequiredService<ChatHandler>());
    return dispatcher;
});

builder.Services.AddSingleton<PipelinesTcpServer>();

var host = builder.Build();

// Start TCP Game Server
var server = host.Services.GetRequiredService<PipelinesTcpServer>();
string bindHost = builder.Configuration["Server:Host"] ?? "0.0.0.0";
int bindPort = int.TryParse(builder.Configuration["Server:Port"], out int p) ? p : 6613;
server.Start(bindHost, bindPort);

Console.WriteLine($"[Server] Ready and listening for TS Online mobile clients on port {bindPort}.");
Console.WriteLine("[Server] Press Ctrl+C to exit.");

await host.RunAsync();
