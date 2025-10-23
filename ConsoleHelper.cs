using System;
using System.Security;

namespace Micromanager
{
    /// <summary>
    /// Utilidades para interacción con la consola
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// Lee una contraseña de la consola de forma segura (mostrando asteriscos)
        /// </summary>
        public static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;
            
            do
            {
                key = Console.ReadKey(true);
                
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);
            
            return password;
        }
        
        /// <summary>
        /// Solicita y confirma una contraseña al usuario
        /// </summary>
        public static string PromptForPassword(string username)
        {
            try
            {
                Console.WriteLine($"\n--- Configuración de Usuario para Carpeta Compartida ---");
                Console.WriteLine($"Usuario: {username}");
                Console.WriteLine($"");
                Console.Write("Ingrese contraseña para el usuario de carpeta compartida: ");
                
                string password = ReadPassword();
                Console.WriteLine();
                
                if (string.IsNullOrEmpty(password))
                {
                    Console.WriteLine(Configuration.Messages.EmptyPasswordNotAllowed);
                    return "";
                }
                
                Console.Write("Confirme la contraseña: ");
                string confirmPassword = ReadPassword();
                Console.WriteLine();
                
                if (password != confirmPassword)
                {
                    Console.WriteLine(Configuration.Messages.PasswordMismatch);
                    return "";
                }
                
                return password;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error al leer contraseña: {ex.Message}");
                return "";
            }
        }
    }
}

