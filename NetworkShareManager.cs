using System;
using System.IO;

namespace Micromanager
{
    /// <summary>
    /// Gestión de carpetas compartidas de red
    /// </summary>
    public class NetworkShareManager
    {
        /// <summary>
        /// Crea una carpeta compartida de red con acceso restringido a un usuario específico
        /// </summary>
    public static bool CreateShare(
        string shareName,
        string localPath,
        string username,
        out string errorMessage,
        bool debugMode = false)
    {
        errorMessage = string.Empty;
        
        try
        {
            // Validar entrada
            if (!UserManager.ValidateUsername(username))
            {
                errorMessage = "Nombre de usuario inválido para la carpeta compartida";
                return false;
            }
            
            if (!Directory.Exists(localPath))
            {
                errorMessage = $"La ruta local no existe: {localPath}";
                return false;
            }
            
            if (debugMode) Console.WriteLine("\n--- Configurando Carpeta Compartida ---");
            
            // Verificar si el recurso compartido ya existe y eliminarlo
            var checkResult = ProcessExecutor.Execute("net", $"share {shareName}");
            if (checkResult.Success)
            {
                var deleteResult = ProcessExecutor.Execute("net", $"share {shareName} /delete /yes");
                if (!deleteResult.Success && !deleteResult.Error.Contains("no existe"))
                {
                    errorMessage = $"No se pudo eliminar el recurso compartido existente: {deleteResult.Error}";
                    return false;
                }
            }
            
            // IMPORTANTE: Habilitar acceso remoto de cuentas locales
            // Sin esto, Windows bloquea el acceso desde la red
            var regResult = ProcessExecutor.Execute(
                "reg",
                "add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v LocalAccountTokenFilterPolicy /t REG_DWORD /d 1 /f");
            
            if (debugMode && regResult.Success)
            {
                Console.WriteLine("  ✓ Acceso remoto de cuentas locales habilitado");
            }
            
            // Configurar permisos NTFS en la carpeta local
            try
            {
                var icaclsResult = ProcessExecutor.Execute(
                    "icacls",
                    $"\"{localPath}\" /grant {username}:(OI)(CI)F /T");
                
                if (debugMode && icaclsResult.Success)
                {
                    Console.WriteLine($"  ✓ Permisos NTFS configurados para {username}");
                }
            }
            catch { /* No crítico, continuar */ }
            
            // Crear recurso compartido con permisos específicos para el usuario
            string escapedPath = localPath.Replace("\"", "\\\"");
            var createResult = ProcessExecutor.Execute(
                "net",
                $"share {shareName}=\"{escapedPath}\" /GRANT:{username},FULL");
            
            if (createResult.Success)
            {
                if (debugMode)
                {
                    Console.WriteLine($"  ✓ Carpeta compartida creada: \\\\{Environment.MachineName}\\{shareName}");
                    Console.WriteLine($"    Ruta local: {localPath}");
                    Console.WriteLine($"    Acceso exclusivo para: {Environment.MachineName}\\{username}");
                    Console.WriteLine($"    Seguridad: Solo el usuario '{username}' puede acceder");
                }
                return true;
            }
            else
            {
                errorMessage = $"No se pudo crear la carpeta compartida: {createResult.Error}";
                return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error configurando carpeta compartida: {ex.Message}";
            return false;
        }
    }
        
        /// <summary>
        /// Elimina una carpeta compartida de red
        /// </summary>
        public static bool DeleteShare(string shareName, out string errorMessage, bool debugMode = false)
        {
            errorMessage = string.Empty;
            
            try
            {
                var result = ProcessExecutor.Execute("net", $"share {shareName} /delete /yes");
                
                if (result.Success)
                {
                    if (debugMode) Console.WriteLine($"✓ Carpeta compartida '{shareName}' eliminada");
                    return true;
                }
                else if (result.Error.Contains("no existe") || result.Error.Contains("not exist"))
                {
                    if (debugMode) Console.WriteLine($"ℹ Carpeta compartida '{shareName}' no existe");
                    return true;
                }
                else
                {
                    errorMessage = $"Error al eliminar carpeta compartida: {result.Error}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error eliminando carpeta compartida: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Verifica si una carpeta compartida existe
        /// </summary>
        public static bool ShareExists(string shareName)
        {
            try
            {
                var result = ProcessExecutor.Execute("net", $"share {shareName}");
                return result.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}

