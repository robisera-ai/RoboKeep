# RoboKeep

*Leer en: [English](README.md) · [Italiano](readmeita.md) · Español · [Français](readmefr.md) · [Deutsch](readmede.md)*

**Configúralo una vez. Cada archivo, cada versión, a salvo en tu propio disco.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Idiomas](https://img.shields.io/badge/idiomas-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Descargas](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep es una aplicación de copia de seguridad para Windows construida sobre `robocopy`, el
motor de copia que ya viene incluido en todos los PC. Responde unas preguntas y mantendrá tus
carpetas reflejadas en un disco externo, con versiones fechadas a las que puedes volver. **Sin
nube, sin cuenta, sin suscripción.**

![Ventana principal de RoboKeep: tareas con resultados de un vistazo y registro de ejecución en vivo](docs/images/main-window.png)

## Por qué RoboKeep

| | |
|---|---|
| 🧙 **Configuración sencilla** | El asistente pregunta sobre tus datos en lenguaje sencillo y elige los ajustes adecuados. Quien lo prefiera puede ajustarlo todo a mano. |
| 🕰️ **Vuelve atrás en el tiempo** | Cada ejecución puede guardar una versión fechada. Los archivos sin cambios se comparten entre versiones, así que diez versiones no cuestan diez veces el espacio. |
| 💿 **Cuida tus discos** | Se detiene al primer error de hardware en vez de insistir durante horas, mantiene el PC despierto durante la copia y va con cuidado en los discos mecánicos. |
| 🩺 **Salud de discos de un vistazo** | Un clic lee el SMART de cada disco y da un veredicto claro: Bien, Precaución, Peligro — con cada valor explicado. *Novedad en 1.8.* |
| 🛡️ **Nunca el disco equivocado** | ¿Alternas dos discos externos que Windows llama a ambos `E:`? Cada tarea reconoce su disco por identidad y simplemente espera a que esté conectado. |
| ✅ **Prueba de que funcionó** | Verificar relee ambos lados y compara las huellas SHA-256. La corrupción silenciosa queda al descubierto. |
| 🔓 **Copia también archivos abiertos** | Archivos de Outlook, bases de datos, cualquier cosa bloqueada: una casilla copia desde una instantánea de Windows. |
| ⏰ **Se ejecuta solo** | Cada tarea tiene su propio horario (diario, semanal, mensual, incluso el último día del mes) y se ejecuta con la app cerrada. |
| 🚨 **Avisa solo cuando importa** | Iconos de color y explicaciones claras para tareas fallidas, atrasadas o interrumpidas. Un disco desconectado es un reloj de arena, no una alarma. |
| 🏠 **Realmente tuyo** | Gratis y de código abierto (MIT), totalmente local, sin telemetría. Cinco idiomas. Portátil si quieres. |

## Empieza en dos minutos

1. Descarga la última versión desde **[Releases](../../releases)**:
   - **`…-selfcontained.zip`**: descomprime y ejecuta, nada que instalar (descarga más grande);
   - **`…-framework-dependent.zip`**: mucho más pequeño, necesita el
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) gratuito.
2. Descomprímelo en una **carpeta con permisos de escritura**, por ejemplo `D:\Programas\RoboKeep` (no en `C:\Program Files`).
3. Ejecuta **`RoboKeep.exe`** → **Nuevo** → responde al asistente → **Ejecutar todos**. Listo.

> **¿Windows SmartScreen te avisa?** RoboKeep todavía no tiene firma digital. Haz clic en *Más
> información* → *Ejecutar de todas formas*, o clic derecho en el zip → Propiedades →
> **Desbloquear** antes de descomprimir.

## Míralo en acción

| | |
|---|---|
| ![Configuración guiada](docs/images/wizard.png) | **Configuración guiada.** Unas preguntas, incluida si los archivos siguen abiertos mientras trabajas, y la tarea queda configurada por ti. |
| ![Editor de tareas](docs/images/editor.png) | **Todo bajo control.** Espejo o acumular, copia de archivos abiertos, opciones para archivos grandes, cada una explicada en una línea. |
| ![Vista previa del comando y versiones](docs/images/editor-preview.png) | **Nada oculto.** Versiones fechadas, exclusiones por tarea y el comando robocopy exacto siempre a la vista. |
| ![Historial de ejecuciones](docs/images/history.png) | **Cada ejecución queda registrada.** Copias y verificaciones de integridad una al lado de la otra; doble clic abre el registro completo. |
| ![Programación por tarea](docs/images/editor-schedule.png) | **Configúralo y olvídate.** Diaria, semanal o mensual, más verificaciones de integridad periódicas. |
| ![Explorar versiones](docs/images/versions.png) | **Elige una fecha.** La copia de ese día se abre en el Explorador de archivos; recupera lo que necesites. |

## Cómo se comporta una copia de seguridad

Cada tarea es un par carpeta origen → carpeta destino. En cada ejecución, RoboKeep:

- **omite** lo que no ha cambiado (la segunda ejecución dura segundos);
- **actualiza** lo que es más reciente en el origen;
- en modo **espejo** (predeterminado) también **elimina** del destino lo que borraste;
- con el espejo desactivado, solo añade y actualiza, **nunca elimina**.

Con las versiones activadas, el estado anterior se guarda primero como una instantánea fechada.

## Novedades de la 1.8

- **Ventana de salud de discos**: SMART de cada disco, explicado en lenguaje sencillo. NVMe sin
  avisos; SATA y USB con una confirmación de administrador, la única forma de atravesar las cajas
  USB.
