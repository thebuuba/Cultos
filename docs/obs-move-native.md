# Integración de Move y PowerPoint — 0.17.0

Esta entrega prepara código y archivos portables. Por indicación del usuario no se ejecutaron OBS, MoveHost, PowerPoint ni las pruebas que abren ventanas durante su desarrollo. La compilación y las pruebas puras no demuestran todavía compatibilidad nativa ni un fundido audible real.

## Complemento Move auténtico

Configuración → Transición del sistema ofrece **Move · OBS**. La selección persiste igual que los demás efectos. El compositor entrega sus imágenes de salida/entrada al proceso privado `MoveHost`, que carga `libobs`, `image-source`, `obs-transitions` y el módulo **move-transition** oficial. El identificador de la transición debe ser `move_transition`; un fallo no se sustituye silenciosamente por Zoom o Disolución.

Se fijan OBS **33.0.1** y Move **3.2.1**, Windows x64. `scripts/prepare-obs-move.ps1` descarga los ZIP oficiales, comprueba sus hashes y extrae el runtime bajo `artifacts/ObsMoveRuntime`. No ejecuta instaladores ni módulos. `-Offline` exige que los ZIP ya estén en la caché verificada. El manifiesto registra archivos, tamaños, hashes, exports y procedencia. El runtime se copia al publicar, excluyendo `.downloads`.

La aplicación inicia el host sin ventana únicamente cuando se solicita Move. El motor usa escenas privadas y no conecta con el OBS instalado, no importa su configuración ni modifica sus escenas. Los fotogramas BGRA se intercambian por un pipe local restringido al usuario actual. Hay límites de tamaño, escrituras serializadas, colas acotadas, cancelación y plazo máximo. Un fallo nativo termina el host propio y se comunica a la consola.

El efecto opera sobre **dos capturas del compositor**, incluyendo el fotograma de un video: el contenido se mantiene congelado durante esa transición y vuelve al reproductor vivo al terminar. No es un editor de escenas OBS por capas, no importa proyectos de OBS y no configura los otros filtros/acciones del paquete Move. Las presentaciones nativas de Office mantienen su propio renderizador y sus transiciones internas.

Pendiente de ejecución autorizada: inicialización de GPU, carga real del plugin y sus dependencias, efecto visible, continuidad de video y rendimiento. La distribución oficial requiere el runtime VC++ x64 indicado en el manifiesto; esta entrega no instala prerrequisitos del sistema.

## Audio de PowerPoint

La copia propia captura el volumen original de cada medio mediante `MediaFormat.Volume`. Una envolvente aplica la ganancia de entrada/salida del sistema a esos medios y al volumen general de Culto Studio. El adaptador se ejecuta desde el hilo STA de la interfaz. Nunca ajusta la sesión de audio del proceso de Office ni el volumen principal de Windows.

El cambio de contenido espera al fundido saliente. Las solicitudes nuevas reemplazan las anteriores; negro, fondo, pausa y reinicio conservan la intención vigente mientras se espera. Los ajustes se capturan por solicitud. Se incluyen los medios que pueden continuar entre diapositivas. Al salir se cierra la reproducción propia, se restauran sus niveles originales y se liberan los objetos COM.

Las pruebas puras cubren curvas, cancelación, solicitudes antiguas, volúmenes originales, reentrada y restauración con fallos. Falta comprobar que cambiar `MediaFormat.Volume` modifique audiblemente un medio que ya está reproduciéndose en la versión instalada de PowerPoint. No se presenta esta comprobación como realizada.

## Barra flotante al pausar

Se mantiene la pausa nativa de animaciones y medios. El programa solamente puede ocultar una ventana `MsoCommandBar` si su cadena de propietarios llega al HWND de su propia presentación. Revalida proceso, hilo, clase, título y propietarios antes de actuar, sin activar ni mover ventanas. Las barras globales de Office o de otra presentación quedan fuera de esa política.

La política tiene pruebas puras de atribución, ciclos, propietarios ausentes y otras presentaciones. Sigue pendiente comprobar a quién pertenece la barra observada y que desaparezca en ejecución; si es global, esta corrección deliberadamente no la oculta.
