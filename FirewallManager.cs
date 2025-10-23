using System;
using System.Diagnostics;

namespace Micromanager
{
    /// <summary>
    /// Gestiona la configuración del Firewall de Windows para permitir acceso remoto a carpetas compartidas
    /// </summary>
    public class FirewallManager
    {
        /// <summary>
        /// Configura las reglas de firewall necesarias para acceso remoto SMB
        /// </summary>
        public static bool ConfigureFirewallForRemoteAccess(string shareName, out string errorMessage, bool debugMode = false)
        {
            errorMessage = string.Empty;
            
            try
            {
                if (debugMode) Console.WriteLine("\n--- Configurando Firewall para Acceso Remoto ---");
                
                // 0. IMPORTANTE: Agregar regla específica para Micromanager.exe
                // Esto permite que la aplicación se comunique a través del firewall
                string installedExePath = Path.Combine(Configuration.InstallPath, Configuration.ExeName);
                
                // Eliminar reglas existentes para Micromanager.exe
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - Aplicación\"");
                
                // Crear regla para permitir TODAS las conexiones de Micromanager.exe
                var appRuleResult = ProcessExecutor.Execute(
                    "netsh",
                    $"advfirewall firewall add rule name=\"Micromanager - Aplicación\" " +
                    $"dir=in action=allow program=\"{installedExePath}\" enable=yes profile=any " +
                    $"description=\"Permite todas las conexiones de Micromanager\"");
                
                if (appRuleResult.Success && debugMode)
                {
                    Console.WriteLine($"  ✓ Regla de aplicación creada para: {installedExePath}");
                }
                
                // Crear también regla para conexiones SALIENTES (OUT)
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - Aplicación (Saliente)\"");
                
                var appRuleOutResult = ProcessExecutor.Execute(
                    "netsh",
                    $"advfirewall firewall add rule name=\"Micromanager - Aplicación (Saliente)\" " +
                    $"dir=out action=allow program=\"{installedExePath}\" enable=yes profile=any " +
                    $"description=\"Permite conexiones salientes de Micromanager\"");
                
                if (appRuleOutResult.Success && debugMode)
                {
                    Console.WriteLine($"  ✓ Regla saliente creada para: {installedExePath}");
                }
                
                // 1. Habilitar reglas predefinidas de "Compartir archivos e impresoras"
                string[] predefinedRules = new[]
                {
                    "File and Printer Sharing (SMB-In)",
                    "File and Printer Sharing (NB-Session-In)",
                    "File and Printer Sharing (NB-Name-In)",
                    "File and Printer Sharing (NB-Datagram-In)"
                };
                
                foreach (var ruleName in predefinedRules)
                {
                    var result = ProcessExecutor.Execute(
                        "netsh",
                        $"advfirewall firewall set rule name=\"{ruleName}\" new enable=yes profile=any");
                    
                    if (result.Success && debugMode)
                    {
                        Console.WriteLine($"  ✓ Regla habilitada: {ruleName}");
                    }
                }
                
                // 2. Crear reglas para TODOS los puertos SMB (ENTRADA y SALIDA)
                
                // Puerto TCP 445 (SMB) - ENTRADA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - SMB TCP 445 (Entrada)\"");
                var smb445InResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - SMB TCP 445 (Entrada)\" " +
                    "dir=in action=allow protocol=TCP localport=445 profile=any " +
                    $"description=\"Permite acceso SMB entrante para {shareName}\"");
                if (smb445InResult.Success && debugMode) Console.WriteLine("  ✓ Puerto TCP 445 (entrada) habilitado");
                
