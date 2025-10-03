using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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
        private readonly BlockingCollection<string> _keyLogQueue = new BlockingCollection<string>();

        private IntPtr _hookId = IntPtr.Zero;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;

            string[] args = Environment.GetCommandLineArgs();
            _outputDir = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                         ? args[1]
                         : "C:\\Micromanager"; // Default to C:\Micromanager if no argument is provided

            _screenshotInterval = args.Length > 3 && int.TryParse(args[3], out int screenshotSeconds) && screenshotSeconds > 0
                ? TimeSpan.FromSeconds(screenshotSeconds)
                : TimeSpan.FromSeconds(10);

            try
            {
                // Log startup state
                string startupLogPath = Path.Combine(_outputDir, "startup.log");
                File.AppendAllText(startupLogPath, $"[{DateTime.UtcNow}] Starting. OutputDir: {_outputDir}, User: {Environment.UserName}, Interactive: {Environment.UserInteractive}{Environment.NewLine}");

                if (!Directory.Exists(_outputDir))
                {
                    Directory.CreateDirectory(_outputDir);
                    _logger.LogInformation("Output directory created: {OutputDir}", _outputDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize output directory or log startup state.");
                throw new InvalidOperationException("Critical failure during Worker initialization.", ex);
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
                await File.AppendAllTextAsync(startupLogPath, $"[{DateTime.UtcNow}] Program started. User: {Environment.UserName}, Machine: {Environment.MachineName}, Interactive: {Environment.UserInteractive}{Environment.NewLine}");

                LogExecutionContext();
                LogInfo("Worker service started.");

                // Ensure the output directory exists
                if (!Directory.Exists(_outputDir))
                {
                    Directory.CreateDirectory(_outputDir);
                    LogInfo("Output directory created.");
                }

                string lastActiveWindow = string.Empty;
                long sessionCounter = 0;

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
                    }
                    catch (Exception ex) when (ex is not TaskCanceledException)
                    {
                        LogError(ex);
                        await File.AppendAllTextAsync(startupLogPath, $"[{DateTime.UtcNow}] Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }

                    await Task.Delay(_screenshotInterval, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                string startupLogPath = Path.Combine(_outputDir, "startup.log");
                await File.AppendAllTextAsync(startupLogPath, $"[{DateTime.UtcNow}] Critical error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                throw;
            }
        }

        private void LogExecutionContext()
        {
            string infoLogPath = Path.Combine(_outputDir, "info.log");
            File.AppendAllTextAsync(infoLogPath, $"[{DateTime.UtcNow}] Execution started. User: {Environment.UserName}, Machine: {Environment.MachineName}, Interactive: {Environment.UserInteractive}{Environment.NewLine}");
        }

        private void LogError(Exception ex)
        {
            string errorLogPath = Path.Combine(_outputDir, "error.log");
            File.AppendAllTextAsync(errorLogPath, $"[{DateTime.UtcNow}] Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
        }

        private void LogInfo(string message)
        {
            string infoLogPath = Path.Combine(_outputDir, "info.log");
            File.AppendAllTextAsync(infoLogPath, $"[{DateTime.UtcNow}] {message}{Environment.NewLine}");
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
                foreach (var logEntry in _keyLogQueue.GetConsumingEnumerable(stoppingToken))
                {
                    await File.AppendAllTextAsync(_keyLogFile, logEntry, stoppingToken);
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
                    string logEntry = $"[{DateTime.UtcNow:o}] {key}{Environment.NewLine}";
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

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(_outputDir, $"capture_{timestamp}_{captureCounter:D4}.png");

                await Task.Run(() => bitmap.Save(filePath, ImageFormat.Png), stoppingToken);

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
                Timestamp = DateTime.UtcNow,
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
                Timestamp = DateTime.UtcNow,
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
                string jsonLine = JsonSerializer.Serialize(logEntry) + Environment.NewLine;
                await File.AppendAllTextAsync(_logFile, jsonLine, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to JSON log file.");
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
