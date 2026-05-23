/*
 * Memory Leak Monitoring Script
 * Add this to LogonConsole.cs to monitor resource usage
 */

using System;
using System.Diagnostics;
using WaadShared;

namespace LogonServer;

public static class ResourceMonitor
{
    private static DateTime lastReportTime = DateTime.Now;
    private static long lastMemory = 0;
    private const int REPORT_INTERVAL_SECONDS = 300; // Report every 5 minutes

    public static void LogResourceMetrics()
    {
        var now = DateTime.Now;
        if ((now - lastReportTime).TotalSeconds < REPORT_INTERVAL_SECONDS)
            return;

        lastReportTime = now;

        try
        {
            // Get memory info
            long currentMemory = GC.GetTotalMemory(false);
            long memoryMB = currentMemory / (1024 * 1024);
            long memoryGrowth = currentMemory - lastMemory;
            long growthMB = memoryGrowth / (1024 * 1024);
            lastMemory = currentMemory;

            // Get active connections
            int socketCount = AuthSocket.GetActiveSocketCount();

            // Get process info
            var process = Process.GetCurrentProcess();
            long workingSetMB = process.WorkingSet64 / (1024 * 1024);
            int threadCount = process.Threads.Count;

            // Log metrics
            CLog.Notice("[ResourceMonitor]", 
                $"Memory: {memoryMB} MB (GC: +{growthMB} MB), " +
                $"WorkingSet: {workingSetMB} MB, " +
                $"ActiveSockets: {socketCount}, " +
                $"Threads: {threadCount}");

            // Alert if memory growing too fast
            if (growthMB > 50) // More than 50 MB growth in 5 minutes
            {
                CLog.Warning("[ResourceMonitor]", 
                    $"⚠️ HIGH MEMORY GROWTH: +{growthMB} MB in 5 minutes!");
            }

            // Alert if too many sockets
            if (socketCount > 5000) // Arbitrary threshold
            {
                CLog.Warning("[ResourceMonitor]", 
                    $"⚠️ HIGH SOCKET COUNT: {socketCount} active connections");
            }

            // Alert if too many threads
            if (threadCount > 200)
            {
                CLog.Warning("[ResourceMonitor]", 
                    $"⚠️ HIGH THREAD COUNT: {threadCount} threads");
            }
        }
        catch (Exception ex)
        {
            CLog.Error("[ResourceMonitor]", $"Monitoring failed: {ex.Message}");
        }
    }

    public static void PrintDetailedReport()
    {
        CLog.Notice("[ResourceMonitor]", "=== Detailed Resource Report ===");

        // Memory info
        long totalMemory = GC.GetTotalMemory(false);
        long gen0 = GC.GetTotalMemory(false);
        
        CLog.Notice("[ResourceMonitor]", $"GC Heap: {totalMemory / 1024 / 1024} MB");
        CLog.Notice("[ResourceMonitor]", $"Gen 0 Collections: {GC.CollectionCount(0)}");
        CLog.Notice("[ResourceMonitor]", $"Gen 1 Collections: {GC.CollectionCount(1)}");
        CLog.Notice("[ResourceMonitor]", $"Gen 2 Collections: {GC.CollectionCount(2)}");

        // Socket info
        int socketCount = AuthSocket.GetActiveSocketCount();
        CLog.Notice("[ResourceMonitor]", $"Active AuthSockets: {socketCount}");

        // Process info
        var process = Process.GetCurrentProcess();
        CLog.Notice("[ResourceMonitor]", $"Working Set: {process.WorkingSet64 / 1024 / 1024} MB");
        CLog.Notice("[ResourceMonitor]", $"Private Memory: {process.PrivateMemorySize64 / 1024 / 1024} MB");
        CLog.Notice("[ResourceMonitor]", $"Threads: {process.Threads.Count}");
        CLog.Notice("[ResourceMonitor]", $"Handles: {process.HandleCount}");

        // CPU info
        CLog.Notice("[ResourceMonitor]", $"CPU Time: {process.TotalProcessorTime}");

        CLog.Notice("[ResourceMonitor]", "=== End Report ===");
    }
}

/*
 * Integration with LogonConsole.cs:
 * 
 * Add this to LogonConsole.ProcessCmd():
 * 
 *  var cmds = new Dictionary<string, Action<string>>
 *  {
 *      { "?", TranslateHelp }, 
 *      { "help", TranslateHelp },
 *      { "reload", ReloadAccts },
 *      { "rehash", TranslateRehash },
 *      { "shutdown", TranslateQuit }, 
 *      { "quit", TranslateQuit }, 
 *      { "exit", TranslateQuit },
 *      { "sockets", (s) => ResourceMonitor.PrintDetailedReport() }, // NEW
 *      { "memory", (s) => ResourceMonitor.PrintDetailedReport() },   // NEW
 *  };
 * 
 * Then add this to your main loop (in PeriodicFunctionCaller or similar):
 *  ResourceMonitor.LogResourceMetrics();
 */
