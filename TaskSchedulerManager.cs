using System;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;

namespace Micromanager
{
    /// <summary>
    /// Gestión de tareas programadas usando la API nativa de Windows
    /// </summary>
    public class TaskSchedulerManager
    {
        /// <summary>
        /// Detecta si está ejecutándose como SYSTEM
        /// </summary>
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

        /// <summary>
        /// Crea tarea usando schtasks.exe con XML (fallback para SYSTEM)
        /// </summary>
        private static bool CreateTaskUsingSchTasks(
            string taskName,
            string executablePath,
            string arguments,
            out string errorMessage,
            bool debugMode = false)
        {
            errorMessage = string.Empty;
            string xmlPath = "";
            
            try
            {
                // Eliminar tarea existente si existe
                var deleteResult = ProcessExecutor.Execute("schtasks", $"/Delete /TN \"{taskName}\" /F");
                // Ignorar errores de delete (puede no existir)
                
                // Crear archivo XML temporal para la tarea
                // Este método es mucho más robusto que usar parámetros en línea de comandos
                xmlPath = Path.Combine(Path.GetTempPath(), $"{taskName}_{Guid.NewGuid()}.xml");
                
                // Crear tarea simple que se ejecute al inicio de sesión de CUALQUIER usuario
                // UserId con GroupId "Users" hace que se ejecute para cualquier usuario que inicie sesión
                string taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Micromanager - Sistema de monitoreo</Description>
    <Author>Micromanager</Author>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <GroupId>S-1-5-32-545</GroupId>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>5</Priority>
  </Settings>
  <Actions>
    <Exec>
      <Command>{executablePath}</Command>
      <Arguments>{System.Security.SecurityElement.Escape(arguments)}</Arguments>
    </Exec>
  </Actions>
</Task>";
                
                File.WriteAllText(xmlPath, taskXml, System.Text.Encoding.Unicode);
                
                if (debugMode)
                {
                    Console.WriteLine($"[DEBUG] Archivo XML de tarea creado: {xmlPath}");
                    Console.WriteLine($"[DEBUG] Comando: schtasks /Create /TN \"{taskName}\" /XML \"{xmlPath}\" /F");
                }
                
                // Crear tarea desde XML
                var createResult = ProcessExecutor.Execute("schtasks", $"/Create /TN \"{taskName}\" /XML \"{xmlPath}\" /F");
                
                // Limpiar archivo XML temporal
                try { File.Delete(xmlPath); } catch { }
                
                if (createResult.Success || createResult.Output.Contains("creada correctamente", StringComparison.OrdinalIgnoreCase))
                {
                    if (debugMode)
                    {
                        Console.WriteLine($"✓ Tarea programada '{taskName}' creada exitosamente (schtasks XML)");
                        Console.WriteLine($"  Se ejecutará al iniciar sesión de CUALQUIER usuario");
                        Console.WriteLine($"  Se ejecutará como: El usuario que inicia sesión (InteractiveToken)");
                        Console.WriteLine($"  RunLevel: LeastPrivilege (sin privilegios elevados)");
                        Console.WriteLine($"  Ejecutable: {executablePath}");
                        Console.WriteLine($"  Argumentos: {arguments}");
                    }
                    return true;
                }
                else
                {
                    errorMessage = $"Error al crear tarea: {createResult.Error}";
                    if (debugMode)
                    {
                        Console.WriteLine($"[DEBUG] Error output: {createResult.Output}");
                        Console.WriteLine($"[DEBUG] Error detail: {createResult.Error}");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error al crear tarea con schtasks XML: {ex.Message}";
                // Intentar limpiar archivo XML si existe
                try { if (!string.IsNullOrEmpty(xmlPath) && File.Exists(xmlPath)) File.Delete(xmlPath); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Crea o actualiza una tarea programada que se ejecuta al iniciar sesión
        /// </summary>
    public static bool CreateOrUpdateTask(
        string taskName,
        string executablePath,
        string arguments,
        out string errorMessage,
        bool debugMode = false)
    {
        errorMessage = string.Empty;
        
        // SIEMPRE usar schtasks.exe con XML para máxima compatibilidad
        // La biblioteca TaskScheduler tiene problemas con tareas sin UserId/GroupId especificado
        if (debugMode) Console.WriteLine("[DEBUG] Usando schtasks.exe con XML para máxima compatibilidad");
        return CreateTaskUsingSchTasks(taskName, executablePath, arguments, out errorMessage, debugMode);
    }
        
        /// <summary>
        /// Elimina una tarea programada
        /// </summary>
        public static bool DeleteTask(string taskName, out string errorMessage, bool debugMode = false)
        {
            errorMessage = string.Empty;
            
            // Si está ejecutando como SYSTEM, usar schtasks.exe
            if (IsSystemAccount())
            {
                try
                {
                    var result = ProcessExecutor.Execute("schtasks", $"/Delete /TN \"{taskName}\" /F");
                    if (result.Success || result.Output.Contains("no se encuentra", StringComparison.OrdinalIgnoreCase))
                    {
                        if (debugMode) Console.WriteLine($"✓ Tarea programada '{taskName}' eliminada (schtasks.exe)");
                        return true;
                    }
                    else
                    {
                        errorMessage = $"Error al eliminar tarea: {result.Error}";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"Error al eliminar tarea: {ex.Message}";
                    return false;
                }
            }
            
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    var task = taskService.GetTask(taskName);
                    if (task != null)
                    {
                        taskService.RootFolder.DeleteTask(taskName, false);
                        if (debugMode) Console.WriteLine($"✓ Tarea programada '{taskName}' eliminada");
                        return true;
                    }
                    else
                    {
                        if (debugMode) Console.WriteLine($"ℹ Tarea '{taskName}' no existe");
                        return true;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = Configuration.Messages.RequiresAdmin;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error al eliminar tarea: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Detiene la ejecución de una tarea programada
        /// </summary>
        public static bool StopTask(string taskName, out string errorMessage, bool debugMode = false)
        {
            errorMessage = string.Empty;
            
            // Si está ejecutando como SYSTEM, usar schtasks.exe
            if (IsSystemAccount())
            {
                try
                {
                    var result = ProcessExecutor.Execute("schtasks", $"/End /TN \"{taskName}\"");
                    if (result.Success || result.Output.Contains("no se está ejecutando", StringComparison.OrdinalIgnoreCase))
                    {
                        if (debugMode) Console.WriteLine($"✓ Tarea '{taskName}' detenida (schtasks.exe)");
                        return true;
                    }
                    else
                    {
                        if (debugMode) Console.WriteLine($"ℹ Tarea '{taskName}' no está en ejecución");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"Error al detener tarea: {ex.Message}";
                    return false;
                }
            }
            
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    var task = taskService.GetTask(taskName);
                    if (task != null && task.State == TaskState.Running)
                    {
                        task.Stop();
                        if (debugMode) Console.WriteLine($"✓ Tarea '{taskName}' detenida");
                        return true;
                    }
                    else
                    {
                        if (debugMode) Console.WriteLine($"ℹ Tarea '{taskName}' no está en ejecución");
                        return true;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = Configuration.Messages.RequiresAdmin;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error al detener tarea: {ex.Message}";
                return false;
            }
        }
        
        /// <summary>
        /// Verifica si una tarea existe
        /// </summary>
        public static bool TaskExists(string taskName)
        {
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    return taskService.GetTask(taskName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

