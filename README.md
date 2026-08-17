# TS Dark World

พื้นที่พัฒนา Windows Client และ .NET Server ของ TS Dark World

## Workflow

- หนึ่งงานต่อหนึ่ง branch
- หนึ่ง branch ต่อหนึ่ง Pull Request
- ห้าม commit secrets, build output, backups หรือข้อมูลเกมต้นฉบับขนาดใหญ่
- ทุก PR ต้องมีผล build/test และระบุข้อจำกัดที่ทราบ

## โครงสร้าง

- `client/` — WPF Windows Client, protocol, rendering และ integration tests
- `server/` — .NET game server, CDN server, data, persistence และ tests

## เริ่มต้นบนเครื่องพัฒนา

```powershell
dotnet build server/TsmServer.sln -c Release
dotnet build client/TsmClient.slnx -c Release
dotnet run --project server/src/TsmServer.App/TsmServer.App.csproj -c Release
```

จากนั้นเปิด Client และเชื่อม `127.0.0.1:6613` ด้วย protocol version `258`

Server ใช้ in-memory repository เป็นค่าเริ่มต้น ค่าเชื่อมต่อฐานข้อมูลจริงต้องกำหนดผ่าน local configuration หรือ environment variable และห้าม commit ลง repository
