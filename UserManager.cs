using System;
using System.DirectoryServices.AccountManagement;
using System.Security;

namespace Micromanager
{
    /// <summary>
    /// Gestión de usuarios locales de Windows usando API nativa de .NET
    /// </summary>
    public class UserManager
    {
        /// <summary>
        /// Valida que el nombre de usuario sea seguro (previene inyección)
        /// </summary>
        public static bool ValidateUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;
            
            // Solo permitir letras, números, guiones y guiones bajos
            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }
            
            // Longitud razonable
            return username.Length >= 3 && username.Length <= 20;
        }
        
        /// <summary>
        /// Crea un usuario local de Windows con contraseña segura
        /// </summary>
        public static bool CreateOrUpdateLocalUser(string username, string password, out string errorMessage)
        {
            errorMessage = string.Empty;
            
            try
            {
                // Validar entrada
                if (!ValidateUsername(username))
                {
                    errorMessage = "Nombre de usuario inválido. Use solo letras, números, guiones y guiones bajos (3-20 caracteres)";
                    return false;
                }
                
                if (string.IsNullOrEmpty(password))
                {
                    errorMessage = Configuration.Messages.EmptyPasswordNotAllowed;
                    return false;
                }
                
                using (var context = new PrincipalContext(ContextType.Machine))
                {
                    UserPrincipal? user = UserPrincipal.FindByIdentity(context, username);
                    
                    if (user != null)
                    {
                        // El usuario existe, actualizar contraseña
                        Console.WriteLine($"ℹ Usuario '{username}' ya existe, actualizando contraseña...");
                        user.SetPassword(password);
                        user.PasswordNeverExpires = true;
                        user.UserCannotChangePassword = false;
                        user.Save();
                        Console.WriteLine($"✓ Contraseña actualizada para usuario '{username}'");
                    }
                    else
                    {
                        // Crear nuevo usuario
                        user = new UserPrincipal(context)
                        {
                            Name = username,
                            Description = "Usuario para acceso a carpeta compartida Micromanager",
                            UserCannotChangePassword = false,
                            PasswordNeverExpires = true,
                            Enabled = true
                        };
                        
                        user.SetPassword(password);
                        user.Save();
                        
                        Console.WriteLine($"✓ Usuario local '{username}' creado exitosamente");
                    }
                    
                    user?.Dispose();
                }
                
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = Configuration.Messages.RequiresAdmin;
                return false;
            }
            catch (PasswordException ex)
            {
                errorMessage = $"Error en la contraseña: {ex.Message}. Use una contraseña más compleja.";
                return false;
            }
            catch (PrincipalOperationException ex)
            {
                errorMessage = $"Error al gestionar el usuario: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error inesperado: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Verifica si un usuario local existe
        /// </summary>
        public static bool UserExists(string username)
        {
            try
            {
                if (!ValidateUsername(username))
                    return false;
                
                using var context = new PrincipalContext(ContextType.Machine);
                using var user = UserPrincipal.FindByIdentity(context, username);
                return user != null;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Elimina un usuario local
        /// </summary>
        public static bool DeleteLocalUser(string username, out string errorMessage, bool debugMode = false)
        {
            errorMessage = string.Empty;
            
            try
            {
                if (!ValidateUsername(username))
                {
                    errorMessage = "Nombre de usuario inválido";
                    return false;
                }
                
                using var context = new PrincipalContext(ContextType.Machine);
                using var user = UserPrincipal.FindByIdentity(context, username);
                
                if (user != null)
                {
                    user.Delete();
                    if (debugMode) Console.WriteLine($"✓ Usuario '{username}' eliminado");
                    return true;
                }
                else
                {
                    if (debugMode) Console.WriteLine($"ℹ Usuario '{username}' no existe");
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = Configuration.Messages.RequiresAdmin;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error al eliminar usuario: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Oculta un usuario de la pantalla de inicio de sesión de Windows
        /// </summary>
        public static bool HideUserFromLoginScreen(string username, out string errorMessage)
        {
            errorMessage = string.Empty;
            
            try
            {
                // Crear la clave de registro para ocultar el usuario
                // HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\SpecialAccounts\UserList
                var hideUserResult = ProcessExecutor.Execute(
                    "reg",
                    $"add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\\SpecialAccounts\\UserList\" " +
                    $"/v {username} /t REG_DWORD /d 0 /f");
                
                if (hideUserResult.Success)
                {
                    return true;
                }
                else
                {
                    errorMessage = $"No se pudo ocultar el usuario: {hideUserResult.Error}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error ocultando usuario: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Muestra un usuario en la pantalla de inicio de sesión de Windows
        /// </summary>
        public static bool ShowUserInLoginScreen(string username, out string errorMessage)
        {
            errorMessage = string.Empty;
            
            try
            {
                // Eliminar la clave de registro para mostrar el usuario
                var showUserResult = ProcessExecutor.Execute(
                    "reg",
                    $"delete \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\\SpecialAccounts\\UserList\" " +
                    $"/v {username} /f");
                
                // Ignorar si no existe (significa que ya está visible)
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error mostrando usuario: {ex.Message}";
                return false;
            }
        }
    }
}

