# Escenas por capas y Move en vivo

## Estado al guardar el commit

Implementados modelo y editor de capas, fuentes compartidas, fuente nativa de video y Move con fotogramas vivos, perfiles y control remoto. Pasaron las pruebas nativas y las de editor, navegador en movimiento y video compartido. La solución compila sin advertencias ni errores y las 302 pruebas del núcleo pasan.

La última integración añade audio PCM propio del navegador y envolventes de volumen por fuente. Esa parte compila pero su verificación final de audio e integración sigue pendiente. Esta versión por capas todavía no se ha instalado; las limitaciones descritas en auditorías anteriores corresponden a las versiones previas, no constituyen una certificación del estado nuevo.

Objetivo autorizado: escenas compuestas por fuentes independientes y Move nativo mientras ambas escenas siguen actualizando sus fotogramas, incluyendo navegador/captura y audio conservado.

## Contratos

- `CueKind.Scene` al final del enum para conservar datos previos. `Cue.Scene` contiene `SceneComposition { Width=1920, Height=1080, Background="#000000", Layers }`.
- `SceneLayer { Id, SourceId, Name, Cue Source, X,Y,Width,Height,Rotation,Opacity,Visible,Muted,Volume }`. SourceId estable identifica una fuente compartida entre escenas; la copia de una escena conserva SourceId para mantener video/navegador y animar su transformación. Las fuentes no contienen escenas anidadas ni PowerPoint.
- La reproducción utiliza un depósito compartido de fuentes WPF vivas. Cada escena dibuja sus capas mediante VisualBrush, sin crear un segundo reproductor para una fuente compartida. Las fuentes dejan de existir al no estar referenciadas por escena entrante/saliente.
- Move usa fuentes de video propias registradas en libobs. Un mapa de memoria por fuente entrega BGRA actualizado, con versión para evitar fotogramas incompletos. El plugin real `move_transition` recibe escenas OBS con los mismos nombres/identidades de fuente y transformaciones.
- El compositor existente conserva las envolventes de audio, pausa, negro y cola. Una superposición nativa presenta Move; ambos decodificadores siguen vivos. Al terminar se retira la superposición y las fuentes WPF continúan.

## Trabajo y verificaciones

1. Modelo, normalización, copia e interfaz de capas; compatibilidad de datos y editor con vista previa.
2. Protocolo, buffers compartidos, fuente de video nativa y renderizador Move de escenas vivas. Verificar fuentes móviles y coincidencia de identidades.
3. Depósito de fuentes, composición WPF, integración con mixer y cliente nativo. Eliminar la sustitución por fundido para los casos soportados.
4. Pruebas de movimiento con fotogramas cambiantes, fuentes compartidas sin reinicio, navegador/captura, audio conservado, cancelación, fallos y persistencia. Revisar código e instalar solo tras pasar las verificaciones.
