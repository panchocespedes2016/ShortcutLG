# ShortcutLG

Aplicación de bandeja para Windows que cambia la entrada HDMI de una TV LG con webOS mediante atajos de teclado globales.

C# WinForms sobre .NET Framework 4.8, sin dependencias externas ni instalador obligatorio. Se compila con `csc.exe`, incluido en el propio Framework. El control se hace por red local: descubrimiento SSDP y WebSocket en el puerto 3000, con emparejamiento en la TV.

## Versión base

Versión 1.0.0.0, 31 de octubre de 2026. Cambio de HDMI 1–4 verificado en una TV LG con webOS 3.4.3. El manifiesto de emparejamiento es el mismo que el del script `lgtv.py` que controla esa TV desde septiembre de 2026: conexión `ws` sin cifrar al puerto 3000, sin bloque firmado. La clave de autorización queda ligada al manifiesto con el que se emparejó; si se cambia el manifiesto, hay que emparejar de nuevo (ver `clear-key.ps1`).

## Versión 1.1.0.0

Añade al menú y a los atajos: volumen +/−, apagar (pide confirmación), Inicio, Aceptar (OK), flechas y Exit. Volumen, apagar e Inicio van por órdenes SSAP sobre el mismo canal; OK, flechas y Exit van como botones del mando por el socket de entrada (`getPointerInputSocket`, protocolo de texto `type:button`).

## Versión 1.1.1

El instalador muestra la versión (la instalada y la nueva) y permite desinstalar desde el propio instalador, con la opción de borrar también la configuración y la autorización de la TV. Registro parcial de atajos: si un atajo ya lo usa otra aplicación, los demás siguen funcionando.

## Uso

1. Ejecuta `ShortcutLG.exe`. Aparece un icono junto al reloj.
2. La primera vez se abre automáticamente «Configurar TV LG». Pulsa «Buscar TV» o escribe la IP de la TV y luego «Emparejar TV».
3. Acepta el aviso en la pantalla de la TV con el mando.
4. Cambia de entrada con el menú del icono o con los atajos.

Atajos por defecto:

| Atajo | Acción |
|---|---|
| Win + Alt + Shift + 1 | HDMI 1 |
| Win + Alt + Shift + 2 | HDMI 2 |
| Win + Alt + Shift + 3 | HDMI 3 |
| Win + Alt + Shift + 4 | HDMI 4 |

Las combinaciones se pueden cambiar en el menú del icono («Cambiar combinaciones de teclas...»). Windows reserva Win+Alt+1..4 para la barra de tareas, por eso los atajos por defecto incluyen Shift.

## Compilación

En Windows, desde la carpeta del proyecto:

```bat
build.bat         :: solo ShortcutLG.exe
build-setup.bat   :: ShortcutLG.exe + ShortcutLG-Setup.exe (instalador)
```

El instalador instala la aplicación para el usuario actual en `%LOCALAPPDATA%\Programs\ShortcutLG`, sin permisos de administrador, y conserva la configuración al actualizar. La versión portable (`ShortcutLG.exe`) no necesita instalación. Ver `LEEME.txt` para las instrucciones completas en español.

## Configuración y datos

Carpeta: `%LOCALAPPDATA%\ShortcutLG`

- `tray.ini`: IP de la TV, autorización y atajos. **No lo compartas**: contiene la clave de autorización de tu TV. Nunca se sube a este repositorio.
- `tray.log`: registro de errores y cambios.

`clear-key.ps1` vacía la clave guardada en `tray.ini` (con copia de seguridad) para forzar un emparejamiento nuevo.

## Requisitos

- Windows 10 u 11 (también Windows 7/8.1 con .NET Framework 4.8).
- TV LG con webOS encendida y en la misma red local que el PC.
- Si el modelo tiene la opción «LG Connect Apps» o «Conexión móvil», debe estar activada.

## Licencia

MIT. Ver `LICENSE`.
