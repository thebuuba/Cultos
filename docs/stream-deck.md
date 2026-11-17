# Culto Studio para Stream Deck

La integración nativa añade la categoría **Culto Studio** a la aplicación de Elgato. Puedes arrastrar sus acciones a los botones, elegir escenas o elementos del orden y ver en las teclas el estado confirmado por la computadora.

## Instalar y conectar

1. Instala o abre **Elgato Stream Deck 7.1 o posterior** en la misma computadora que Culto Studio.
2. En Culto Studio, abre **Configuración → Stream Deck → Instalar plugin en Elgato…**, o abre `StreamDeck/com.cultostudio.control.streamDeckPlugin` dentro de la carpeta del programa.
3. Acepta la instalación del plugin **Culto Studio** en Elgato. Se añade una categoría nueva; no reemplaza tus perfiles.
4. En Culto Studio, activa **Configuración → Stream Deck → Integración nativa con Elgato**.
5. En Elgato, arrastra las acciones de **Culto Studio** a las teclas que quieras usar.

La conexión se configura automáticamente en esta computadora. No necesita QR, wifi, direcciones, contraseñas ni combinaciones de teclas. No necesitas instalar Node.js por separado.

## Acciones

| Botón | Función y estado |
| --- | --- |
| Proyectar | Envía el contenido preparado; muestra su título. |
| Anterior / Siguiente | Recorre el orden, la lectura o PowerPoint. |
| Logo | Muestra el logo configurado; indica si está activo. |
| Negro | Oculta o restaura la salida; indica negro activo. |
| Pausa | Pausa o continúa; cambia según el estado real. |
| Reiniciar | Reinicia el video o audio que admite ese control. |
| Volumen − / + | Ajusta cinco puntos el volumen de Culto Studio y muestra el porcentaje. |
| Volver a medios | Vuelve al video o audio de fondo conservado. |
| Escena | Elige una escena existente en el panel de configuración del botón. |
| Elemento del orden | Elige un elemento del orden; conserva su identidad aunque lo muevas y señala si está preparado o en vivo. |

**Escena** y **Elemento del orden** presentan una lista del contenido disponible en la computadora. Si se elimina una selección o deja de estar disponible, el botón lo indica. La reproducción de YouTube se controla desde Chrome; los botones que no pueden controlar ese contenido muestran **Desde YouTube**.

## Desconexiones

Si Culto Studio está cerrado o la integración desactivada, las teclas muestran **Sin conexión**. Al abrir y activar el programa se recuperan automáticamente, incluso después de reiniciarlo. El estado se consulta cada 750 milisegundos.

Las acciones no se repiten automáticamente tras una respuesta perdida. Si un botón muestra una alerta, comprueba la salida antes de pulsarlo otra vez. Las pulsaciones del mismo botón separadas por menos de 250 milisegundos se descartan para evitar duplicados.

## Compatibilidad y alternativa

El plugin es para Windows 10 o posterior y los botones de los dispositivos Stream Deck. No cambia tus perfiles ni instala contenido en Marketplace. La opción **Alternativa: teclas F13–F24** permanece disponible para perfiles antiguos, pero no hace falta activarla para el plugin.

La integración se comprueba con el SDK oficial, el protocolo de Elgato y una instancia real de Culto Studio. Esto no sustituye probar el dispositivo físico conectado.

## Compilar el instalador

Desde la carpeta del proyecto, ejecuta `scripts/build-streamdeck.ps1`. Utiliza las dependencias fijadas, ejecuta pruebas y valida y empaqueta con la CLI oficial de Elgato. El resultado queda en `artifacts/StreamDeck` y se copia a la distribución local si existe.

Fuentes oficiales: [SDK y acciones de Elgato](https://docs.elgato.com/streamdeck/sdk/guides/actions/), [empaquetado de plugins](https://docs.elgato.com/streamdeck/sdk/introduction/distribution/).
