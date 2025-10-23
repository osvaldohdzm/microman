using System;
using System.IO;

namespace Micromanager
{
    /// <summary>
    /// Configuración centralizada del sistema Micromanager
    /// </summary>
    public static class Configuration
    {
        // Rutas dinámicas del sistema
        private static readonly string CommonAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        
        public static readonly string InstallPath = Path.Combine(CommonAppDataPath, "microman");
        public static readonly string DataPath = Path.Combine(InstallPath, "data");
        public static readonly string ExeName = "Micromanager.exe";
        public static readonly string ShareName = "microman$";
        public static readonly string TaskName = "Micromanager";
        
        // Valores por defecto
        public const int DefaultScreenshotInterval = 30;
        public const int DefaultCleanupDays = 0;
        public const string DefaultSharedUser = "SoporteManager";
        
        // Archivos de log
        public static string GetLogFilePath(string logName)
        {
            return Path.Combine(InstallPath, logName);
        }
        
        public static string GetDataLogFilePath(string logName)
        {
            return Path.Combine(DataPath, logName);
        }
        
        // Mensajes de texto
        public static class Messages
        {
            public const string RequiresAdmin = "⚠ Se requieren privilegios de administrador para esta operación";
            public const string InstallationComplete = "✓ Instalación completada exitosamente";
            public const string CleanupComplete = "✓ Limpieza completada";
            public const string PasswordMismatch = "⚠ Las contraseñas no coinciden";
            public const string EmptyPasswordNotAllowed = "⚠ Contraseña vacía no permitida";
        }
    }
}

