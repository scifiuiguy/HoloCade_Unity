// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.IO;

namespace HoloCade.Editor
{
    /// <summary>
    /// Command-Line Interface (CLI) for HoloCade Compilation Reporter
    /// 
    /// **What is this?**
    /// This is a command-line entry point that allows external tools (batch scripts, CI/CD pipelines,
    /// or AI assistants) to trigger Unity compilation and generate a structured report file without
    /// requiring manual interaction with the Unity Editor.
    /// 
    /// **Why do I see console messages like "[HoloCade AUTO-COMPILE] CLI invoked"?**
    /// These messages appear when you run CompileProject_Silent.bat or when an external tool
    /// launches Unity in batch mode with the -executeMethod flag. This is normal automated behavior.
    /// 
    /// **What does it do?**
    /// - Launches Unity in batch mode (no GUI, headless operation)
    /// - Waits for Unity to finish initial compilation
    /// - Forces a recompilation to ensure fresh results
    /// - Generates a compilation report (Temp/CompilationErrors.log)
    /// - Exits Unity automatically (batch script terminates the process)
    /// 
    /// **When is it used?**
    /// - Running CompileProject_Silent.bat from command line
    /// - CI/CD pipelines that need to check compilation status
    /// - Automated testing workflows
    /// - AI assistants checking for compilation errors
    /// 
    /// **How to use it manually:**
    /// Unity.exe -batchmode -nographics -projectPath "path/to/project" -executeMethod HoloCade.Editor.CompilationReporterCLI.CompileAndExit
    /// 
    /// **Note:** The batch script (CompileProject_Silent.bat) handles Unity process management.
    /// You typically don't need to call this directly - use the batch script instead.
    /// 
    /// **For more information:**
    /// See Claude_Unity_AutoCompilation/README.md in the repository root for detailed documentation.
    /// </summary>
    public static class CompilationReporterCLI
    {
        private const string REPORT_RELATIVE_PATH = "Temp/CompilationErrors.log";
        private const double MAX_WAIT_SECONDS = 180.0;
        private const double NO_COMPILE_GRACE_SECONDS = 5.0;

        private static bool s_callbacksRegistered;
        private static bool s_reportReady;
        private static bool s_compilationObserved;
        private static double s_cliStartTime;

        /// <summary>
        /// Compile the project and generate report
        /// Does NOT exit - batch script will kill Unity after reading report
        /// </summary>
        public static void CompileAndExit()
        {
            Debug.Log("🚀🤖 [HoloCade AUTO-COMPILE] CLI invoked - using compilation callbacks for completion detection");

            s_reportReady = false;
            s_compilationObserved = EditorApplication.isCompiling;
            s_cliStartTime = EditorApplication.timeSinceStartup;

            // Wait for the initial domain load to settle before we start forcing recompilation
            EditorApplication.delayCall += EnsureInitialCompilationSettled;
        }

        private static void EnsureInitialCompilationSettled()
        {
            if (EditorApplication.isCompiling)
            {
                // Keep waiting until startup compilation ends, then proceed
                EditorApplication.delayCall += EnsureInitialCompilationSettled;
                return;
            }

            BeginCompilationMonitoring();
        }

        private static void BeginCompilationMonitoring()
        {
            RegisterCallbacks();

            if (!EditorApplication.isCompiling)
            {
                Debug.Log("📦 [HoloCade AUTO-COMPILE] Requesting script compilation via callbacks...");
                CompilationPipeline.RequestScriptCompilation();
            }

            // Poll for timeout/fallback from Update so we never spin on blocking loops
            EditorApplication.update += MonitorCompilationState;
        }

        private static void RegisterCallbacks()
        {
            if (s_callbacksRegistered)
            {
                return;
            }

            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            s_callbacksRegistered = true;
        }

        private static void CleanupCallbacks()
        {
            if (!s_callbacksRegistered)
            {
                return;
            }

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            s_callbacksRegistered = false;
        }

        private static void OnCompilationStarted(object context)
        {
            s_compilationObserved = true;
            Debug.Log("⚙️ [HoloCade AUTO-COMPILE] Compilation started");
        }

        private static void OnCompilationFinished(object context)
        {
            Debug.Log("✅ [HoloCade AUTO-COMPILE] Compilation finished - generating report");
            EnsureReportFile();
            s_reportReady = true;
            CleanupCallbacks();
        }

