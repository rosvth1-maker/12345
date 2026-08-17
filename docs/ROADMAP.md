# แผนพัฒนาต่อ TS Dark World Windows Client และ Server

วันที่จัดทำ: 17 สิงหาคม 2569

ฐานอ้างอิง: ผลทดสอบ Client `TS-Dark-World-Windows-Client-Test-win-x64` กับ Server `TS Dark World online Server new 2026`
สถานะปัจจุบัน: ระบบเครือข่ายและ gameplay หลักผ่าน automated tests 74/74 แต่ Client ในโฟลเดอร์ทดสอบยังเป็น build เก่าและฉากจริงจาก Unity AssetBundle ยังไม่ถูกนำเข้า renderer

---

## 1. เป้าหมายของแผน

1. ทำให้มี Client รุ่นทดสอบเพียงรุ่นเดียวและตรวจสอบเลข build ได้
2. แสดง Map, NPC, จุดวาร์ป และข้อมูลภารกิจจาก Server จริง
3. เพิ่มภาพฉากจากข้อมูลเกมเดิมโดยไม่แก้ไฟล์ต้นฉบับ
4. ทำให้ข้อมูลผู้เล่นคงอยู่หลังปิดและเปิด Server ใหม่
5. เตรียมระบบอัปเดต Installer ความปลอดภัย และการทดสอบโหลด
6. สร้าง Release Candidate ที่ผู้เล่นภายนอกสามารถติดตั้งและทดสอบได้

## 2. หลักการบังคับใช้ทุกระยะ

- สำรองไฟล์ก่อนแก้จุดเสี่ยงทุกครั้ง
- ห้ามเขียนทับข้อมูลเกมไทยต้นฉบับ
- ห้ามบันทึกบัญชี รหัสผ่าน token หรือ connection string ลง log/crash report
- การคำนวณเงิน ไอเทม EXP และสิทธิ์ต้องตัดสินที่ Server
- Build ต้องผ่านโดยไม่มี compile error
- ต้องมี test สำหรับ packet และระบบใหม่
- ต้องรัน regression ของระบบเดิมก่อนส่งมอบ
- ทุก ZIP/Installer ต้องมี SHA-256
- รายงานต้องแยกสิ่งที่ทดสอบจริงออกจากข้อสันนิษฐาน
- Code Signing ต้องใช้ certificate จริงเท่านั้น

---

# ระยะที่ 8A — รวม Client รุ่นทดสอบให้เป็นรุ่นเดียว

## เป้าหมาย

แก้ปัญหา Client หลายชุดมีชื่อคล้ายกันแต่ build คนละเวลา และทำให้ผู้ทดสอบทราบแน่นอนว่ากำลังเปิดรุ่นใด

## งานพัฒนา

1. สำรองโฟลเดอร์ `TS-Dark-World-Windows-Client-Test-win-x64`
2. กำหนดเวอร์ชัน เช่น `0.8.0-test.1`
3. ฝังข้อมูลต่อไปนี้ใน assembly และหน้าจอ:
   - Product version
   - Build number
   - Commit/revision ถ้ามี Git
   - วันเวลา build แบบ UTC
   - Protocol version 258
4. Publish source ล่าสุดแบบ `win-x64 self-contained`
5. แสดงเลขรุ่นบน Title bar และหน้า Login
6. ตั้งค่า Local profile:
   - Game Host: `127.0.0.1`
   - Game Port: `6613`
   - Client Version: `258`
   - CDN: `http://127.0.0.1:8443`
   - Server directory: `C:\Users\Administrator\Documents\Codex\TS Dark World online Server new 2026`
7. ให้ปุ่มซิงก์ Server อ่านเฉพาะ Host/Port/Version/CDN/Data path
8. ห้าม Client อ่านหรือแสดงส่วน Database credentials
9. ย้าย build เก่าไปโฟลเดอร์ archive โดยไม่ลบทันที
10. สร้าง `build-info.json` และ `checksums.sha256`

## การทดสอบ

- เปิด EXE จากโฟลเดอร์ส่งมอบจริง
- ตรวจชื่อหน้าต่างและเลข build
- ตรวจ config ที่ถูก copy ไปกับ publish
- เชื่อม Handshake/Login กับ Server
- ตรวจว่าปิด Client แล้วไม่มี process ค้าง
- ตรวจ crash report และ log ไม่มีข้อมูลบัญชี

