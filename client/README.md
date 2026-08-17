# TS Dark World — C# Windows Client

ตัวเกม Windows ภาษาไทยที่สร้างใหม่ด้วย C#/.NET 9 และ WPF เพื่อเชื่อมต่อกับ TS Dark World Server

## สิ่งที่ทำงานแล้ว

- Launcher และหน้าต่างหลักภาษาไทย
- ตั้งค่า Host, TCP Port และ Client Version
- เชื่อมต่อ TCP และส่ง Handshake เวอร์ชัน 258
- Frame codec ตรงกับเซิร์ฟเวอร์: `C0 91`, XOR `AD`, little-endian
- ส่ง Login packet และอ่านผล Login
- อ่านรายชื่อตัวละครเบื้องต้น
- พื้นผิวเกมต้นแบบที่คลิกเพื่อย้ายตำแหน่งได้
- สแกนและสรุปทรัพยากรจากตัวเกมไทยเดิม
- ชุดทดสอบ Protocol แบบไม่พึ่งแพ็กเกจภายนอก

## เปิดโปรแกรม

```powershell
dotnet run --project .\src\TsmClient.App\TsmClient.App.csproj
```

## ทดสอบ

```powershell
dotnet run --project .\tests\TsmClient.Tests\TsmClient.Tests.csproj
```

## ขอบเขต

รุ่นนี้เป็น Windows Client foundation ไม่ใช่ตัวเกมที่มีระบบครบทั้งหมด ขั้นต่อไปต้องยืนยันแพ็กเก็ตเลือก/สร้างตัวละคร เข้าเกม เดิน แผนที่ NPC และต่อสู้กับ Server ทีละระบบ

โฟลเดอร์ข้อมูลเกมเดิมถูกใช้อ่านอย่างเดียว ไม่มีการลบ ย้าย หรือเขียนทับ
