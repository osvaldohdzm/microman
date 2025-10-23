using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Micromanager
{
    /// <summary>
    /// Ejecutor centralizado de procesos externos con manejo robusto de errores
    /// </summary>
    public class ProcessExecutor
    {
        public class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
            public bool Success => ExitCode == 0;
        }
        
        /// <summary>
        /// Ejecuta un proceso externo de manera segura y captura toda su salida
        /// </summary>
        public static async Task<ProcessResult> ExecuteAsync(
            string fileName,
            string arguments,
            int timeoutMs = 30000,
            bool createNoWindow = true)
        {
            var result = new ProcessResult();
            
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = createNoWindow,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputBuilder.AppendLine(e.Data);
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        errorBuilder.AppendLine(e.Data);
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                var completed = await Task.Run(() => process.WaitForExit(timeoutMs));
                
                if (!completed)
                {
                    try
                    {
                        process.Kill();
                        result.Error = $"Process timed out after {timeoutMs}ms";
                        result.ExitCode = -1;
                        return result;
                    }
                    catch
                    {
                        // Ignore errors when killing process
                    }
                }
                
                result.ExitCode = process.ExitCode;
                result.Output = outputBuilder.ToString();
                result.Error = errorBuilder.ToString();
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = $"Failed to execute process: {ex.Message}";
            }
            
            return result;
        }
        
        /// <summary>
        /// Ejecuta un proceso de manera síncrona
        /// </summary>
        public static ProcessResult Execute(
            string fileName,
            string arguments,
            int timeoutMs = 30000,
            bool createNoWindow = true)
        {
            return ExecuteAsync(fileName, arguments, timeoutMs, createNoWindow).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Ejecuta un proceso con reintentos en caso de fallo
        /// </summary>
        public static async Task<ProcessResult> ExecuteWithRetryAsync(
            string fileName,
            string arguments,
            int maxRetries = 3,
            int delayMs = 1000)
        {
            ProcessResult? lastResult = null;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                lastResult = await ExecuteAsync(fileName, arguments);
                
                if (lastResult.Success)
                    return lastResult;
                
                if (attempt < maxRetries - 1)
                    await Task.Delay(delayMs);
            }
            
            return lastResult ?? new ProcessResult { ExitCode = -1, Error = "No attempts were made" };
        }
    }
}

