# 🧪 Guide de Validation des Corrections CPU

**Date**: May 11, 2026  
**Objectif**: Vérifier que les corrections des boucles busy-wait ont résolu les problèmes de CPU

---

## 📋 Plan de Test

### Phase 1: Pré-Test (Avant Redémarrage)
- [ ] Compiler le projet
- [ ] Vérifier pas d'erreurs
- [ ] Backup de la config

### Phase 2: Startup Test (5-10 minutes)
- [ ] Redémarrer le serveur de logon
- [ ] Monitorer la consommation CPU
- [ ] Observer si CPU baisse après chargement complet
- [ ] Vérifier logs pour erreurs

### Phase 3: Idle Test (30 minutes)
- [ ] Serveur complètement chargé, pas de clients
- [ ] CPU idle should be: **< 15%** (était 40-50%)
- [ ] Pas de CPU spikes
- [ ] Mémoire stable

### Phase 4: Connection Stress (60 minutes)
- [ ] Connecter 100+ clients
- [ ] Monitorer CPU avec charge
- [ ] Should scale better: **+ 2-5% per 100 clients** (était +10%)
- [ ] Vérifier pas de memory leaks

### Phase 5: Long-Running (24-48 heures)
- [ ] Laisser serveur tourner
- [ ] Monitorer CPU toutes les 4 heures
- [ ] Vérifier stabilité
- [ ] Comparer baseline avant/après

---

## 🔍 Métriques à Monitorer

### CPU Metrics
```
Métrique                    Avant       Après       Target
─────────────────────────────────────────────────────────
CPU Idle (0 clients)        40-50%      8-15%       ✅ 60-75% reduction
CPU + 100 clients           60-70%      25-35%      ✅ Meilleur scaling
CPU + 1000 clients          80-95%      50-70%      ✅ Acceptable

Startup time                ~30s        ~30s        ✅ Pas affecté
Shutdown time               ~15s        ~15s        ✅ Possible amélioration
```

### Memory Metrics
```
Métrique                    Watch For
────────────────────────────────────────
Idle Memory                 Stable (no growth)
Memory under load           Linear growth only
GC Collections              Normal frequency
Memory After GC             Reduces properly
```

### Performance Metrics
```
Métrique                    Target
────────────────────────────────────
Client login latency        < 2s (unchanged)
Server response time        < 100ms (unchanged)
Database query latency      < 50ms (improved)
```

---

## 💻 Commandes de Monitoring

### Monitorer en Temps Réel (Windows)
```powershell
# Dans une fenêtre PowerShell
while ($true) {
    $proc = Get-Process waad-logonserver -ErrorAction SilentlyContinue
    if ($proc) {
        $cpu = (Get-Counter "\Processor(_Total)\% Processor Time").CounterSamples[0].CookedValue
        $mem = [Math]::Round($proc.WorkingSet64 / 1MB)
        Write-Host "$(Get-Date): CPU: $cpu%, Memory: ${mem}MB, Handles: $($proc.Handles)"
    }
    Start-Sleep -Seconds 5
}
```

### Monitorer avec Task Manager
- Ouvrir Task Manager
- Onglet "Performance" → CPU graph
- Onglet "Processes" → waad-logonserver
- Clic droit → "Open resource monitor"

### Linux Monitoring
```bash
# En continu
watch -n 5 "ps aux | grep waad-logonserver"

# Ou avec top
top -p $(pgrep waad-logonserver)
```

---

## 📊 Cas de Test Spécifiques

### Test 1: FastMutex Contention
**Objectif**: Vérifier que les locks ne consomment plus 25-30% CPU

```csharp
// Ajouter au LogonConsole.cs pour tester:
case "lock-test":
    var sw = System.Diagnostics.Stopwatch.StartNew();
    for (int i = 0; i < 10000; i++)
    {
        lock (testLock) { /* work */ }
    }
    sw.Stop();
    CLog.Notice("[Test]", $"10k lock iterations: {sw.ElapsedMilliseconds}ms");
    break;
```

