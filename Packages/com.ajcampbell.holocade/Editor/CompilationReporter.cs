// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.IO;
using System.Text;
using System.Linq;

namespace HoloCade.Editor
{
    /// <summary>
    /// Compilation Reporter for HoloCade
    /// 
    /// **What is this?**
    /// This is an automated compilation monitoring system that captures Unity compilation errors
    /// and writes them to a structured report file (Temp/CompilationErrors.log) that can be read
    /// by external tools, CI/CD pipelines, or AI assistants.
    /// 
    /// **Why do I see console messages like "[HoloCade AUTO-COMPILE]"?**
    /// These messages indicate that the automated compilation system is active and monitoring
    /// your project's compilation status. This is normal and expected behavior.
    /// 
    /// **What does it do?**
    /// - Automatically monitors Unity's compilation pipeline
    /// - Captures compilation errors and warnings as they occur
    /// - Writes structured reports to Temp/CompilationErrors.log
    /// - Enables external tools to check compilation status without opening Unity Editor
    /// 
    /// **When is it used?**
    /// - During automated builds (CI/CD pipelines)
    /// - When running CompileProject_Silent.bat script
    /// - When AI assistants need to check compilation status
    /// - Any time Unity compiles scripts (normal editor usage)
    /// 
    /// **Can I disable it?**
    /// This system is lightweight and doesn't impact normal Unity Editor usage. If you need to
    /// disable it, you can comment out the [InitializeOnLoad] attribute, but this is not recommended
    /// as it helps with automated testing and development workflows.
    /// 
    /// **For more information:**
    /// See Claude_Unity_AutoCompilation/README.md in the repository root for detailed documentation.
    /// </summary>
    [InitializeOnLoad]
    public static class CompilationReporter
    {
        private const string OUTPUT_FILE = "Temp/CompilationErrors.log";
        private static StringBuilder errorLog = new StringBuilder();
        private static bool compilationStarted = false;

