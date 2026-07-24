# ChatBridgeService

ASP.NET Core 8 service yang menjembatani WhatsApp (Meta Cloud API) dengan Creatio CRM.
Dirancang sebagai SaaS — satu service bisa handle banyak Creatio instance (multi-tenant).

## Architecture

```
WhatsApp User
    ↕ (Meta Cloud API)
ChatBridgeService (localhost:5051)
    ↕ (HTTP + cookie auth)
Creatio CRM (localhost:8080)
```

### Multi-tenant routing
Setiap Creatio instance punya `ApiKey` unik yang digunakan sebagai path segment:
- `POST /webhook/{apiKey}` — terima pesan masuk dari Meta
- `GET  /webhook/{apiKey}` — verifikasi webhook Meta
- `POST /send/{apiKey}/text|buttons|list` — kirim pesan WA (dipanggil dari Creatio)
- `GET  /chat/{apiKey}/{conversationId}?phone=...` — chat UI di-embed sebagai iframe di Creatio
- `GET  /api/{apiKey}/messages/{conversationId}` — proxy get messages ke Creatio
- `POST /api/{apiKey}/reply` — proxy agent reply ke Creatio

### Admin panel
- `GET /admin` — dashboard (stats hari ini per instance)
- `GET /admin/instances` — CRUD instances
- `GET /admin/logs` — log semua events, bisa filter per instance

Login pakai username + password (dari `appsettings.json`), session disimpan in-memory 8 jam.

## Project Structure

```
Controllers/
  AdminController.cs   — admin UI (server-rendered HTML, sidebar layout)
  ChatController.cs    — chat iframe + proxy ke Creatio
  WebhookController.cs — terima webhook dari Meta
  SendController.cs    — kirim pesan ke Meta (dipanggil dari Creatio)

Services/
  CreatioForwarder.cs  — forward pesan ke Creatio, handle auth cookie
  CreatioAuthCache.cs  — singleton, cache auth cookie Creatio per instance (15 menit)
  MetaMessageSender.cs — kirim pesan ke Meta Cloud API
  InstanceService.cs   — CRUD CreatioInstance
  LogService.cs        — tulis MessageLog ke DB
  AdminSession.cs      — singleton, in-memory session tokens untuk admin
  MetaWebhookParser.cs — parse payload dari Meta webhook

Data/
  AppDbContext.cs      — EF Core, PostgreSQL

Models/
  CreatioInstance.cs   — entity: config per Creatio instance (credentials + API key)
  MessageLog.cs        — entity: log events (webhook_in, agent_reply, error_creatio, error_meta)
  MetaWebhookPayload.cs, IncomingMessage.cs, SendMessageRequest.cs
```

## Database

PostgreSQL. Connection string di `appsettings.json`:
```json
"ConnectionStrings": {
  "Default": "Host=...;Port=5433;Database=chatbridge;Username=...;Password=..."
}
```

Migration otomatis berjalan saat startup (`db.Database.Migrate()`).

### Tables
- `CreatioInstances` — config per tenant
- `MessageLogs` — event log (indexed by `InstanceId` dan `CreatedAt`)

## Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5433;Database=chatbridge;Username=...;Password=..."
  },
  "Admin": {
    "Username": "admin",
    "Password": "..."
  }
}
```

Credentials Meta dan Creatio disimpan **per instance di database**, bukan di appsettings.

## Key Design Decisions

### Fire-and-forget scope
`WebhookController.Receive` menggunakan `Task.Run` untuk forward pesan ke Creatio tanpa memblokir response ke Meta (Meta expects 200 dalam 20 detik). Scope DI baru dibuat via `IServiceScopeFactory` agar `AppDbContext` tidak di-dispose sebelum task selesai.

### Auth Creatio
Cookie-based auth. `CreatioAuthCache` (singleton) menyimpan cookie per instance ID dengan TTL 15 menit. Jika 401, cache di-invalidate dan re-auth otomatis.

### Admin UI
Server-rendered HTML dari controller (tidak ada frontend framework). CSS/JS inline. Raw string literals `$$"""..."""` dipakai untuk template HTML yang mengandung CSS `{}` agar tidak conflict dengan interpolasi C#.

### Logging events
| Type | Trigger |
|------|---------|
| `webhook_in` | Pesan WA masuk berhasil di-forward ke Creatio |
| `agent_reply` | Agent reply dari chat UI berhasil dikirim ke Creatio |
| `error_creatio` | Gagal forward/reply ke Creatio |
| `error_meta` | Gagal kirim ke Meta API |

### Reply flow
`POST /api/{apiKey}/reply` hanya proxy ke `ChatBridgeAgentService/Reply` di Creatio.
Untuk kirim ke WhatsApp, Creatio harus call balik ke `POST /send/{apiKey}/text`.

## Running Locally

```bash
dotnet run --launch-profile http
# atau dengan hot-reload:
dotnet watch --launch-profile http
```

Service jalan di `http://localhost:5051`.
Admin panel: `http://localhost:5051/admin`

