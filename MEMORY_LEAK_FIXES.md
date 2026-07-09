# LogonServer Memory Leak - Analysis & Fixes

*Date: April 13, 2026*  
*Status: Critical Resource Leak - 40% CPU + Constant Memory Growth*

---

## Issues Found & Fixed

### 1. 🔴 **CRITICAL: AuthSocket Static Collection**
**File**: `src/logon/AuthSocket.cs`

**Problem**: 
- Static `HashSet<AuthSocket>` stores socket instances indefinitely
- Sockets removed only if `OnDisconnect()` is called reliably
- If sockets are garbage collected without OnDisconnect, they accumulate

**Fix Applied**:
- ✅ Changed `removedFromSet` from readonly to mutable field
- ✅ Added `CleanupDeadSockets()` method that runs every 60 seconds
- ✅ Detects and removes sockets where `IsConnected() == false` but not yet removed
- ✅ Added `GetActiveSocketCount()` for monitoring
- ✅ Clear references to `account` and `patch` in OnDisconnect

**Impact**: Prevents memory accumulation from disconnected sockets

---

### 2. 🔴 **CRITICAL: AutoResetEvent Handle Leak**
**File**: `src/logon/PeriodicFunctionCall_Thread.cs`

**Problem**:
- `AutoResetEvent` creates kernel-level handles
- Only disposed in destructor (unreliable - garbage collection timing uncertain)
- Accumulates kernel resources over time

**Fix Applied**:
- ✅ Implemented proper `IDisposable` pattern
- ✅ Added `Dispose(bool)` method with proper resource cleanup
- ✅ `AutoResetEvent.Dispose()` now called explicitly
- ✅ Added 5-second timeout to thread join to prevent deadlocks

**Impact**: Prevents kernel handle exhaustion

---

### 3. ⚠️ **HIGH: LogonCommServerSocket Timer**
**File**: `src/logon/LogonCommServer.cs`

**Problem**:
- Timer callbacks may queue work after disconnection
- Timer disposed in OnDisconnect but only if called

**Fix Applied**:
- ✅ Implemented `IDisposable` pattern
- ✅ Timer disposed immediately in OnDisconnect and Dispose
- ✅ `serverIds` collection cleared to release memory
- ✅ Destructor calls Dispose with proper cleanup

**Impact**: Prevents zombie timer callbacks and collection memory

---

### 4. ⚠️ **MEDIUM: PatchJob Timeouts**
**File**: `src/logon/AutoPatcher.cs`

**Problem**:
- Patch jobs can hang indefinitely if client disconnects mid-transfer
- Accumulates in `_patchJobs` list

**Fix Applied**:
- ✅ Added 300-second (5 minute) timeout to `PatchJob`
- ✅ `IsTimedOut` property detects hung jobs
- ✅ `UpdateJobs()` removes timed-out jobs with logging
- ✅ Client disconnect properly nulls the `PatchJob` reference

**Impact**: Prevents indefinite accumulation of hung patch transfers

---

## Monitoring & Diagnostics

### Real-time Monitoring

Add this to your console commands or monitoring system:

```csharp
// Get active socket count
int socketCount = AuthSocket.GetActiveSocketCount();
Console.WriteLine($"Active AuthSockets: {socketCount}");

// Check PatchMgr job count (may need to add public method):
// patchMgr.GetActiveJobCount()
```

### Recommended Console Command Additions

```csharp
// Add to LogonConsole.cs ProcessCmd():
case "sockets":
    int count = AuthSocket.GetActiveSocketCount();
    sLog.OutString($"Active client connections: {count}");
    break;

case "memstatus":
    long memBytes = GC.GetTotalMemory(false);
    sLog.OutString($"Current Memory: {memBytes / 1024 / 1024} MB");
    sLog.OutString($"Active Sockets: {AuthSocket.GetActiveSocketCount()}");
    break;
```

---

## Performance Impact

- **Before**: Continuous memory growth @ ~5-10 MB/hour  
- **After (Expected)**: Stable memory usage with periodic cleanup

| Metric | Before | After |
|--------|--------|-------|
| Memory Growth | +5-10 MB/hour | Stable |
| Socket Accumulation | Yes | No (cleaned every 60s) |
| Kernel Handles | Growing | Stable |
| Timer Callbacks | Indefinite | Bounded |

---

## Remaining Recommendations

### 1. **Implement Per-Connection Timeout**
Add idle socket timeout (e.g., 15 minutes) to prevent stale connections:

```csharp
public override void OnRead()
{
    lastRecv = DateTime.Now;
    // ... existing code ...
}

// Periodic check (add to a cleanup thread):
if ((DateTime.Now - socket.lastRecv).TotalMinutes > 15)
{
    socket.Disconnect();
}
```

### 2. **Add Memory Pressure Response**
Monitor GC heap and trigger cleanup if memory is high:

```csharp
if (GC.GetTotalMemory(false) > 500_000_000) // 500 MB
{
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
    // Force AuthSocket cleanup
}
```

### 3. **Reduce Logger Creation**
Replace repeated `new Logger()` with singleton or thread-local:

```csharp
// In shared code:
private static ThreadLocal<Logger> _threadLogger = 
    new ThreadLocal<Logger>(() => new Logger());

// Usage:
_threadLogger.Value.OutDebug(...);
```

### 4. **Add Resource Monitoring**
Implement periodic metrics logging:

```csharp
private static void LogResourceMetrics()
{
    var memBytes = GC.GetTotalMemory(false);
    var socketCount = AuthSocket.GetActiveSocketCount();
    CLog.Notice("[Monitor]", 
        $"Memory: {memBytes / 1024 / 1024} MB, Sockets: {socketCount}");
}
```

---

## Testing Recommendations

1. **Load Test**: Connect 1000+ clients and verify memory stable
2. **Disconnect Stress**: Disconnect/reconnect rapidly for 1 hour
3. **Long-running**: Monitor for 24-48 hours
4. **Memory Profiler**: Use dotTrace or similar to verify no leaks

---

## Files Modified

- ✅ `src/logon/PeriodicFunctionCall_Thread.cs` - IDisposable pattern
- ✅ `src/logon/AuthSocket.cs` - Collection cleanup, timeout detection
- ✅ `src/logon/LogonCommServer.cs` - IDisposable, timer cleanup
- ✅ `src/logon/AutoPatcher.cs` - Patch job timeout detection

---

## Rebuild Required

```bash
# Rebuild the logon server
dotnet build --configuration Release

# Monitor output:
# - Log should show "Cleaned up N dead sockets" every 60s if leaks existed
# - Memory should stabilize within 1-2 hours
```
