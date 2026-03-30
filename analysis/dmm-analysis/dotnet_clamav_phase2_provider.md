# Phase 2 — Core Scanner Infrastructure

## Service: Documents.NET (port 5006)
## Date: 2026-03-29

---

## 1. Files Implemented

| File | Description |
|------|-------------|
| `Documents.Infrastructure/Scanner/ClamAvFileScannerProvider.cs` | Production TCP ClamAV scanner |
| `Documents.Infrastructure/Scanner/NullScannerProvider.cs` | Unchanged — pass-through, returns Skipped |
| `Documents.Infrastructure/Scanner/MockScannerProvider.cs` | Unchanged — config-driven mock |
| `Documents.Infrastructure/DependencyInjection.cs` | Added `clamav` case to scanner factory |
| `Documents.Api/appsettings.json` | Added `Scanner:ClamAv` section |

---

## 2. ClamAV Provider Implementation

### 2.1 Configuration (`ClamAvOptions`)

```json
"Scanner": {
  "Provider": "clamav",
  "ClamAv": {
    "Host":           "localhost",
    "Port":           3310,
    "TimeoutMs":      30000,
    "ChunkSizeBytes": 2097152
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Host` | `localhost` | clamd hostname or IP |
| `Port` | `3310` | clamd TCP port (default) |
| `TimeoutMs` | `30000` | Send/receive timeout (30 s) |
| `ChunkSizeBytes` | `2097152` | Stream chunk size (2 MB) |

### 2.2 INSTREAM Protocol

ClamAV's `zINSTREAM` command accepts a file stream over TCP:

```
Client → clamd:
  "zINSTREAM\0"               (command, null-terminated)
  [4-byte BE length][chunk]   (repeat for each chunk)
  [4 bytes of 0x00]           (end of stream)

clamd → Client:
  "stream: OK\n"              (clean)
  "stream: FOUND Eicar-Test-Signature\n"   (infected, includes threat name)
  "stream: ERROR ...\n"       (error / size limit exceeded)
```

### 2.3 Response Parsing

| Response | Mapped Status | Threats |
|----------|--------------|---------|
| Ends with `: OK` | `ScanStatus.Clean` | `[]` |
| Contains `FOUND` | `ScanStatus.Infected` | `["ThreatName"]` |
| Contains `ERROR` or unexpected | `ScanStatus.Failed` | `[]` |
| TCP exception | `ScanStatus.Failed` | `[]` |

### 2.4 Error Handling

- TCP connection failure → `Failed` (logged at Error level)
- Timeout → `Failed` (TCP timeouts fire via `ReceiveTimeout`/`SendTimeout`)
- Unexpected response → `Failed` (logged at Warning level with raw response)
- Partial response / stream closed early → `Failed`

**Fail closed:** Any error returns `Failed`, never `Clean`.

### 2.5 Resource Management

- `TcpClient` is `using`-scoped — disposed on method exit
- `NetworkStream` is `await using`-scoped
- File stream is passed in from caller and is NOT disposed by the provider
- All I/O is async (`ConnectAsync`, `WriteAsync`, `ReadLineAsync`)

---

## 3. Provider Selection

`DependencyInjection.cs` scanner factory:

```csharp
services.AddSingleton<IFileScannerProvider>(sp => scannerProvider switch
{
    "clamav" => sp.GetRequiredService<ClamAvFileScannerProvider>(),
    "mock"   => sp.GetRequiredService<MockScannerProvider>(),
    _        => sp.GetRequiredService<NullScannerProvider>(),
});
```

Set `Scanner:Provider` in configuration:
- `"none"` → NullScannerProvider (all files marked `Skipped`)
- `"mock"` → MockScannerProvider (configurable result)
- `"clamav"` → ClamAvFileScannerProvider (production TCP)

---

## 4. Keeping Existing Providers Working

### NullScannerProvider
- No changes required
- Returns `ScanStatus.Skipped` — access allowed unless `RequireCleanScanForAccess=true` with strict `Skipped` enforcement

### MockScannerProvider
- No changes required
- `Scanner:Mock:MockResult` accepts `clean`, `infected`, `failed`
- Useful for integration testing without a real clamd

---

## 5. Running ClamAV Locally (Development)

### Option A: Docker
```bash
docker run -d --name clamd \
  -p 3310:3310 \
  clamav/clamav:latest
```

### Option B: Native (Debian/Ubuntu)
```bash
sudo apt install clamav clamav-daemon
sudo freshclam
sudo systemctl start clamav-daemon
```

### Option C: Config mock for development
```json
"Scanner": {
  "Provider": "mock",
  "Mock": { "MockResult": "clean" }
}
```

---

## 6. Verification Without clamd

Set `Scanner:Provider=mock` and `Scanner:Mock:MockResult=infected` to verify:
- Upload returns `202 Accepted` / `201 Created` (always — async scan)
- Background worker processes the job
- Scan status updates to `Infected`
- Access endpoint returns `403 Forbidden`

---

## 7. Security Notes

- ClamAV connection is plaintext TCP — suitable for localhost or private network only
- For cloud deployments: run clamd as a sidecar in the same pod/task, or use a VPC-internal endpoint
- Do not expose clamd port (3310) to the public internet
- Chunk size (2 MB) limits peak memory per scan operation regardless of file size
- Timeout (30 s) prevents resource exhaustion from hung clamd connections