                // Puerto TCP 445 (SMB) - SALIDA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - SMB TCP 445 (Salida)\"");
                var smb445OutResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - SMB TCP 445 (Salida)\" " +
                    "dir=out action=allow protocol=TCP remoteport=445 profile=any " +
                    $"description=\"Permite acceso SMB saliente para {shareName}\"");
                if (smb445OutResult.Success && debugMode) Console.WriteLine("  ✓ Puerto TCP 445 (salida) habilitado");
                
                // Puerto TCP 139 (NetBIOS) - ENTRADA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - NetBIOS TCP 139 (Entrada)\"");
                var netbios139InResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - NetBIOS TCP 139 (Entrada)\" " +
                    "dir=in action=allow protocol=TCP localport=139 profile=any " +
                    "description=\"Permite NetBIOS entrante\"");
                if (netbios139InResult.Success && debugMode) Console.WriteLine("  ✓ Puerto TCP 139 (entrada) habilitado");
                
                // Puerto TCP 139 (NetBIOS) - SALIDA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - NetBIOS TCP 139 (Salida)\"");
                var netbios139OutResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - NetBIOS TCP 139 (Salida)\" " +
                    "dir=out action=allow protocol=TCP remoteport=139 profile=any " +
                    "description=\"Permite NetBIOS saliente\"");
                if (netbios139OutResult.Success && debugMode) Console.WriteLine("  ✓ Puerto TCP 139 (salida) habilitado");
                
                // Puerto UDP 137 (NetBIOS Name) - ENTRADA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - NetBIOS UDP 137 (Entrada)\"");
                var netbios137InResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - NetBIOS UDP 137 (Entrada)\" " +
                    "dir=in action=allow protocol=UDP localport=137 profile=any " +
                    "description=\"Permite NetBIOS Name entrante\"");
                if (netbios137InResult.Success && debugMode) Console.WriteLine("  ✓ Puerto UDP 137 (entrada) habilitado");
                
                // Puerto UDP 137 (NetBIOS Name) - SALIDA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - NetBIOS UDP 137 (Salida)\"");
                var netbios137OutResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - NetBIOS UDP 137 (Salida)\" " +
                    "dir=out action=allow protocol=UDP remoteport=137 profile=any " +
                    "description=\"Permite NetBIOS Name saliente\"");
                if (netbios137OutResult.Success && debugMode) Console.WriteLine("  ✓ Puerto UDP 137 (salida) habilitado");
                
                // Puerto UDP 138 (NetBIOS Datagram) - ENTRADA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - NetBIOS UDP 138 (Entrada)\"");
                var netbios138InResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - NetBIOS UDP 138 (Entrada)\" " +
                    "dir=in action=allow protocol=UDP localport=138 profile=any " +
                    "description=\"Permite NetBIOS Datagram entrante\"");
                if (netbios138InResult.Success && debugMode) Console.WriteLine("  ✓ Puerto UDP 138 (entrada) habilitado");
                
                // Puerto UDP 138 (NetBIOS Datagram) - SALIDA
                ProcessExecutor.Execute("netsh", "advfirewall firewall delete rule name=\"Micromanager - NetBIOS UDP 138 (Salida)\"");
                var netbios138OutResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall add rule name=\"Micromanager - NetBIOS UDP 138 (Salida)\" " +
                    "dir=out action=allow protocol=UDP remoteport=138 profile=any " +
                    "description=\"Permite NetBIOS Datagram saliente\"");
                if (netbios138OutResult.Success && debugMode) Console.WriteLine("  ✓ Puerto UDP 138 (salida) habilitado");
                
                // 3. HABILITAR "Compartir archivos e impresoras para redes Microsoft"
                // Esto hace que los recursos compartidos estén disponibles en la red
                try
                {
                    // Habilitar el binding de File and Printer Sharing en todos los adaptadores de red
                    var enableBindingResult = ProcessExecutor.Execute(
                        "powershell",
                        "-Command \"Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | ForEach-Object { Enable-NetAdapterBinding -Name $_.Name -ComponentID 'ms_server' -ErrorAction SilentlyContinue }\"");
                    
                    if (enableBindingResult.Success && debugMode)
                    {
                        Console.WriteLine("  ✓ 'Compartir archivos e impresoras' habilitado en adaptadores de red");
                    }
                }
                catch { /* No crítico, continuar */ }
                
                // 4. Verificar que el servicio Server (LanmanServer) esté ejecutándose
                var serverServiceResult = ProcessExecutor.Execute("sc", "query LanmanServer");
                if (!serverServiceResult.Output.Contains("RUNNING"))
                {
                    if (debugMode) Console.WriteLine("  ℹ Iniciando servicio de compartición de archivos...");
                    
                    var startResult = ProcessExecutor.Execute("net", "start LanmanServer");
                    if (startResult.Success && debugMode)
                    {
                        Console.WriteLine("  ✓ Servicio de compartición iniciado");
                    }
                }
                else if (debugMode)
                {
                    Console.WriteLine("  ✓ Servicio de compartición activo");
                }
                
                // 5. Habilitar servicios necesarios para descubrimiento de red
                try
                {
                    string[] networkServices = new[] { "FDResPub", "fdPHost", "SSDPSRV" };
                    foreach (var serviceName in networkServices)
                    {
                        // Configurar inicio automático
                        ProcessExecutor.Execute("sc", $"config {serviceName} start= auto");
                        // Intentar iniciar el servicio
                        ProcessExecutor.Execute("net", $"start {serviceName}");
                    }
                    
                    if (debugMode)
                    {
                        Console.WriteLine("  ✓ Servicios de descubrimiento de red configurados");
                    }
                }
                catch { /* No crítico, continuar */ }
                
                // 6. Habilitar descubrimiento de red
                var discoveryResult = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall set rule group=\"Network Discovery\" new enable=Yes profile=any");
                
                if (discoveryResult.Success && debugMode)
                {
                    Console.WriteLine("  ✓ Descubrimiento de red habilitado");
                }
                
                // 7. Configurar el registro para permitir acceso remoto a recursos compartidos
                try
                {
                    // Desactivar SMB v1 (inseguro)
                    var regResult = ProcessExecutor.Execute(
                        "reg",
                        "add HKLM\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters /v SMB1 /t REG_DWORD /d 0 /f");
                    
                    if (debugMode) Console.WriteLine("  ✓ SMB v1 desactivado (usando SMB v2/v3)");
                }
                catch { /* No crítico */ }
                
                // 8. Verificar estado del firewall
                try
                {
                    var firewallStateResult = ProcessExecutor.Execute(
                        "netsh",
                        "advfirewall show allprofiles state");
                    
                    if (debugMode && firewallStateResult.Success)
                    {
                        if (firewallStateResult.Output.Contains("ON"))
                        {
                            Console.WriteLine("  ℹ Firewall de Windows está activo (reglas configuradas)");
                        }
                    }
                }
                catch { /* No crítico */ }
                
                if (debugMode)
                {
                    Console.WriteLine($"\n✓ Firewall configurado para acceso remoto");
                    Console.WriteLine($"  Puertos habilitados: TCP 445, 139, 137-138 (UDP)");
                    Console.WriteLine($"  Acceso desde LAN: Habilitado");
                    Console.WriteLine($"  Compartir archivos: Habilitado");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error configurando firewall: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Elimina las reglas de firewall personalizadas creadas por Micromanager
        /// </summary>
        public static bool RemoveFirewallRules(string shareName, out string errorMessage, bool debugMode = false)
        {
            errorMessage = string.Empty;
            
            try
            {
                // Lista de TODAS las reglas a eliminar
                string[] rulesToDelete = new[]
                {
                    "Micromanager - Aplicación",
                    "Micromanager - Aplicación (Saliente)",
                    "Micromanager - SMB TCP 445 (Entrada)",
                    "Micromanager - SMB TCP 445 (Salida)",
                    "Micromanager - NetBIOS TCP 139 (Entrada)",
                    "Micromanager - NetBIOS TCP 139 (Salida)",
                    "Micromanager - NetBIOS UDP 137 (Entrada)",
                    "Micromanager - NetBIOS UDP 137 (Salida)",
                    "Micromanager - NetBIOS UDP 138 (Entrada)",
                    "Micromanager - NetBIOS UDP 138 (Salida)",
                    $"Micromanager - SMB Access ({shareName})" // Regla antigua (por compatibilidad)
                };
                
                foreach (var ruleName in rulesToDelete)
                {
                    var result = ProcessExecutor.Execute(
                        "netsh",
                        $"advfirewall firewall delete rule name=\"{ruleName}\"");
                    
                    if (result.Success && debugMode)
                    {
                        Console.WriteLine($"✓ Regla eliminada: {ruleName}");
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error eliminando reglas de firewall: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Verifica si las reglas de firewall están configuradas correctamente
        /// </summary>
        public static bool VerifyFirewallConfiguration(out string status)
        {
            try
            {
                var result = ProcessExecutor.Execute(
                    "netsh",
                    "advfirewall firewall show rule name=\"File and Printer Sharing (SMB-In)\"");
                
                if (result.Output.Contains("Enabled:") && result.Output.Contains("Yes"))
                {
                    status = "Firewall configurado correctamente para acceso remoto";
                    return true;
                }
                else
                {
                    status = "Las reglas de firewall no están habilitadas";
                    return false;
                }
            }
            catch
            {
                status = "No se pudo verificar la configuración del firewall";
                return false;
            }
        }
    }
}

