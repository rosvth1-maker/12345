# แนวทางการทำงาน

1. อัปเดต `main` ก่อนเริ่มงาน
2. สร้าง branch ชื่อ `task/<ชื่องาน>` หรือ `fix/<ชื่อปัญหา>`
3. หนึ่ง branch ต้องแก้เพียงหนึ่งงานที่ตรวจสอบได้
4. Build และรัน test ที่เกี่ยวข้องก่อน push
5. เปิด Pull Request พร้อมสรุป ผลทดสอบ ความเสี่ยง และ rollback
6. ห้าม push รหัสผ่าน token connection string หรือข้อมูลบัญชี
7. ห้าม commit `bin`, `obj`, backups, Portable ZIP หรือ asset ต้นฉบับ

ตัวอย่าง:

```text
main
└── task/phase-8a-unified-client
    └── Pull Request: Phase 8A — Unified versioned client build
```
