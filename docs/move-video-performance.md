# Move con video: comprobación de rendimiento

La captura de dos superficies de video mediante `VisualBrush` y `RenderTargetBitmap` bloqueaba el hilo de interfaz durante Move. Con dos videos sintéticos Full HD ya reproduciéndose, la mediana por actualización fue de 208 ms, con 15,53 MiB de nuevas matrices por actualización.

La ruta de video ahora copia el buffer RV32 decodificado bajo el bloqueo existente de VLC, sin esperar si se está escribiendo. Cada fuente reutiliza su matriz de píxeles. Las bandas negras y el alfa opaco se conservan. La publicación inicial puede usar el fotograma ya presentado; los errores, la supresión y las dimensiones incompatibles conservan la captura normal del compositor.

En la comprobación posterior, la mediana Full HD fue de 16,12 ms, sin nuevas matrices de fotogramas por actualización. Una transición nativa posterior entre videos produjo 91 imágenes distintas en 3,40 s, con una separación mediana de 33 ms y máxima de 83 ms. Son mediciones locales con fuentes sintéticas; varían con la carga del equipo.

Comprobaciones ejecutadas:

- `Culto.Renderer.Smoke --move-video-cadence`: alfa RV32, centrado, limpieza al reducir el video, dimensiones mayores, reutilización de buffers a 640/1280/1920 y avance continuo del efecto nativo.
- `Culto.Renderer.Smoke --live-media`: una sola fuente compartida, reloj de video, ganancia de audio, retención detrás de texto, interrupción y recuperación del motor.

La ruta de cadencia genera su video de prueba con `ffmpeg` si no existe en `.tools/move-cadence-1080.mp4`. El ejecutable de pruebas necesita `MoveHost` y `Runtime/ObsMove` junto a él, igual que las otras pruebas nativas.