## EF Core Migrations

```bash
export PATH="$PATH:/Users/ahmadbagus99/.dotnet/tools"
dotnet ef migrations add <MigrationName>
```

Migrations yang sudah ada: `InitialCreate`, `AddMessageLogs`, `IncreasePhoneNumberLength`.

## CSP (Content Security Policy)

Service menambahkan header `Content-Security-Policy: frame-ancestors *` agar chat iframe bisa di-embed di Creatio. Creatio juga perlu whitelist `http://localhost:5051` di **System Designer → Security → Content Security Policy → Trusted Sources** (directives: `connect-src` dan `frame-src`).

---

## Architectural Context & Future Direction

### Arsitektur saat ini (v1 — transitional)

ChatBridgeService menyimpan credentials Creatio (username/password) di DB dan melakukan cookie-based auth ke Creatio. Ini adalah trade-off kesederhanaan untuk iterasi awal.

**Masalah yang diketahui:**
- Creatio username/password disimpan plaintext di DB → security risk
- Cookie auth rapuh (expire, BPMCSRF token)
- Setup di dua tempat (ChatBridgeService admin + Creatio)
- ChatBridgeService terlalu banyak tanggung jawab (bridge + chat UI + proxy)

### Target arsitektur (v2 — Opsi B)

Mengacu pada model Beesender: **Chat UI native di Creatio, ChatBridgeService hanya pure bridge.**

```
WA masuk → ChatBridgeService → POST callbackUrl (X-Api-Key) → Creatio
Agent reply → Creatio → POST /send/{apiKey}/text → ChatBridgeService → Meta
```

**Yang hilang dari ChatBridgeService di v2:**
- `/chat/{apiKey}/...` — chat page (pindah ke Creatio native UI)
- `/api/{apiKey}/messages/...` — GetMessages (Creatio query sendiri)
- `/api/{apiKey}/reply` — proxy reply (Creatio langsung call /send)
- Creatio credentials — tidak perlu disimpan sama sekali
- Cookie auth / CreatioAuthCache / CreatioForwarder

**Yang tersisa di ChatBridgeService v2:**
- `POST /webhook/{apiKey}` — terima dari Meta → push ke Creatio via API key
- `POST /send/{apiKey}/text|buttons|list` — terima dari Creatio → kirim ke Meta
- Admin panel (manage instances)
- Logging

### Config ownership (best practice)

| Setting | Disimpan di |
|---------|------------|
| MetaAccessToken, PhoneNumberId, VerifyToken | ChatBridgeService (yang call Meta) |
| Creatio BaseUrl, credentials | ChatBridgeService saat ini → hilang di v2 |
| ChatBridgeSenderUrl, ApiKey | Creatio (Creatio yang call /send) |
| ProcessName, BotName, AI settings | Creatio (internal logic Creatio) |

**Rule:** Config hidup di tempat yang **menggunakannya**.

### Security improvements yang perlu dilakukan

1. **Sekarang:** Ganti akun `Supervisor` → dedicated service account dengan minimal permissions
2. **Berikutnya:** Encrypt Creatio password di DB (AES dengan master key dari env var)
3. **Target:** Flip ke API key auth — Creatio implement `X-ChatBridge-Key` validation di service-nya, ChatBridgeService tidak perlu simpan credentials Creatio sama sekali

### Meta message status flow

```
Agent send → ChatBridgeService → Meta API
                                    ↓ metaMessageId
              ChatBridgeService → Creatio /SetMetaMessageId
                                    ↓
Meta kirim status update (sent/delivered/read/failed)
                                    ↓
ChatBridgeService → Creatio /UpdateStatus (match by metaMessageId)
```

Status yang mungkin dari Meta: `sent` → `delivered` → `read` / `failed` (error code 131047 = 24h window expired).

### Migrations

`InitialCreate`, `AddMessageLogs`, `IncreasePhoneNumberLength`
