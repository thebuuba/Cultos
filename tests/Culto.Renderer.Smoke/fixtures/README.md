# Archivos sintéticos para pruebas

`test.mkv`, `audio.wav` y `panorama.png` fueron generados localmente para probar reproducción y ajuste de imagen. No contienen contenido del himnario ni material de terceros. `corrupt.png` contiene datos inválidos intencionalmente para probar el manejo de errores.

El MKV dura tres segundos y contiene un patrón visual de prueba y un tono. El ensayo puede emitir ese tono brevemente. Las capturas y el resultado se generan aquí al ejecutar `scripts/build.ps1 -Smoke` y no se guardan en Git.

`dotnet run --project tests/Culto.Renderer.Smoke -c Release -- --capture-to-file` abre únicamente una fuente sintética propia y dos monitores de prueba. Comprueba la preparación del motor durante la captura, el primer cambio a un archivo local y los clics repetidos mientras espera el primer fotograma. Este selector usa volumen cero y no controla el navegador.

`test.mp4` es una conversión del mismo patrón sintético a H.264/AAC para comprobar el video incrustado de PowerPoint. Se genera con `ffmpeg -i test.mkv -c:v libx264 -pix_fmt yuv420p -c:a aac test.mp4`. La prueba nativa requiere Microsoft PowerPoint instalado: `.tools/dotnet/dotnet.exe run --project tests/Culto.Desktop.Smoke -c Release -- --powerpoint`. Crea y cierra únicamente sus propias presentaciones temporales.
