# ChatBridgeService — Technical Documentation

**Version:** 1.0  
**Platform:** ASP.NET Core 8, PostgreSQL  
**Last Updated:** May 2026

---

## Table of Contents

1. Overview
2. System Requirements
3. Deployment Guide
4. Configuration
5. Admin Panel — Setup Instances
6. API Reference
7. Creatio Integration Guide
8. Troubleshooting

---

## 1. Overview

ChatBridgeService is a middleware service that bridges WhatsApp (Meta Cloud API) with Creatio CRM. It is designed as a multi-tenant SaaS — a single deployment can handle multiple Creatio instances, each with its own WhatsApp credentials.

### Architecture

```
WhatsApp User
      ↕  (Meta Cloud API)
ChatBridgeService  (port 5051)
      ↕  (HTTP + cookie auth)
Creatio CRM  (port 8080)
```

### Message Flow

**Incoming (WhatsApp → Creatio):**
1. Customer sends a WhatsApp message
2. Meta delivers it via webhook to `POST /webhook/{apiKey}`
3. ChatBridgeService parses the payload and forwards it to `ChatBridgeWebhookService/Receive` in Creatio
4. The event is logged to the database

**Outgoing (Agent → WhatsApp):**
1. Agent types a reply in the chat iframe inside Creatio
2. The chat page calls `POST /api/{apiKey}/reply`
3. ChatBridgeService proxies the reply to `ChatBridgeAgentService/Reply` in Creatio
4. Creatio triggers `POST /send/{apiKey}/text` on ChatBridgeService
5. ChatBridgeService calls Meta Cloud API to deliver the message to WhatsApp

---

## 2. System Requirements

| Component | Requirement |
|-----------|-------------|
| Runtime | .NET 8 SDK or Runtime |
| Database | PostgreSQL 13+ |
| OS | Windows, Linux, or macOS |
| Network | Port 5051 must be accessible to Meta webhooks (public URL or tunnel) |

---

## 3. Deployment Guide

### 3.1 Clone and Build

```bash
git clone <repository-url>
cd ChatBridgeService
dotnet restore
dotnet build -c Release
```

### 3.2 Prepare the Database

Ensure PostgreSQL is running and create a database:

```sql
CREATE DATABASE chatbridge;
CREATE USER chatbridge_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE chatbridge TO chatbridge_user;
```

### 3.3 Configure appsettings.json

Edit `appsettings.json` with your values:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=chatbridge;Username=chatbridge_user;Password=your_password"
  },
  "Admin": {
    "Username": "admin",
    "Password": "your_strong_password"
  }
}
```

### 3.4 Run the Service

```bash
dotnet run --launch-profile http
```

Or for production using the published build:

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet ChatBridgeService.dll
```

The service will:
- Automatically run database migrations on startup
- Listen on `http://localhost:5051`

### 3.5 Deploy with Docker (Optional)

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5051

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChatBridgeService.dll"]
```

Run with environment variables:

```bash
docker run -d \
  -p 5051:5051 \
  -e ConnectionStrings__Default="Host=db;Port=5432;Database=chatbridge;Username=user;Password=pass" \
  -e Admin__Username="admin" \
  -e Admin__Password="your_password" \
  -e ASPNETCORE_URLS="http://+:5051" \
  chatbridge-service
```

### 3.6 Expose to the Internet (for Meta Webhooks)

Meta Cloud API requires a publicly accessible HTTPS URL for webhooks. Options:

- **Production:** Deploy behind a reverse proxy (nginx/caddy) with a domain and SSL certificate
- **Development:** Use ngrok `ngrok http 5051` to get a temporary public URL

---

## 4. Configuration

All configuration lives in `appsettings.json`. Sensitive values (Creatio credentials, Meta tokens) are stored **per instance in the database**, not in this file.

| Key | Description |
|-----|-------------|
| `ConnectionStrings:Default` | PostgreSQL connection string |
| `Admin:Username` | Admin panel login username |
| `Admin:Password` | Admin panel login password |

---

## 5. Admin Panel — Setup Instances

### 5.1 Access the Admin Panel

Open a browser and navigate to:

```
http://localhost:5051/admin
```

Login with the `Admin:Username` and `Admin:Password` configured in `appsettings.json`.

Sessions are valid for 8 hours.

### 5.2 Dashboard

The dashboard shows real-time statistics for today:

- **Total Instances** — all configured Creatio instances
- **Active Instances** — instances with status Active
- **Messages Today** — total incoming + outgoing messages
- **Errors Today** — total failed events

The table below shows per-instance breakdown.

### 5.3 Create a New Instance

1. Go to **Instances** in the sidebar
2. Click **+ Add Instance**
3. Fill in the form:

**Basic Information**

| Field | Description | Example |
|-------|-------------|---------|
| Instance Name | Friendly name for this client | `PT Maju Bersama` |
| API Key | Auto-generated unique key used in all URLs. Leave blank to auto-generate | `pGJnKXGCuUyj2IXu` |
| Status | Active or Inactive | `Active` |

**Creatio Configuration**

| Field | Description | Example |
|-------|-------------|---------|
| Creatio Base URL | URL of the Creatio installation | `http://localhost:8080` |
| Username | Creatio login username | `Supervisor` |
| Password | Creatio login password | `Supervisor1!` |

