# Fundido de audio con reverberación Catedral

## Experiencia aprobada

En Transiciones se podrá elegir **Fundido normal** o **Fundido con reverberación Catedral**. Catedral añade una cola suave al contenido que sale. El contenido que entra conserva su sonido seco. Habrá controles de intensidad y duración y un botón **Probar audio** para escuchar un ejemplo aislado antes de guardar.

El usuario aprobó esta experiencia en conversación. La revisión técnica descubre un cambio de propiedad de la salida de audio; este documento concreta ese alcance para aprobarlo antes de implementar el nuevo procesador.

## Alcance

- Archivos de video y audio, incluidos himnos y los reproducidos desde Lista de videos.
- Misma configuración en los perfiles de transiciones y en la transición del sistema.
- Catedral desactivada por defecto; configuraciones guardadas anteriores mantienen el fundido normal.
- Intensidad de la reverberación entre 0 y 60 %, con 25 % de inicio.
- Duración de la cola entre 0,5 y 8 segundos, con 2,5 segundos de inicio.
- La duración del fundido seco continúa siendo el valor existente de Audio al salir.
- YouTube/Chrome externo, captura de ventanas y PowerPoint conservan sus rutas actuales.

## Activación y reproducción en curso

La elección de la ruta de audio se hace antes de iniciar el decodificador de cada archivo. Guardar un cambio no reinicia ni interrumpe un video en curso. Por ello, activar Catedral mientras ya suena un archivo se aplica a los siguientes archivos que se empiecen a reproducir; la salida del archivo que ya estaba sonando conserva el fundido normal. El editor debe explicar esta condición junto a la opción.

## Diseño técnico

### Ruta normal

El fundido normal conserva la salida DirectSound actual de LibVLC. No se cambia su selección de dispositivo ni el comportamiento de silencio, volumen, pausa o avance automático.

### Ruta Catedral

Para los archivos iniciados con Catedral activada, LibVLC entrega PCM S16N est?reo a 48 kHz mediante `SetAudioCallbacks` y `SetAudioFormat`. Estos callbacks sustituyen la salida de audio nativa: la aplicación pasa a controlar el búfer, el reloj, el volumen y la reproducción mediante WASAPI compartido. Se reutiliza el patrón local de `BrowserAudioOutput`, sin capturar el audio de Windows ni de otras aplicaciones.

El sonido normal del archivo se reproduce seco. Al abandonar el archivo, se alimenta el procesador de reverberación con el audio saliente y se mantiene una cola finita después de terminar su fundido y de retirar su imagen. El siguiente archivo tiene su propia señal seca; su sonido no alimenta la cola anterior. Se limita el número de colas simultáneas y se cancela la cola anterior en cambios rápidos para evitar acumulaciones.

Se usará el efecto I3DL2 de NAudio/Windows, disponible en las dependencias existentes. La cola debe seguir procesando y reproduciendo silencio hasta agotarse: el envoltorio `DmoEffectWaveProvider` de NAudio 2.2.1 descarta la cola al consumirla y no sirve directamente para este fin.

### Volumen y ciclo de vida

- Mantener la curva audible del volumen general actual y las ganancias independientes de cada fundido.
- Silenciar o bajar el volumen general afecta también a las colas que sigan sonando.
- Pausa, seek, reinicio, EOF y cambio rápido vacían o detienen los búferes de forma explícita; ninguna muestra anterior reaparece tras reanudar.
- Una fuente compartida entre escenas conserva un solo decodificador. Mover su geometría no inicia una reverberación ni duplica su audio.
- Cerrar la salida o la aplicación cancela y libera dispositivos, callbacks, procesadores y colas.
- Un fallo del procesador debe conservar audio seco y mostrar un aviso; nunca dejar una salida silenciosa marcada como correcta.

## Editor y prueba

Añadir una tarjeta de efecto de audio debajo de los tiempos existentes, con selector Normal/Catedral, intensidad, cola y Probar audio/Detener. Los controles Catedral se habilitan al elegir ese modo. Guardar y Cancelar siguen trabajando sobre un borrador. Restablecer vuelve al fundido normal.

La prueba reproduce una muestra corta seguida de su cola con los valores del borrador. No proyecta contenido, no modifica el volumen de Windows y no altera la lista ni el archivo que esté sonando en salida.

## Comprobación requerida

1. Persistencia, normalización de valores y compatibilidad de archivos guardados sin campos nuevos.
2. Procesar un impulso y verificar señal posterior al final de la entrada, decaimiento y límite temporal; intensidad cero produce salida seca.
3. Medir el audio realmente emitido: fundido seco, cola Catedral y siguiente archivo seco, sin saturación ni silencios persistentes.
4. Verificar sincronía entre audio y video en reproducción normal con la ruta Catedral activa.
5. Lista de videos: añadir, inicio, avance natural, salida de lista, cambios rápidos y reproducción tras EOF con Normal y Catedral.
6. Pausa/reanudación, seek, silencio general, cierre de salida y cancelación de la prueba.
7. Revisar visualmente el editor y comprobar que Cancelar no persiste valores ni altera la salida activa.

## Fuentes técnicas verificadas

- API LibVLC 3: los callbacks de audio reemplazan la salida normal: https://videolan.videolan.me/vlc-3.0/group__libvlc__media__player.html
- Spatializer de VLC 3: controles internos y filtro de inicio, sin una API pública equivalente para activarlo dinámicamente por transición: https://raw.githubusercontent.com/videolan/vlc/3.0.x/modules/audio_filter/spatializer/spatializer.cpp
- Efecto I3DL2 de NAudio 2.2.1: https://raw.githubusercontent.com/naudio/NAudio/v2.2.1/NAudio.Wasapi/Dmo/Effect/DmoI3DL2Reverb.cs
- Tratamiento de colas en el envoltorio de NAudio 2.2.1: https://raw.githubusercontent.com/naudio/NAudio/v2.2.1/NAudio.Wasapi/DmoEffectWaveProvider.cs