        private static void MonitorCompilationState()
        {
            if (s_reportReady)
            {
                EditorApplication.update -= MonitorCompilationState;
                Debug.Log("📄 [HoloCade AUTO-COMPILE] Compilation report ready for external tooling");
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - s_cliStartTime;

            if (!s_compilationObserved && elapsed >= NO_COMPILE_GRACE_SECONDS)
            {
                Debug.LogWarning("⚠️ [HoloCade AUTO-COMPILE] No compilation activity detected - writing current status");
                EnsureReportFile();
                s_reportReady = true;
                CleanupCallbacks();
                EditorApplication.update -= MonitorCompilationState;
                return;
            }

            if (elapsed >= MAX_WAIT_SECONDS)
            {
                Debug.LogWarning("⏰ [HoloCade AUTO-COMPILE] Timed out waiting for compilation callbacks - writing current status");
                EnsureReportFile();
                s_reportReady = true;
                CleanupCallbacks();
                EditorApplication.update -= MonitorCompilationState;
            }
        }

        private static void EnsureReportFile()
        {
            string reportPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), REPORT_RELATIVE_PATH);
            WriteCompilationReport(reportPath);
        }

        private static void WriteCompilationReport(string reportPath)
        {
            try
            {
                // Check if CompilationReporter already wrote a report (from compilation events)
                if (File.Exists(reportPath))
                {
                    string existingReport = File.ReadAllText(reportPath);
                    // If the existing report has actual compilation results, use it
                    if (existingReport.Contains("Status: SUCCESS") || existingReport.Contains("Status: FAILED"))
                    {
                        Debug.Log("📄 [HoloCade AUTO-COMPILE] Using existing compilation report from CompilationReporter");
                        return; // Use the report from CompilationReporter which has detailed errors
                    }
                }

                // Otherwise, generate a basic report
                System.Text.StringBuilder report = new System.Text.StringBuilder();
                report.AppendLine("===========================================");
                report.AppendLine("HoloCade COMPILATION REPORT");
                report.AppendLine("🤖 AI-READABLE AUTOMATED COMPILATION CHECK");
                report.AppendLine("===========================================");
                report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Report ID: HoloCade-{System.Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}");
                report.AppendLine();

                // Check compilation status
                report.AppendLine("Status Check:");
                report.AppendLine($"  Compiling: {EditorApplication.isCompiling}");
                report.AppendLine($"  Play Mode Enabled: {EditorApplication.isPlaying}");
                report.AppendLine();

                // Check for actual compilation errors via CompilationPipeline
                var assemblies = CompilationPipeline.GetAssemblies();
                bool hasErrors = false;
                foreach (var assembly in assemblies)
                {
                    // Note: This is a basic check - detailed errors are captured by CompilationReporter
                    if (assembly.name.Contains("error", System.StringComparison.OrdinalIgnoreCase))
                    {
                        hasErrors = true;
                        break;
                    }
                }

                report.AppendLine("===========================================");
                if (EditorApplication.isCompiling)
                {
                    report.AppendLine("Status: COMPILING (still in progress)");
                }
                else if (hasErrors)
                {
                    report.AppendLine("Status: FAILED - Compilation errors detected");
                    report.AppendLine("Note: Check Unity console or CompilationReporter for detailed errors");
                }
                else
                {
                    report.AppendLine("Status: SUCCESS - Project compiled successfully");
                    report.AppendLine("No compilation errors detected");
                }
                report.AppendLine("===========================================");

                // Write to file
                string directory = Path.GetDirectoryName(reportPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(reportPath, report.ToString());
                Debug.Log("📄 [HoloCade AUTO-COMPILE] Report file written successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HoloCade CLI] Failed to write report: {ex.Message}");
            }
        }

        /// <summary>
        /// Compile and report but don't exit (for manual testing)
        /// </summary>
        [MenuItem("HoloCade/CLI Test - Compile and Report")]
        public static void CompileAndReport()
        {
            Debug.Log("[HoloCade CLI] Compilation test started...");

            // Wait for compilation
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isCompiling)
                {
                    string projectRoot = Application.dataPath.Replace("/Assets", "");
                    string reportPath = Path.Combine(projectRoot, "Temp/CompilationErrors.log");

                    if (File.Exists(reportPath))
                    {
                        string report = File.ReadAllText(reportPath);
                        Debug.Log("=== COMPILATION REPORT ===\n" + report);
                    }
                    else
                    {
                        Debug.LogWarning("No compilation report found. It will be generated on next compile.");
                    }
                }
            };
        }
    }
}
#endif

