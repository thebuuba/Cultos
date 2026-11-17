# Culto Studio para Elgato Stream Deck

Requiere Windows 10 o posterior y Stream Deck 7.1 o posterior (runtime Node.js 24). El instalador local está en `dist/com.cultostudio.control.streamDeckPlugin` después de compilar.

1. Abre el instalador con Stream Deck y acepta instalar el plugin **Culto Studio**.
2. Abre Culto Studio y activa su integración nativa Stream Deck.
3. Arrastra las acciones de la categoría **Culto Studio** a las teclas de tu perfil. Para **Escena** o **Elemento del orden**, elige el contenido en el inspector de propiedades.

La conexión local se descubre automáticamente; no necesita códigos QR, tokens manuales, puertos fijos ni atajos de teclado. Instalar este plugin no importa ni reemplaza perfiles.

Las teclas muestran el estado confirmado por Culto Studio: pausa, negro, volumen y elementos preparados/en vivo. Sin la aplicación abierta muestran **Sin conexión** y vuelven a conectarse automáticamente. Los cambios se consultan cada 750 ms. Las pulsaciones que fallan no se reenvían: comprueba el estado antes de volver a pulsar. Pulsaciones del mismo botón separadas por menos de 250 ms se descartan.

## Desarrollo

```powershell
npm ci
npm test
npm run pack
```

`build` genera `com.cultostudio.control.sdPlugin` con las dependencias incluidas en `bin/plugin.js`; `validate` y `pack` utilizan la CLI oficial de Elgato. El manifiesto usa SDKVersion 2 para distribución local y el SDK oficial `@elgato/streamdeck` 3.0.4 para las acciones. No se necesita instalar Node.js por separado en el equipo del operador.

El descriptor se lee de `%LOCALAPPDATA%/CultoStudio/streamdeck-connection.json`. La variable `CULTO_STREAMDECK_CONNECTION` permite usar un descriptor aislado en pruebas. No incluye credenciales en la configuración del botón ni en los registros. Solo admite `http://127.0.0.1:<puerto>` y tokens hexadecimales de 64 caracteres. Las solicitudes usan autenticación bearer y un identificador único de comando.

Las comprobaciones automatizadas cubren transporte HTTP, validación, desconexión, reconexión, pulsaciones repetidas y estado derivado. La validación y el empaquetado oficial no sustituyen una prueba con el dispositivo físico.
