# Culto Studio: plan de adaptación visual

> **Para la ejecución:** usar la habilidad `executing-plans` y completar las tareas en orden. Estado: propuesta preparada; implementación pendiente.

**Objetivo:** trasladar los colores, la tipografía y las formas de la referencia a la consola WPF de Culto Studio, conservando las funciones y los datos actuales.

**Arquitectura:** reutilizar los recursos y las plantillas existentes en `App.xaml`, y adaptar las superficies de `MainWindow.xaml`. Mantener AvalonDock, los manejadores de eventos, los identificadores de paneles y los motores actuales. No incorporar un framework visual nuevo.

**Tecnologías verificadas:** .NET 10, WPF, AvalonDock 5.0.1; aplicación 0.28.3.

**Especificación:** las secciones «Dirección visual» y «Contrato de conservación» de este documento constituyen la propuesta de diseño para este trabajo.

**Referencia:** [prototipo Polymet](https://dev-untitled-pro-d156mu4f.polymet.app/), revisado el 12 de noviembre de 2026 mediante capturas y estilos calculados del navegador, en modo oscuro.

**Recuperación:** `c13da65da67e3d5e4c772c7251333220567e258e` (`c13da65`, checkpoint de Culto Studio 0.28.3). Al preparar este plan, `HEAD` coincide con ese commit y el árbol estaba limpio.

## Dirección visual

La referencia tiene superficies gris carbón, verde esmeralda, tipografía Lexend, paneles con esquinas amplias, botones tipo cápsula e iconos de trazo fino. La separación se consigue principalmente con el fondo, el espacio y el tamaño; las sombras son casi inexistentes en modo oscuro.

### Enfoque elegido

Aplicar una adaptación visual a la estructura actual. Es la opción recomendada porque permite reconocer el diseño de referencia sin alterar el manejo de la consola.

Se descartan para este alcance dos alternativas: limitarse a cambiar colores dejaría Poppins y varias superficies violetas; reproducir las seis columnas del prototipo exigiría reorganizar los paneles y reduciría el espacio disponible. En el navegador observado, de 1094 × 884 píxeles, las columnas de la referencia requieren desplazamiento horizontal.

### Colores medidos y aplicación propuesta

Los valores de referencia son los RGB calculados por el navegador, expresados en hexadecimal. Los ajustes de accesibilidad son decisiones propuestas para la aplicación.

| Recurso WPF existente o propuesto | Referencia | Aplicación en Culto Studio |
|---|---|---|
| `CanvasBrush` | `#1F2023` | Fondo general y superficies exteriores |
| `PanelBrush` | `#2A2B2F` | Paneles, tarjetas y diálogos |
| `RaisedBrush` | `#3A3B40` | Controles secundarios y superficies elevadas |
| `InputBrush` | `#34353A` | Campos y contenedores de búsqueda |
| `HoverBrush` | `#34353A` | Respuesta al puntero sobre filas sin seleccionar |
| `SelectedBrush` | `#3A3B40` | Selección preparada; borde verde, sin relleno de directo |
| `PanelBorderBrush` | `#35363B` | Separadores decorativos y bordes sutiles de panel |
| `BorderBrush` | `#35363B` | Proponer `#858893` en límites necesarios para identificar campos y controles |
| `TextBrush` | `#F5F5F5` | Texto principal |
| `MutedBrush` | `#999BA3` | Proponer `#B1B3BB` para conservar legibilidad también sobre controles elevados |
| `AccentBrush` / `ActionBrush` | `#24B27E` | Acciones principales, pestañas activas y detalles de marca |
| `OnActionBrush` — nuevo | Blanco en el prototipo | Proponer `#1F2023` para texto sobre verde |
| `LiveBrush` | Verde de la referencia | Proponer `#24B27E` para borde, indicador y fila en vivo |
| `AmberBrush` | Sin equivalencia necesaria | Conservar el ámbar actual `#E7BE90` para foco y avisos existentes |

Comprobación local de contraste con luminancia sRGB: blanco `#FFFFFF` sobre verde `#24B27E` da **2,71:1**; carbón `#1F2023` sobre ese verde da **6,01:1**. El gris de referencia sobre `#3A3B40` da **4,03:1**; el gris propuesto `#B1B3BB` da **5,34:1**. Por ello, no copiar el blanco sobre verde ni el gris pequeño sobre superficies elevadas. Exigir al menos 4,5:1 para texto normal y 3:1 para indicadores y límites necesarios para identificar controles.

El borde `#35363B` sirve para decoración, no como único indicador de un campo. El borde interactivo propuesto `#858893` alcanza 3,16:1 sobre `#3A3B40` y 3,46:1 sobre `#34353A`. Conservar un anillo de foco visible. No reducir la opacidad de un contenedor que incluya texto necesario para operar.

### Tipografía

- Sustituir la fuente de interfaz Poppins por **Lexend**, con caras estáticas de pesos 400, 500, 600 y 700, empaquetadas para funcionar sin internet.
- Mantener un cuerpo de interfaz de **13–14 DIP**, encabezados de panel de **15 DIP / Medium**, títulos principales de **24–26 DIP / Regular** cuando ya exista ese nivel, y metadatos de **11–12 DIP**. Conservar los campos grandes de Biblia y búsqueda, de 16–17 DIP, y los versículos de la lista, de 15 DIP.
- Mantener `Segoe MDL2 Assets` para glifos de controles y `Segoe UI` con cifras tabulares en tiempos. No aplicar un reemplazo indiscriminado de `FontFamily`.
- La fuente elegida por el usuario para los versículos proyectados y los estilos de texto de salida conservan su configuración. Cambiar `ConsoleFont` afecta a la interfaz, no al contenido del culto.
- Lexend puede ensanchar etiquetas respecto a Poppins: comprobar títulos largos, nombres de libros, botones, comboboxes y ventanas flotantes antes de reducir tamaños.

La distribución oficial consultada contiene una fuente variable `Lexend[wght].ttf`. Para este plan se prepararán cuatro instancias estáticas y se comprobará su resolución real en WPF; no se dará por compatible una fuente variable por el aspecto del navegador. [Origen y metadatos](https://github.com/google/fonts/tree/main/ofl/lexend), [empaquetado de fuentes en WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/packaging-fonts-with-applications).

### Formas y espaciado

| Elemento | Referencia observada | Adaptación propuesta |
|---|---|---|
| Panel exterior | Radio 22 px | Recurso `PanelRadius = 22` DIP; si falta altura útil, reducir padding antes de reducir controles |
| Miniaturas y tarjetas | Radio hasta 24 px | Radio 16 DIP para miniaturas; 12–16 DIP para filas seleccionadas |
| Botones y filtros | Cápsula | Reutilizar `PillRadiusConverter`, que ya calcula la mitad de la altura |
| Buscadores de una línea | Cápsula | Radio según altura; altura 36–42 DIP según el control actual |
| Editor multilínea | No gobierna la referencia | Radio 12 DIP; conservar scroll, selección y salto de línea |
| Iconos de transporte | Círculos compactos | Conservar controles actuales de 32 y 40 DIP y sus nombres accesibles |
| Separación entre paneles | 12 px | Adaptar márgenes internos sin alterar los separadores de AvalonDock |
| Padding de panel | Aproximadamente 14 px en encabezados | Escala de 12, 14 y 16 DIP según espacio disponible |

No recortar el video para obtener esquinas redondas: redondear su marco exterior y conservar el viewport completo de proporción 16:9. WPF y las superficies nativas/DWM requieren comprobar el resultado; `Border.CornerRadius` por sí solo no garantiza el recorte de un hijo.

### Estados

- **Normal:** superficie carbón, texto claro y metadatos legibles.
- **Hover:** cambio leve de superficie, aprovechando `UiMotion` existente.
- **Preparado/seleccionado:** superficie elevada con borde verde y etiqueta de preparado; no representa la salida actual.
- **En vivo:** borde verde del monitor y distintivo textual; fila del orden verde con texto carbón. Mantener una señal adicional al color.
- **Foco de teclado:** anillo ámbar visible, independiente de selección y directo.
- **Deshabilitado:** conservar la semántica y la respuesta actual; comprobar que no parezca una acción disponible.
- **LOCAL, HDMI, NEGRO, FONDO y errores:** conservar las etiquetas reales y sus condiciones actuales.

## Contrato de conservación

1. Un clic, una búsqueda y un cambio de pestaña siguen preparando contenido sin sustituir la salida. Doble clic, Enter y F5 conservan sus acciones y excepciones actuales.
2. F6/F7, F8, F9, Ctrl+S, Ctrl+F, Ctrl+1–4 y el presentador USB conservan su alcance. No añadir los atajos 1–9 de escenas mostrados en el prototipo.
3. Conservar `ContentId`: `preview`, `output`, `order`, `sources`, `sequence` y `media`; preservar `paneles.xml`, tamaños mínimos, arrastre, acoplamiento y ventanas flotantes.
4. Mantener biblioteca, filtros, carpetas locales, medios en lista/cuadrícula, ordenación, importación, colecciones, cultos guardados, notas y edición de texto.
5. Mantener búsqueda bíblica, selectores editables, rangos, multiselección, incorporación agrupada o separada y editor de estilo.
6. Mantener controles de archivo multimedia: pausa, posición, saltos, reinicio, silencio, volumen, repetición y reproductor flotante. La captura YouTube conserva sus controles específicos, sin introducir pausa falsa.
7. Mantener YouTube/captura de ventanas, recorte, transformación, recuperación de fuente, audio conservado y regreso con F9. No añadir descarga de YouTube ni reproducción por URL como consecuencia del aspecto del prototipo.
8. Mantener salida local/HDMI, reflejo de monitor, PowerPoint nativo, transiciones y motor Move. No cambiar calentamiento de VLC ni la lógica del primer cambio de captura a archivo de 0.28.3.
9. Mantener Logo, Fondo negro, retorno al contenido y mando móvil con sus configuraciones actuales. El mando del teléfono queda fuera del rediseño visual.
10. Mantener formatos y ubicación de los datos. No sobrescribir bibliotecas, Biblia local, orden, preferencias ni historial con los ejemplos del prototipo.

No se incorporan como parte de este trabajo un modo claro, escenas nuevas, notificaciones, métricas de GPU ni cambios de arquitectura. El objetivo son los colores, la tipografía y las formas de las funciones existentes.

## Archivos y responsabilidades

Las rutas siguientes son relativas a la raíz del repositorio.

| Archivo | Cambio previsto |
|---|---|
| `src/Culto.Desktop/App.xaml` | Paleta compartida, fuente, radios, estados y plantillas de controles |
| `src/Culto.Desktop/MainWindow.xaml` | Superficies, filas, filtros, marcos y colores locales que hoy son violetas |
| `src/Culto.Desktop/MainWindow.Docking.cs` | Solo recursos cromáticos de AvalonDock, si los recursos actuales no cubren su chrome |
| `src/Culto.Desktop/MainWindow.Layout.cs` | Colores de la barra de título nativa; conservar sus controles de Windows |
| `src/Culto.Desktop/Assets/Fonts/Lexend-{Regular,Medium,SemiBold,Bold}.ttf` | Cuatro caras estáticas para la consola |
| `src/Culto.Desktop/Assets/Licenses/Lexend-OFL.txt` | Licencia distribuida con la fuente |
| `src/Culto.Desktop/Culto.Desktop.csproj` | Comprobar inclusión de recursos; los comodines actuales ya incluyen TTF y licencias TXT |
| `src/Culto.Desktop/CueThumbnail.cs` y `MediaThumbnail.cs` | Solo acento de portadas/iconos de interfaz todavía codificado en violeta |
| `tests/Culto.Desktop.Smoke/ThemeTests.cs` | Adaptar prueba de fuente y ampliar comprobaciones de legibilidad/encaje necesarias |
| `THIRD-PARTY-NOTICES.md` | Incorporar procedencia y licencia de Lexend |
| `docs/validacion.md` | Registrar resultados nuevos y límites comprobados |

Los diálogos heredan los estilos compartidos. Revisar visualmente `Dialogs/BibleStyleDialog.cs`, `SavedSetsDialog.cs`, `LogoSettingsDialog.cs`, `TransitionDialog.cs`, `WindowCaptureDialog.cs`, `RemoteDialog.cs` y `OutputPlayerWindow.cs`; editarlos solo si un valor local impide heredar el aspecto o produce recortes. Los colores editables de los botones del mando y los de la Biblia son datos del usuario, no restos del tema.

No modificar `Culto.Core`, `Culto.Remote`, `Projection/*` ni los servicios de reproducción para conseguir el cambio visual. Si una comprobación descubre una dependencia real, documentar y revisar esa ampliación antes de implementarla.

## Tarea 1: fijar la comparación y preparar la recuperación

- [ ] Confirmar de nuevo `git rev-parse HEAD`, `git status --short` y `git show --no-patch c13da65`. Preservar cualquier cambio aparecido después de este plan.
- [ ] Preparar el trabajo de implementación en una rama `codex/culto-studio-adaptacion-visual`, con el flujo de aislamiento del repositorio. No mover ni reescribir el checkpoint.
- [ ] Conservar `artifacts/CultoStudio-0.28.3` y los iniciadores actuales. Los datos de `%LOCALAPPDATA%/CultoStudio` son una recuperación separada de Git; respaldarlos antes de ensayar con un espacio de trabajo real, con el programa cerrado.
- [ ] Capturar la consola base a 1480 × 900 y 1280 × 680 DIP con datos de prueba, más una distribución acoplada personalizada. Registrar selección preparada distinta del elemento en vivo.
- [ ] Ejecutar `./scripts/build.ps1` y guardar sus resultados como línea base, sin confundirlos con los resultados históricos de `docs/validacion.md`.

**Resultado revisable:** punto de recuperación identificado, capturas comparables y estado de compilación inicial documentado. No se altera la aplicación de uso diario.

## Tarea 2: incorporar Lexend sin dependencia de internet

- [ ] Obtener `Lexend[wght].ttf` y su OFL del origen oficial enlazado; registrar revisión de origen y SHA-256 de entrada y de los cuatro archivos finales.
- [ ] Preparar TTF estáticas de pesos 400, 500, 600 y 700 con nombres internos coherentes. Si se usa FontTools para instanciarlas, es una herramienta de preparación y no una dependencia de ejecución del producto. Conservar la licencia y la atribución del original.
- [ ] Actualizar primero la prueba existente «Console font is bundled and resolves all four weights offline» en `ThemeTests.cs`: debe exigir Lexend, URI `pack`, y el peso real correspondiente, sin síntesis. Ejecutarla contra Poppins y comprobar que detecta el cambio pendiente.
- [ ] Añadir los cuatro archivos y `Assets/Licenses/Lexend-OFL.txt`; cambiar únicamente el recurso de interfaz:

```xml
<FontFamily x:Key="ConsoleFont">/CultoStudio;component/Assets/Fonts/#Lexend</FontFamily>
```

- [ ] Reejecutar el ensayo de escritorio, comprobar las cuatro caras y revisar «Génesis», «1 Corintios», «¿Quién irá?», cifras y acentos sin acceso a internet. No cambiar la fuente configurada en el estilo bíblico.
- [ ] Actualizar los avisos de terceros y guardar este cambio como un commit independiente.

**Resultado revisable:** Lexend funciona empaquetada y la prueba detecta una sustitución accidental por fuente del sistema.

## Tarea 3: aplicar la paleta y los estados compartidos

- [ ] Cambiar los recursos de `App.xaml` según la tabla; incorporar `OnActionBrush` y sustituir el blanco fijo de `PrimaryButton` por ese recurso. Reutilizar las claves actuales para evitar cambiar sus consumidores.
- [ ] Revisar `TextBox`, `ComboBox`, `ComboBoxItem`, `ListBoxItem`, `MenuItem`, `TabItem`, `ScrollBar` y `Slider`: sustituir los colores violetas locales, conservando triggers, comandos, bindings y estados deshabilitados.
- [ ] Mantener `PART_ContentHost`, `PART_EditableTextBox`, `PART_Popup` y `PART_Track`. No sustituir controles nativos por elementos decorativos sin su comportamiento.
- [ ] Mantener el foco ámbar. Verificar selección de texto, caret, desplegables editables, scroll y ajuste de volumen por teclado.
- [ ] Mantener el color seleccionado de una cabecera de pestaña dentro de su cabecera; comprobar que no se hereda por los textos de Biblia ni otros contenidos.
- [ ] Compilar y ejecutar el ensayo de escritorio general, que ya incluye `RunThemeTests`, y revisar los contrastes de texto y de límites interactivos renderizados. Guardar el cambio en un commit de tema.

**Resultado revisable:** consola y diálogos comparten la paleta sin perder interacción nativa ni foco.

## Tarea 4: trasladar las formas a los paneles existentes

- [ ] Centralizar radios reutilizables en `App.xaml` y aplicarlos a `Card` y a las superficies `PreviewPanel`, `LivePanel`, `OrderPanel`, `SourcesPanel`, `MediaPanel` y el panel de Secuencia en `MainWindow.xaml`.
- [ ] Conservar las ubicaciones iniciales de los paneles, sus `ContentId`, nombres `x:Name`, `AutomationProperties` y todos los manejadores actuales. Mantener las cuatro pestañas Himnario, Biblia, Texto y YouTube.
- [ ] Adaptar `OrderRow`, `FilterChip`, `SequenceItem` y los templates de medios. Diferenciar claramente preparado y en vivo, incluso cuando sean elementos distintos. En filas verdes, ajustar también los textos e iconos con `Foreground` local para que usen carbón y no conserven un gris claro por encima del fondo verde.
- [ ] Redondear buscadores de una línea, botones secundarios y filtros; conservar el editor de texto multilínea y los controles de reproducción completos.
- [ ] Sustituir fondos y bordes locales de la cabecera, monitores, transporte, insignias y barra de estado. En `MainWindow.Layout.cs`, representar los colores de DWM como `COLORREF` BGR: fondo `#1F2023` → `0x23201F`; texto `#F5F5F5` → `0xF5F5F5`.
- [ ] Aplicar los recursos de tema ya usados en `InitializeDocking`. Revisar cabeceras activas/inactivas y ventanas flotantes sin reemplazar plantillas de arrastre o serialización de AvalonDock.
- [ ] En miniaturas, cambiar solo los acentos de interfaz. Conservar la representación del contenido, la carga asíncrona, cancelación y liberación de archivos.
- [ ] Ejecutar ensayos `--compact-output`, `--order`, `--content-drag`, `--media-layout-headless` y `--capture-layout-headless`; revisar capturas comparables y guardar un commit de superficies.

**Resultado revisable:** el aspecto refleja la referencia y la consola sigue funcionando con su distribución actual y una guardada por el usuario.

## Tarea 5: validar conservación funcional y entregar por separado

Usar los proyectos y selectores existentes. No crear un framework de pruebas ni pruebas que simplemente repitan los hexadecimales del XAML.

```powershell
./scripts/build.ps1
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --compact-output
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --order
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --content-drag
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --bible-tools
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --youtube-controls
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --player
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --output-player
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --backdrops
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --saved-sets
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --remote-order
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --media-layout-headless
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --capture-layout-headless
```

Si `.tools/dotnet/dotnet.exe` no existe en el entorno de ejecución, usar el SDK .NET 10 que ya resuelve `scripts/build.ps1`. Los ensayos de escritorio crean ventanas y datos temporales; no dirigirlos al espacio de trabajo real.

- [ ] Revisar 1480 × 900 y 1280 × 680 DIP, más el mínimo declarado de 1200 × 680, y escalas de Windows de 100 %, 125 % y 150 %. Distinguir DIP del tamaño físico del monitor al registrar resultados.
- [ ] Comprobar títulos largos, listas vacías, filtros, pestañas, menús, notas, Biblia y todos los diálogos indicados. Los controles principales deben seguir visibles o alcanzables mediante el desplazamiento de su panel.
- [ ] Comprobar monitor preparado y monitor de salida en 16:9, sin recorte del contenido, tanto localmente como con HDMI disponible. Mantener visibles los estados de salida y controles de transporte en panel estrecho.
- [ ] Comprobar arrastre, flotación, guardado/restauración de `paneles.xml`, cambio rápido de pestañas y foco del teclado sin cambiar el contenido en vivo.
- [ ] Repetir la regresión relevante de 0.28.3 con los comandos siguientes. Registrar resultados, incluidos fallos; no inferir rendimiento a partir del tema.

```powershell
./.tools/dotnet/dotnet.exe run --project tests/Culto.Renderer.Smoke -c Release --no-build -- --capture-to-file
./.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release --no-build -- --cold-play
```

- [ ] Revisar PowerPoint nativo, mando físico y HDMI real cuando estén disponibles. Si faltan Office, teléfono o pantallas, indicarlo expresamente en `docs/validacion.md` y conservar ese límite en la entrega.
- [ ] Actualizar `docs/validacion.md` con comandos, resultados, capturas antes/después y límites. La validación de este plan no sustituye la validación de una implementación.
- [ ] Preparar una distribución nueva en una carpeta de versión independiente solo al completar la implementación y sus comprobaciones. No sobrescribir la entrega 0.28.3 ni cambiar el iniciador habitual antes de revisar el resultado visual.

**Aceptación:** colores y formas coherentes con la referencia, Lexend empaquetada, texto y foco legibles, controles completos, distribuciones compatibles y funciones del contrato conservadas. Una falla preexistente debe registrarse y compararse con la línea base, no ocultarse ni declararse resuelta por este trabajo.

## Recuperación

Si la adaptación no se acepta, cerrar la edición nueva y ejecutar la distribución conservada `artifacts/CultoStudio-0.28.3`. Para el código, revertir únicamente los commits de adaptación o recuperar una copia aislada desde `c13da65`; no usar `reset --hard` sobre trabajo compartido. La recuperación de datos locales usa el respaldo independiente, no el commit. Este plan no requiere migraciones de datos.

## Estado al entregar este documento, antes de implementarlo

Se revisaron la referencia y el código de `c13da65`, y se calcularon los contrastes indicados. No se modificaron código de aplicación, fuentes, datos, distribución ni iniciadores. No se ejecutaron compilación ni pruebas de la aplicación porque el trabajo solicitado es preparar el plan; los comandos anteriores son comprobaciones de la futura implementación.

La consulta automática de diseño de UI/UX no produjo una propuesta adecuada a esta consola y se descartó. Las decisiones visuales proceden de la referencia observada; la guía WPF se usa para conservar foco, teclado y comportamiento nativo.

## Ejecución — versión 0.29.0

El usuario autorizó ejecutar este plan con «pues trabaja». Se preparó la implementación en `codex/culto-studio-adaptacion-visual`, desde el plan confirmado en `c337534`, conservando la distribución 0.28.3 y el checkpoint `c13da65`.

Se incorporaron Lexend, paleta, estados y superficies de las tareas 1–4. Las correcciones del encaje compacto incluyen `MainWindow.CompactOutput.cs`: controles secundarios dentro del desplazamiento, imagen con mínimo propio y cabecera de una línea. Los mínimos de altura se normalizan antes de restaurar AvalonDock para evitar los desbordamientos reproducidos también en la versión anterior. No se sustituyen sus plantillas ni se cambian los motores de proyección, reproducción o captura.

La evidencia de las verificaciones, los fallos preexistentes y los límites físicos de la tarea 5 están en [validacion.md](../../validacion.md). Las comprobaciones implementadas cubren 1480 × 900, 1280 × 680 y 1200 × 680 DIP, así como una salida flotante mínima. Las escalas 125 %/150 %, HDMI, teléfono, presentador físico y PowerPoint real permanecen como ensayos de hardware pendientes; no se presentan como comprobados.

Los casilleros anteriores conservan la lista del plan original. Esta sección y el registro de validación describen lo efectivamente ejecutado. Los cambios visuales, sus pruebas y la entrega se guardan como un commit conjunto tras verificarlos, en lugar de separar commits de una misma edición de `App.xaml`.