## เกณฑ์ผ่าน

- มี EXE รุ่นใช้งานเพียงชุดเดียวในโฟลเดอร์ทดสอบ
- ผู้ทดสอบบอกเลข build จากหน้าจอได้
- Handshake/Login ผ่าน
- ไม่มีการใช้ config จากโฟลเดอร์ build เก่าโดยไม่ตั้งใจ

## ไฟล์ส่งมอบ

- Portable ZIP
- `build-info.json`
- `checksums.sha256`
- รายงานผลระยะ 8A

---

# ระยะที่ 8B — Map และ NPC จาก Server พร้อมเล่น

## เป้าหมาย

ให้ Client รับและแสดงข้อมูลแผนที่/NPC จาก Server จริง ไม่พึ่งข้อมูลตัวอย่างที่ฝังไว้ใน Client

## งานฝั่ง Server

1. ขยาย Scene packet ให้มี:
   - Map ID และชื่อแผนที่
   - ขนาดหรือขอบเขตแผนที่
   - รายชื่อผู้เล่น
   - NPC ID และชื่อ
   - ตำแหน่ง X/Y
   - ประเภท NPC
   - Quest/Event ID
   - Shop ID หรือ Battle group ถ้ามี
2. โหลด NPC placement จาก Eve/Server data
3. สร้าง fallback placement ที่ Server เท่านั้นเมื่อข้อมูลฉากไม่ครบ
4. ส่ง NPC ใหม่ทุกครั้งที่เข้าเกมหรือวาร์ป
5. ตรวจระยะก่อนสนทนา ซื้อสินค้า หรือเริ่มต่อสู้
6. ป้องกัน Client ส่ง NPC ID ที่ไม่มีในแผนที่

## งานฝั่ง Client

1. Parse NPC list จาก Scene packet
2. ล้าง NPC ของแผนที่เดิมเมื่อวาร์ป
3. วาด NPC พร้อมชื่อภาษาไทยและ ID สำหรับ debug
4. แยกสัญลักษณ์:
   - ผู้ให้ภารกิจ
   - ผู้รับภารกิจ
   - ร้านค้า
   - มอนสเตอร์
   - จุดวาร์ป
5. คลิก NPC บนแผนที่เพื่อเลือกและสนทนา
6. แสดงระยะห่างจากตัวละคร
7. เพิ่ม minimap และตำแหน่งตัวละคร
8. แสดงข้อความที่เข้าใจง่ายเมื่อ Scene ไม่มี NPC

## การทดสอบ

- Map 10801 มี NPC อย่างน้อย 3 ตัว
- ชื่อและตำแหน่งตรงกับ Server
- สนทนา NPC 10001–10003 ได้
- รับ/ส่งภารกิจได้
- วาร์ป 10801 → 10802 แล้ว NPC เดิมหาย
- กลับ 10802 → 10801 แล้ว NPC โหลดใหม่
- ส่ง NPC ID ปลอมแล้ว Server ปฏิเสธอย่างปลอดภัย
- ผู้เล่นสองคนเห็นการเคลื่อนที่กัน

## เกณฑ์ผ่าน

- Client ไม่มีข้อมูล NPC gameplay ที่ hard-code เป็นแหล่งหลัก
- NPC และแผนที่เปลี่ยนตาม Scene ของ Server
- ไม่มี NPC ค้างข้ามแผนที่
- Packet malformed ไม่ทำให้ Client/Server crash

---

# ระยะที่ 8C — นำเข้าภาพแผนที่จริงแบบ Read-only

## เป้าหมาย

นำภาพหรือองค์ประกอบฉากจากไฟล์เกมไทยเดิมมาแสดง โดยรักษาไฟล์ต้นฉบับทุกไบต์

## ขั้นสำรวจ

1. สร้าง SHA-256 catalog ของไฟล์ `.unity3d`, `.jmg`, `.pmg`, `.jmxa`, `.sty`
2. ระบุ Unity AssetBundle version และ compression
3. ตรวจว่าไฟล์มี texture, sprite, tilemap, mesh หรือ scene object ใดบ้าง
4. เลือก Map 10801 เป็น proof of concept
5. สร้างโฟลเดอร์ cache แยกจาก source