**Avant Fix**: ~500-1000ms (heavy CPU spike)  
**Après Fix**: ~100-200ms (smooth, low CPU)

### Test 2: Database Query Performance
**Objectif**: Vérifier ProcessQueries ne spinne plus

```csharp
// Monitorer database thread idle CPU
case "db-test":
    var dbCpu = GetDatabaseThreadCPU();  // Pseudo-code
    CLog.Notice("[Test]", $"Database thread CPU: {dbCpu}%");
    break;
```

**Avant Fix**: 15-25% CPU même sans queries  
**Après Fix**: < 2% CPU when idle

### Test 3: Connection Pool
**Objectif**: Vérifier GetFreeConnection ne fait plus polling

```csharp
case "conn-test":
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var conn = DatabaseManager.GetFreeConnection();
    sw.Stop();
    CLog.Notice("[Test]", $"GetFreeConnection: {sw.ElapsedMilliseconds}ms");
    break;
```

**Avant Fix**: ~50-500ms (polling high contention)  
**Après Fix**: < 10ms (backoff efficient)

---

## ✅ Success Criteria

### Minimal Success
- [ ] CPU idle: **< 20%** (reduction de 50%)
- [ ] No compiler errors
- [ ] No runtime crashes

### Target Success  
- [ ] CPU idle: **< 15%** (reduction de 60%)
- [ ] CPU + 100 clients: **< 35%** (improved scaling)
- [ ] Memory: **Stable** (no growth)
- [ ] Logs: **Clean** (no warnings)

### Excellent Success
- [ ] CPU idle: **< 10%** (reduction de 75%)
- [ ] CPU + 1000 clients: **< 60%** (linear scaling)
- [ ] Startup: **< 30s** (unchanged)
- [ ] Memory profile: **Optimal** (predictable, no spikes)

---

## 🐛 Troubleshooting

### Si CPU reste haute (> 30%):
1. Vérifier les fichiers modifiés ont bien été sauvegardés
2. Vérifier recompilation effectuée
3. Chercher autres boucles busy-wait:
   ```bash
   grep -r "while (true)" src/ | grep -v "//"
   ```
4. Profiler avec dotTrace pour identifier hotspots

### Si Performance Degraded:
1. Vérifier les Sleep() values:
   - 10ms: trop court → CPU spike
   - 100ms+: trop long → latency
   - **Optimal: 10-20ms**
2. Vérifier ProcessQueries return type change
3. Check timeout values en socket cleanup

### Si Memory Leaks:
1. Vérifier GC happening normally
2. Profiler heap allocation
3. Check finalizers being called
4. Peut indiquer d'autres problèmes

---

## 📈 Rapporter les Résultats

Format pour rapporter:
```
CPU Consumption Test Results:
- Idle CPU Before: XX%
- Idle CPU After: XX%
- Reduction: XX%
- Under Load (100 clients): XX%
- Memory Stable: YES/NO
- Errors: NONE/DESCRIBE

Conclusion: SUCCESS / NEEDS_REVIEW
```

---

## 🚀 Prochaines Étapes après Validation

Si test SUCCEED:
1. ✅ Merge corrections into production
2. ✅ Monitor 24-48 hours
3. ✅ Document solution
4. ✅ Apply same fixes to other servers (realm, world)

Si test FAILS:
1. ⚠️  Check error logs
2. ⚠️  Revert specific changes
3. ⚠️  Profile with instrumentation
4. ⚠️  Investigate alternate causes

---

## 📞 Support

Si problèmes lors testing:
1. Vérifier les fichiers modifiés: [CPU_CONSUMPTION_FIX_REPORT.md](CPU_CONSUMPTION_FIX_REPORT.md)
2. Compiler avec `-VerboseLogging`
3. Exécuter tests de régression
4. Créer issue avec metrics complètes

---

**Last Updated**: May 11, 2026  
**Status**: Ready for Testing ✅