        static CompilationReporter()
        {
            // Subscribe to compilation events
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

            Debug.Log("🤖 [HoloCade AUTO-COMPILE] Reporter initialized - automated compilation monitoring active");

            // If running in batch mode, force a compilation report on initialization
            if (Application.isBatchMode)
            {
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isCompiling)
                    {
                        // Check for existing errors and generate report
                        GenerateCurrentReport();
                    }
                };
            }
        }

        private static void OnCompilationStarted(object obj)
        {
            compilationStarted = true;
            errorLog.Clear();
            errorLog.AppendLine("===========================================");
            errorLog.AppendLine("HoloCade COMPILATION REPORT");
            errorLog.AppendLine("🤖 AI-READABLE AUTOMATED COMPILATION CHECK");
            errorLog.AppendLine("===========================================");
            errorLog.AppendLine($"Started: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            errorLog.AppendLine($"Report ID: HoloCade-{System.Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}");
            errorLog.AppendLine();
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (messages == null || messages.Length == 0)
            {
                return;
            }

            // Filter for errors and warnings
            var errors = messages.Where(m => m.type == CompilerMessageType.Error).ToArray();
            var warnings = messages.Where(m => m.type == CompilerMessageType.Warning).ToArray();

            if (errors.Length > 0 || warnings.Length > 0)
            {
                errorLog.AppendLine($"Assembly: {Path.GetFileName(assemblyPath)}");
                errorLog.AppendLine("-------------------------------------------");

                // Log errors
                if (errors.Length > 0)
                {
                    errorLog.AppendLine($"ERRORS: {errors.Length}");
                    foreach (var error in errors)
                    {
                        errorLog.AppendLine($"  [{error.type}] {error.file}({error.line},{error.column}): {error.message}");
                    }
                    errorLog.AppendLine();
                }

                // Log warnings
                if (warnings.Length > 0)
                {
                    errorLog.AppendLine($"WARNINGS: {warnings.Length}");
                    foreach (var warning in warnings)
                    {
                        errorLog.AppendLine($"  [{warning.type}] {warning.file}({warning.line},{warning.column}): {warning.message}");
                    }
                    errorLog.AppendLine();
                }
            }
        }

        private static void OnCompilationFinished(object obj)
        {
            if (!compilationStarted)
            {
                return;
            }

            compilationStarted = false;

            // Get final compilation status
            bool hasErrors = CompilationPipeline.codeOptimization == CodeOptimization.None;
            
            errorLog.AppendLine("===========================================");
            errorLog.AppendLine($"Finished: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            if (errorLog.ToString().Contains("ERRORS:"))
            {
                errorLog.AppendLine("Status: FAILED - Compilation errors detected");
            }
            else if (errorLog.ToString().Contains("WARNINGS:"))
            {
                errorLog.AppendLine("Status: SUCCESS (with warnings)");
            }
            else
            {
                errorLog.AppendLine("Status: SUCCESS - No errors or warnings");
            }
            
            errorLog.AppendLine("===========================================");

            // Write to file
            try
            {
                string projectRoot = Application.dataPath.Replace("/Assets", "");
                string outputPath = Path.Combine(projectRoot, OUTPUT_FILE);
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, errorLog.ToString());
                Debug.Log($"✅ [HoloCade AUTO-COMPILE] Report generated successfully → {outputPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HoloCade] Failed to write compilation report: {ex.Message}");
            }
        }

        /// <summary>
        /// Menu item to manually trigger compilation and report
        /// </summary>
        [MenuItem("HoloCade/Compile and Report Errors")]
        public static void ManualCompileAndReport()
        {
            Debug.Log("[HoloCade] Manual compilation triggered...");
            
            // Clear console
            var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearMethod.Invoke(null, null);

            // Force recompile
            CompilationPipeline.RequestScriptCompilation();
        }

        /// <summary>
        /// Menu item to open the compilation report file
        /// </summary>
        [MenuItem("HoloCade/Open Compilation Report")]
        public static void OpenCompilationReport()
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string outputPath = Path.Combine(projectRoot, OUTPUT_FILE);

            if (File.Exists(outputPath))
            {
                // Open in default text editor
                System.Diagnostics.Process.Start(outputPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Compilation Report", 
                    "No compilation report found. Try compiling first (HoloCade > Compile and Report Errors).", 
                    "OK");
            }
        }

        /// <summary>
        /// Get the current compilation status
        /// </summary>
        public static string GetCompilationStatus()
        {
            if (EditorApplication.isCompiling)
            {
                return "Compiling...";
            }
            else if (CompilationPipeline.codeOptimization == CodeOptimization.None)
            {
                return "Compilation Failed";
            }
            else
            {
                return "Compilation Successful";
            }
        }

        /// <summary>
        /// Generate a compilation report based on current state (for batch mode)
        /// </summary>
        private static void GenerateCurrentReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("===========================================");
            report.AppendLine("HoloCade COMPILATION REPORT (BATCH MODE)");
            report.AppendLine("===========================================");
            report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // Check if project has compilation errors
            var assemblies = CompilationPipeline.GetAssemblies();
            // Note: Detailed errors are captured during compilation events above
            // This section is for additional status checks if needed

            report.AppendLine("===========================================");
            if (EditorApplication.isCompiling)
            {
                report.AppendLine("Status: COMPILING (in progress)");
            }
            else
            {
                report.AppendLine("Status: No active compilation");
                report.AppendLine("Note: Detailed errors captured during compilation events");
                report.AppendLine("      Run CompileAndExit method to force compilation check");
            }
            report.AppendLine("===========================================");

            // Write to file
            try
            {
                string projectRoot = Application.dataPath.Replace("/Assets", "");
                string outputPath = Path.Combine(projectRoot, OUTPUT_FILE);
                
                string directory = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, report.ToString());
                Debug.Log($"✅ [HoloCade AUTO-COMPILE] Batch mode report generated → {outputPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HoloCade] Failed to write batch report: {ex.Message}");
            }
        }
    }
}
#endif