- **Comprobación de actualizaciones opcional**: RoboKeep pregunta una vez si puede buscar nuevas
  versiones en GitHub. Cuando hay una, un banner ofrece *Novedades*, *Descargar*, *Ignorar*. Tú
  reemplazas los archivos.
- **Asistente más simple**: pregunta solo lo que importa, guarda la tarea en cuanto terminas y
  gestiona las credenciales de red por su cuenta.
- Activar las versiones en una tarea existente **ya no recopia todo**; una tarea cancelada sigue
  dejando un registro; las programaciones mensuales pueden ejecutarse el **último día del mes**.

Historial completo en el [CHANGELOG](CHANGELOG.md).

<details>
<summary><b>Todas las funciones</b></summary>

**Copia de seguridad**: modo espejo o acumular · protección de rotación de discos (la tarea se
vincula a su disco por identidad de volumen) · versiones fechadas con enlaces duros y retención
configurable, sin versión duplicada cuando no ha cambiado nada · copia de archivos
abiertos/bloqueados mediante VSS · verificación de integridad SHA-256, a demanda o periódica ·
parada por error de hardware que deja el disco en reposo · límite automático de hilos en discos
mecánicos · PC mantenido despierto · copia multihilo · exclusiones por tarea · "forzar copia"
para archivos cuya fecha/tamaño nunca cambian, con un modo opcional de hash de contenido · modo
reanudable para archivos enormes · vista previa / ejecución en seco.

**Te mantiene informado**: historial de ejecuciones con un registro por cada entrada, abierto en
el Bloc de notas · botón Carpeta de registros · iconos de salud por tarea con explicaciones
claras · iconos que se actualizan en el instante en que conectas o desconectas un disco ·
comprobaciones previas (destino accesible, espacio en disco, idoneidad de VSS y de versiones,
errores de disco recientes del registro de eventos de Windows) · ventana de salud de discos
(SMART) · registro en tiempo real · archivo de registros comprimidos con limpieza automática ·
notificaciones toast y bandeja del sistema · informes por correo (SMTP), opcionalmente solo en
caso de errores.

**Se adapta a tu configuración**: asistente o editor manual completo · vista previa exacta del
comando · programación por tarea (diaria / semanal / mensual / último día del mes) · recursos de
red con credenciales cifradas con DPAPI · limitación de velocidad de copia para copias en red ·
exportación/importación de configuración · línea de comandos para automatización · reordenar
tareas arrastrando · comprobación de actualizaciones opcional · 5 idiomas · modo portátil.

</details>

<details>
<summary><b>Bueno saberlo</b></summary>

- **Solo Windows 10/11.** RoboKeep se apoya en robocopy y otras funciones nativas de Windows.
- **Los archivos cambiados se recopian enteros** (sin copia por bloques/delta): perfecto para
  documentos y fotos, costoso para archivos enormes que cambian a diario.
- **Las versiones necesitan un destino NTFS local** (los enlaces duros no existen en exFAT ni en
  recursos de red).
- **La copia de archivos abiertos necesita un origen NTFS local** y una confirmación de
  administrador (UAC) por ejecución.
- **Las verificaciones de integridad releen cada archivo en ambos lados**, así que tardan casi
  tanto como una primera copia. Por eso la verificación automática se ejecuta cada 7 días por
  defecto (0 = después de cada ejecución).
- **Un error de hardware detiene la tarea a propósito.** Antes de volver a ejecutarla, comprueba
  el cable, la caja USB y la alimentación (en discos externos causan exactamente los mismos
  errores que un disco que falla), y luego abre **Salud de discos**.
- Los ajustes, resultados y registros viven en `%APPDATA%\RoboKeep` y sobreviven a las
  actualizaciones. Las contraseñas se cifran con DPAPI de Windows; el ámbito predeterminado es a
  nivel de máquina para que las tareas programadas puedan leerlas. En un PC compartido, cambia al
  ámbito por usuario.

</details>

<details>
<summary><b>Para usuarios avanzados</b></summary>

```text
RoboKeep.exe --run-all              ejecuta todas las tareas habilitadas (código de salida 0 = todo bien)
RoboKeep.exe --job "Documentos"     ejecuta una sola tarea
RoboKeep.exe --run-all --dry-run    solo vista previa, no cambia nada
RoboKeep.exe --job "Fotos" --config "D:\ruta\config.json"
```

Compilar desde el código: `dotnet build src/RoboKeep.sln -c Release` (SDK de .NET 10); pruebas:
`dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`. Modo portátil: un archivo vacío
`portable.flag` junto al exe mantiene todo dentro de la carpeta de la app. Configuración de
ejemplo: [config/config.example.json](config/config.example.json). Contexto del proyecto y
decisiones técnicas: [ANALISI.md](ANALISI.md) *(en italiano)*.

</details>

## Privacidad

RoboKeep no recopila **nada**. Sin telemetría, sin analíticas, sin cuenta. El único tráfico de
red es el que tú mismo habilitas: la comprobación de actualizaciones opcional (una petición a
GitHub) y los informes por correo (tu propio servidor SMTP, con TLS por defecto). Todo lo que
sabe vive en archivos JSON legibles en tu PC.

## Contribuir

Las traducciones las mantiene la comunidad: ¿has visto una frase que un hablante nativo diría de
otra forma? [Se agradecen las pull requests](../../pulls), incluso una corrección de una línea.

## Licencia

**Licencia MIT**, consulta [LICENSE](LICENSE). Componentes de terceros en
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). © 2026 Roberto Serafini.