## ขั้นพัฒนา

1. สร้าง Asset Importer แบบ read-only
2. ถอดข้อมูลเข้า cache โดยใช้ชื่อที่ไม่ชนกัน
3. บันทึก mapping ระหว่าง Map ID กับ asset
4. ตรวจ SHA-256 ก่อนใช้ cache
5. เพิ่ม renderer เป็นลำดับ:
   - ภาพพื้นหลัง
   - Tile layers
   - วัตถุฉาก
   - จุดบังตัวละคร
   - animation ที่จำเป็น
6. หากไฟล์ไม่รองรับ ให้ fallback เป็นแผนที่ 2D โดยไม่บล็อก gameplay
7. ห้ามโหลดไฟล์หลาย GB เข้าหน่วยความจำพร้อมกัน

## การทดสอบประสิทธิภาพ

- วัดเวลาโหลดฉากครั้งแรกและจาก cache
- วัด Working Set ก่อนและหลังเปลี่ยนฉาก 20 รอบ
- ตรวจ memory growth
- วัดเวลา render เฉลี่ยและเฟรมที่ช้า
- ทดสอบความละเอียด 1280×720, 1920×1080 และ fullscreen

## เกณฑ์ผ่าน

- แสดงภาพจริงของ Map 10801 อย่างน้อยหนึ่งชั้น
- SHA-256 ของต้นฉบับไม่เปลี่ยน
- เปลี่ยนฉาก 20 รอบโดยไม่มี memory growth ผิดปกติ
- หาก importer ล้มเหลว Client ยังเปิดแผนที่ fallback ได้

---

# ระยะที่ 8D — UI การควบคุม และประสบการณ์ผู้เล่น

## งานพัฒนา

1. รองรับ WASD และ Arrow keys
2. ให้ผู้เล่นเปลี่ยนคีย์ลัดและตรวจ key conflict
3. เพิ่มซูมกล้องและติดตามตัวละคร
4. คลิกพื้นเพื่อเดินและคลิก NPC เพื่อโต้ตอบ
5. เพิ่มเครื่องหมาย `!`, `?`, ร้านค้า และต่อสู้เหนือ NPC
6. แสดง HP/SP/EXP/Gold แบบ real-time
7. แสดง ping และสถานะ Server
8. เพิ่ม reconnect เมื่อเครือข่ายหลุด
9. ปรับ UI scaling ตาม DPI
10. รองรับ windowed, maximized และ fullscreen
11. บันทึกระดับเสียงและคุณภาพภาพ
12. ปิดปุ่มที่ยังใช้ไม่ได้พร้อม tooltip อธิบาย
13. เพิ่มหน้ารายงาน bug ที่ไม่แนบข้อมูลบัญชี

## การทดสอบ

- Keyboard, mouse และ DPI
- ปรับขนาดหน้าต่างขั้นต่ำ/สูงสุด
- สลับ fullscreen 20 ครั้ง
- reconnect หลังตัดเครือข่าย
- ตรวจ Tab order และข้อความภาษาไทย
- UI soak test อย่างน้อย 30 นาที

## เกณฑ์ผ่าน

- ไม่มี control ถูกตัดที่ความละเอียดขั้นต่ำ
- การตั้งค่าคงอยู่หลังเปิด Client ใหม่
- reconnect ไม่สร้าง session ซ้ำ
- log และ crash report ไม่มีข้อมูลสำคัญ

---

# ระยะที่ 8E — Updater, CDN, Installer และ Portable Package

## ระบบอัปเดต

1. CDN สร้าง `manifest.json` ประกอบด้วย:
   - Version
   - Published UTC
   - Relative path
   - File length
   - SHA-256
   - Download URL
2. Client ตรวจ manifest ผ่าน HTTP สำหรับ Local/LAN และ HTTPS สำหรับ VPS
3. Validate path ป้องกัน `..`, absolute path และ path traversal
4. ดาวน์โหลดเข้า staging
5. ตรวจ length และ SHA-256 ก่อนติดตั้ง
6. ปิด Client ก่อนแทนที่ EXE
7. สำรองรุ่นก่อนหน้าและรองรับ rollback
8. ล้าง staging ที่ไม่สมบูรณ์