**Meta WhatsApp Configuration**

| Field | Description | Example |
|-------|-------------|---------|
| Access Token | Meta permanent or temporary access token | `EAAxxxxx...` |
| Phone Number ID | Meta WhatsApp phone number ID (not the phone number itself) | `123456789012345` |
| Verify Token | Secret token used to verify the webhook handshake | `my-secret-verify-token` |

4. Click **Create Instance**
5. Copy the **API Key** — it will be used in all URLs for this instance

### 5.4 Configure Meta Webhook

In the [Meta Developer Console](https://developers.facebook.com):

1. Go to your App → WhatsApp → Configuration
2. Set the Webhook URL to:
   ```
   https://your-domain.com/webhook/{apiKey}
   ```
3. Set the Verify Token to the same value as `Verify Token` in the instance config
4. Subscribe to the `messages` field

### 5.5 Logs

Go to **Logs** in the sidebar to monitor activity.

Filter by instance using the dropdown. Logs are paginated (50 per page), sorted newest first.

| Log Type | Description |
|----------|-------------|
| `webhook_in` | Incoming WhatsApp message successfully forwarded to Creatio |
| `agent_reply` | Agent reply from chat UI sent to Creatio |
| `error_creatio` | Failed to communicate with Creatio |
| `error_meta` | Failed to send message via Meta API |

Click **Show** on long detail entries to expand the full error message.

---

## 6. API Reference

All endpoints use the `{apiKey}` path segment to identify the Creatio instance. Replace `{apiKey}` with the API Key from the instance configuration.

Base URL: `http://localhost:5051`

### 6.1 Webhook — Receive from Meta

Used by Meta Cloud API to deliver incoming WhatsApp messages.

#### Verify Webhook

```
GET /webhook/{apiKey}
```

**Query Parameters:**

| Parameter | Description |
|-----------|-------------|
| `hub.mode` | Must be `subscribe` |
| `hub.challenge` | Random challenge string from Meta |
| `hub.verify_token` | Must match the instance's Verify Token |

**Response:** `200 OK` with the challenge integer value.

#### Receive Message

```
POST /webhook/{apiKey}
Content-Type: application/json
```

**Body:** Standard Meta webhook payload.

```json
{
  "object": "whatsapp_business_account",
  "entry": [{
    "id": "entry_id",
    "changes": [{
      "field": "messages",
      "value": {
        "messaging_product": "whatsapp",
        "metadata": {
          "display_phone_number": "6281234567890",
          "phone_number_id": "123456789"
        },
        "contacts": [{
          "profile": { "name": "Customer Name" },
          "wa_id": "6281234567890"
        }],
        "messages": [{
          "id": "wamid.xxx",
          "from": "6281234567890",
          "type": "text",
          "timestamp": "1700000000",
          "text": { "body": "Hello" }
        }]
      }
    }]
  }]
}
```

**Response:** `200 OK` (always, to prevent Meta retries)

---

### 6.2 Send Message — Outgoing to WhatsApp

Called by Creatio to send messages to WhatsApp customers. Requires the `apiKey` in the path.

#### Send Text Message

```
POST /send/{apiKey}/text
Content-Type: application/json
```

**Body:**

```json
{
  "to": "6281234567890",
  "body": "Hello, how can I help you?",
  "phoneNumberId": ""
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `to` | string | Yes | Customer WhatsApp number with country code |
| `body` | string | Yes | Text message content |
| `phoneNumberId` | string | No | Override the instance's Phone Number ID |

**Response:**

```json
{
  "success": true,
  "metaMessageId": "wamid.xxx",
  "error": null
}
```

---

#### Send Button Message

```
POST /send/{apiKey}/buttons
Content-Type: application/json
```

**Body:**

```json
{
  "to": "6281234567890",
  "bodyText": "Please choose an option:",
  "buttons": [
    { "id": "btn_1", "title": "Option 1" },
    { "id": "btn_2", "title": "Option 2" },
    { "id": "btn_3", "title": "Option 3" }
  ],
  "phoneNumberId": ""
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `to` | string | Yes | Customer WhatsApp number |
| `bodyText` | string | Yes | Message body text |
| `buttons` | array | Yes | Max 3 buttons. Each has `id` and `title` (max 20 chars) |
| `phoneNumberId` | string | No | Override Phone Number ID |

---

#### Send List Message

```
POST /send/{apiKey}/list
Content-Type: application/json
```

**Body:**

```json
{
  "to": "6281234567890",
  "bodyText": "Please select from the list:",
  "buttonLabel": "View Options",
  "rows": [
    { "id": "row_1", "title": "Item 1", "description": "Description 1" },
    { "id": "row_2", "title": "Item 2", "description": "Description 2" }
  ],
  "phoneNumberId": ""
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `to` | string | Yes | Customer WhatsApp number |
| `bodyText` | string | Yes | Message body text |
| `buttonLabel` | string | Yes | Label on the list trigger button |
| `rows` | array | Yes | Max 10 rows. Each has `id`, `title` (max 24 chars), `description` (max 72 chars) |

---

### 6.3 Chat Page (iframe)

Embedded in Creatio as an iframe to display the conversation and allow agent replies.

```
GET /chat/{apiKey}/{conversationId}?phone={phoneNumber}
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `apiKey` | path | Instance API Key |
| `conversationId` | path | Creatio conversation/case GUID |
| `phone` | query | Customer WhatsApp number (used for sending replies) |

**Response:** `200 OK` — HTML page with the chat interface.

The chat page auto-refreshes messages every 5 seconds and allows the agent to send replies directly.

---

### 6.4 Chat API (used internally by the chat page)

These endpoints are called by the chat iframe's JavaScript, not intended to be called directly by Creatio.

#### Get Messages

```
GET /api/{apiKey}/messages/{conversationId}
```

**Response:**

```json
{
  "success": true,
  "messages": [
    {
      "message": "[Customer] Hello",
      "createdOn": "2024-01-01T10:00:00"
    }
  ]
}
```

#### Agent Reply

```
POST /api/{apiKey}/reply
Content-Type: application/json
```

**Body:**

```json
{
  "phoneNumber": "6281234567890",
  "message": "Hello, how can I help?"
}
```

---

## 7. Creatio Integration Guide

### 7.1 Required Custom Services in Creatio

You need to create these Web Service schemas in Creatio (Configuration → Web Services):

#### ChatBridgeWebhookService

Endpoint: `POST /0/rest/ChatBridgeWebhookService/Receive`

Receives incoming WhatsApp messages from ChatBridgeService.

**Expected payload:**

```json
{
  "MessageId": "wamid.xxx",
  "PhoneNumberId": "123456789",
  "From": "6281234567890",
  "CustomerName": "John Doe",
  "Type": "Text",
  "TextBody": "Hello",
  "InteractiveReplyId": null,
  "InteractiveReplyTitle": null,
  "ReceivedAt": "2024-01-01T10:00:00Z"
}
```

#### ChatBridgeAgentService

**Get Messages** — `POST /0/rest/ChatBridgeAgentService/GetMessages`

Called every 5 seconds by the chat iframe.

**Request:**
```json
{ "ConversationId": "33989cce-3e37-4b9a-b26c-f5f5e48da781" }
```

**Expected response:**
```json
{
  "success": true,
  "messages": [
    { "message": "[Customer] Hello", "createdOn": "2024-01-01T10:00:00" },
    { "message": "[Agent] Hi there", "createdOn": "2024-01-01T10:01:00" }
  ]
}
```

Message format: prefix with `[Customer]`, `[Agent]`, or `[Bot]` to determine display side and color in the chat UI.

**Agent Reply** — `POST /0/rest/ChatBridgeAgentService/Reply`

Receives the agent's reply from the chat iframe. Creatio should then call `POST /send/{apiKey}/text` on ChatBridgeService to deliver it to WhatsApp.

**Request:**
```json
{
  "phoneNumber": "6281234567890",
  "message": "Hello, how can I help?"
}
```

### 7.2 Embed Chat Page in Creatio

1. Open the Case or Contact page in Creatio Page Designer
2. Add an **iFrame** element
3. Set **Content type** to `URL`
4. Set **Content to display** to:
   ```
   http://localhost:5051/chat/{apiKey}/{Id}?phone={PhoneNumber}
   ```
   Where `{Id}` and `{PhoneNumber}` are Creatio page binding variables.

5. In Creatio **System Designer → Security → Content Security Policy → Trusted Sources**, add:
   - Source URL: `http://localhost:5051`
   - Directives: `connect-src`, `frame-src`

---

## 8. Troubleshooting

### Service won't start — database connection error

Check the connection string in `appsettings.json`. Verify PostgreSQL is running and the credentials are correct.

### Webhook verification fails (403)

The `hub.verify_token` sent by Meta does not match the `Verify Token` configured for the instance. Verify the token in the admin panel matches what you entered in the Meta Developer Console.

### Chat iframe shows "Content Unavailable" in Creatio

Creatio's Content Security Policy is blocking the iframe. Add `http://localhost:5051` to Trusted Sources in Creatio with `connect-src` and `frame-src` directives.

### Messages not appearing in chat page

1. Check `/admin/logs` for `error_creatio` entries
2. Verify `ChatBridgeAgentService/GetMessages` is implemented and compiled in Creatio
3. Check the Creatio URL and credentials in the instance config

### Log entries not showing for incoming messages

Incoming messages are forwarded in a background task. Check the terminal output for errors like `Failed to forward message`. Common cause: Creatio authentication failure.

### Error: "password authentication failed"

The PostgreSQL password in the connection string is incorrect. Check the `POSTGRES_PASSWORD` environment variable in your PostgreSQL container.
