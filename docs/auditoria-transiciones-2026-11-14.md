# Auditoría de transiciones — 14 de noviembre de 2026

## Resultado

Las pruebas detectaron y permitieron corregir cuatro problemas reales. La implementación sigue sin equivaler al conjunto completo de transiciones y escenas de OBS. Una prueba que termina correctamente no certifica esa equivalencia.

## Correcciones comprobadas

1. Una escena de audio/video activada con «Solo audio» tomaba los ajustes anteriores de la salida. Ahora captura los ajustes del perfil asignado a esa escena.
2. Conservar audio podía ejecutar un fundido mientras el estado informado indicaba Move. Ahora informa el efecto real y la interfaz explica esta limitación.
3. Un corte a negro podía esperar detrás de un fundido de audio de cinco segundos. Ahora el corte interrumpe la transición y descarta órdenes antiguas en espera. La prueba exige respuesta dentro de 250 ms.
4. Un fallo del efecto Move detenía contenido válido. Ahora retira la superposición, conserva el destino, comunica el fallo y vuelve a crear el motor en el siguiente intento.

## Cobertura ejecutada

| Comprobación | Resultado |
| --- | --- |
| Perfiles: guardado, cancelación, selección y asignación por escena | Correcto |
| Vista previa independiente de la proyección | Correcto |
| «Solo audio» con fundidos propios de la escena | Correcto |
| Move nativo en direcciones opuestas, comprobando píxeles | Correcto |
| Diez funciones nativas de suavizado, con extremos de escala y curva | Correcto |
| Imagen → video → imagen | Correcto |
| Fallo del motor y recuperación posterior | Correcto |
| Interrupción de Move y protección frente a fotogramas antiguos | Correcto |
| Corte inmediato durante un fundido largo | Correcto |
| Continuidad de reproducción y audio durante fundidos del compositor | Correcto |
| Navegador: cambio de escena, negro, restauración, recarga y retirada | Correcto |
| HTML local: CSS, transparencia, animación y reflejo | Correcto |
| Mando: escenas y captura de una ventana propia de Chrome | Correcto |

La prueba del navegador se ejecutó desde una distribución Windows x64 con sus bibliotecas nativas. Su comprobación de reflejo tenía un error de coordenadas: capturaba el elemento con su desplazamiento vertical dentro de una imagen demasiado pequeña. La captura completa confirmaba que la fuente y el reflejo eran visibles; la medición se corrigió y volvió a pasar.

## Diferencias pendientes frente a OBS

- Move recibe dos capturas fijas del compositor. El video vuelve a su reproducción normal al terminar; no se renderizan continuamente ambas fuentes durante el efecto Move.
- Las escenas representan un contenido completo. No hay editor de capas independientes ni correspondencia de una misma fuente entre escenas para animar su posición y tamaño como Move Match.
- YouTube, navegador y las combinaciones que conservan audio usan fundido del compositor. Elegir Move no incorpora animación nativa a esos casos.
- Los perfiles permiten guardar los efectos disponibles. No incorporan por sí solos tipos adicionales de OBS, como Stinger o Luma Wipe, ni sus opciones específicas.
- No se ha certificado rendimiento en todas las resoluciones, equipos, medios, monitores o combinaciones de opciones.

## Reproducción de las pruebas

- Escritorio: `--transition-presets`, `--transition-settings`, `--editable-scenes`.
- Reproductor: `--native-move`, `--transition-audit`, `--smooth-transitions`.
- Distribución de pruebas Windows x64: `--browser-rendering`, `--remote-scenes`.
- Compilación y pruebas del núcleo/mando: `scripts/build.ps1`.

Evidencias locales en `.tools/transition-audit-*.log`, `.tools/move-audit-native-result.txt` y capturas en `.tools/move-editor/`.

## Referencias primarias

- [Guía oficial de OBS: transiciones y modo estudio](https://obsproject.com/kb/obs-studio-overview).
- [Complemento Move 3.2.1: código y propiedades nativas](https://github.com/exeldro/obs-move-transition/tree/3.2.1).
- [OBS: transiciones Stinger con máscara](https://obsproject.com/kb/track-matte-stinger-transitions).