## การบรรจุและติดตั้ง

1. Portable ZIP แบบ self-contained win-x64
2. Installer เลือกตำแหน่งติดตั้งได้
3. Shortcut Start Menu/Desktop เป็นตัวเลือก
4. Uninstaller ไม่ลบ save/config โดยไม่ถาม
5. ไม่บรรจุ PDB ในชุดผู้เล่นทั่วไป
6. แยก symbol package สำหรับทีมพัฒนา
7. ตรวจ Code Signing Certificate
8. เซ็น EXE/Installer เมื่อมี certificate จริง

## การทดสอบ

- ติดตั้งบน Windows user ใหม่
- อัปเดตจาก N-1 → N
- SHA-256 ผิดต้องหยุด
- ไฟล์ดาวน์โหลดไม่ครบต้อง retry ได้
- rollback หลังติดตั้งล้มเหลว
- uninstall/reinstall

## เกณฑ์ผ่าน

- ไม่เขียนไฟล์นอกโฟลเดอร์ที่อนุญาต
- อัปเดตไม่ทำให้ Client ใช้งานไม่ได้
- Portable และ Installer ให้ผลการทำงานเหมือนกัน

---

# ระยะที่ 9 — Persistence และฐานข้อมูล MySQL

## เป้าหมาย

ทำให้ข้อมูล gameplay คงอยู่หลัง restart Server และป้องกันเงิน/ไอเทมซ้ำ

## งานฐานข้อมูล

1. ออกแบบตาราง:
   - Accounts และ Characters
   - Inventory และ Equipment
   - Skills
   - Pets และ Teams
   - Bank
   - Friends และ Blocks
   - Mail และ Attachments
   - Trade history
   - Guilds และ Members
   - Quests และ Progress
2. เพิ่ม schema version และ migration
3. ย้าย connection string ไป environment variable หรือ secret store
4. ห้ามมีรหัสผ่านฐานข้อมูลใน source หรือชุด Client
5. ใช้ transaction สำหรับร้านค้า ธนาคาร จดหมายแนบของ และการค้า
6. เพิ่ม unique constraints และ idempotency key
7. สร้าง automatic backup และ restore test
8. เพิ่ม retention policy สำหรับ log/history

## Migration จาก in-memory

1. ระบุ state ที่ยังอยู่ใน singleton dictionaries
2. สร้าง repository interface และ MySQL implementation
3. เขียน migration tool สำหรับข้อมูลทดสอบที่ต้องเก็บ
4. เปรียบเทียบยอดเงิน ไอเทม และสกิลก่อน/หลัง migration
5. ปิด in-memory fallback ใน production profile

## การทดสอบ

- ปิด/เปิด Server แล้วข้อมูลยังอยู่
- concurrent purchase/trade
- transaction rollback
- duplicate packet/retry
- backup และ restore ไปฐานข้อมูลใหม่
- connection หลุดระหว่าง transaction

## เกณฑ์ผ่าน

- ข้อมูลสำคัญคงอยู่หลัง restart 100%
- ไม่พบ item/gold duplication ใน concurrency tests
- restore backup แล้วจำนวน record และยอดเงินตรงกัน

---

# ระยะที่ 10 — Security, Observability และ Load Testing

## ความปลอดภัย

1. Rate limit Login, Chat, Movement, Shop และ Trade
2. Server ตรวจ session state และ ownership ทุกคำสั่ง
3. จำกัด packet size และ string length
4. ป้องกัน malformed packet, replay และ command spam
5. Hash รหัสผ่านผู้เล่นด้วย algorithm ที่เหมาะสม
6. TLS สำหรับ CDN/VPS
7. Firewall เปิดเฉพาะพอร์ตจำเป็น
8. ลบ secrets จาก config/history และหมุน credential ที่เคยเปิดเผย
9. ตรวจ dependency vulnerability ในสภาพแวดล้อมที่เข้าถึง NuGet ได้

## Observability

1. Structured logs พร้อม correlation/session ID ที่ไม่ระบุตัวตน
2. Health endpoints สำหรับ Game Server, CDN และ Database
3. Metrics: connections, login failures, packet rate, latency, CPU, memory, GC
4. Log rotation และ retention
5. Alert เมื่อ crash, DB unavailable หรือ latency สูง

## Load Test

1. 50 connections — smoke load
2. 100 connections — expected test load
3. 500 connections — stress
4. Login burst
5. Movement/Chat burst
6. Battle และ shop concurrency
7. Soak test 4 ชั่วโมง

## เกณฑ์ผ่าน

- Server ไม่ crash และไม่มี memory leak
- latency อยู่ในเป้าหมายที่กำหนด
- การป้องกัน spam ทำงานโดยไม่เตะผู้เล่นปกติ
- ไม่มีข้อมูลบัญชีหรือ secret ใน log

---

# ระยะที่ 11 — Release Candidate และ User Acceptance Test

## การทดสอบก่อน RC

1. เปิด Installer/Portable บน Windows เครื่องสะอาด
2. ทดสอบ Local, LAN และ VPS profiles
3. ทดสอบสร้างบัญชี/ตัวละคร
4. เล่นตั้งแต่เข้าเกมจนจบภารกิจเริ่มต้น
5. ทดสอบ Map/NPC/Warp/Battle/Inventory/Shop/Bank/Social
6. ทดสอบ reconnect และ Server restart
7. เล่นต่อเนื่อง 2–4 ชั่วโมง
8. ตรวจ updater และ rollback
9. ทดสอบภาษาไทยทุกหน้าจอ
10. ตรวจ SmartScreen/Code Signing

## เอกสารส่งมอบ

- คู่มือผู้เล่น
- คู่มือติดตั้ง Client
- คู่มือผู้ดูแล Server/CDN/Database
- คู่มือ Backup/Restore/Rollback
- Known Issues
- Release notes
- Protocol compatibility matrix
- Test report และรายการ test cases
- SHA-256 ของทุก package

## เกณฑ์อนุมัติ RC

- ไม่มี Critical/High bug ที่ยังเปิดอยู่
- Automated tests ผ่านทั้งหมด
- UAT scenarios ผ่านตามเกณฑ์
- restart Server แล้วข้อมูลไม่สูญหาย
- Installer/Updater/Rollback ผ่าน
- คู่มือและ checksums ครบ

---

# 12. ลำดับดำเนินงานที่แนะนำ

1. **8A** — รวม Client build ล่าสุดก่อน เพื่อหยุดความสับสนเรื่องรุ่น
2. **8B** — ทำ Map/NPC จาก Server ให้ครบทุกแผนที่ที่เปิดทดสอบ
3. **8D** — ทำ UI/controls ให้ผู้ทดสอบเล่น flow ได้จริง
4. **9** — ย้ายข้อมูลสำคัญไป MySQL ก่อนเปิดทดสอบระยะยาว
5. **8E** — ทำ updater และ package หลังรูปแบบไฟล์เริ่มนิ่ง
6. **8C** — พัฒนาภาพฉากจริงแบบแยกสายงาน ไม่ให้บล็อก gameplay
7. **10** — security/load/monitoring
8. **11** — Release Candidate และ UAT

# 13. งานที่ควรเริ่มทันที

## Sprint ถัดไป

1. สำรอง Client test folder ปัจจุบัน
2. Publish source รุ่นล่าสุดไปโฟลเดอร์ใหม่ที่มีเลข build
3. เพิ่ม build info บน Title bar
4. เพิ่ม test ยืนยัน NPC มาจาก Scene packet
5. เพิ่มข้อมูล Map 10802 และ NPC ของแผนที่นั้น
6. เพิ่มการคลิก NPC บน GameSurface
7. รัน regression Phase 1–7
8. ส่ง Portable ZIP และรายงาน 8A/8B รอบแรก

# 14. Definition of Done สำหรับทุกงาน

งานจะถือว่าเสร็จเมื่อ:

- โค้ด build ผ่าน
- มี test ที่พิสูจน์พฤติกรรมใหม่
- regression ผ่าน
- ไม่มี secret ใน source/log/report/package
- มี backup ก่อนแก้ไข
- มีเอกสารใช้งานหรือ migration note
- มี artifact ที่เปิดทดสอบได้จริง
- มี SHA-256
- ระบุ known limitations อย่างตรงไปตรงมา
