# Videos de YouTube en el orden del culto

1. Copia el enlace del video de YouTube.
2. En **Orden del culto**, pulsa **Ctrl+V** o abre el menú y elige **Pegar enlace de YouTube**. También puedes pegar varios enlaces separados por líneas.
3. El elemento muestra el título y la miniatura cuando hay conexión. El enlace se conserva aunque falle la consulta del título.
4. Haz doble clic en el elemento para cargarlo, iniciar su reproducción y proyectarlo. Culto Studio reutiliza la misma ventana y pestaña de YouTube que tienes abierta en Chrome y aplica el encuadre que tengas guardado. Si tienes varias ventanas, selecciona la que usas en el panel **YouTube**.

Los enlaces quedan guardados con el orden. Se reconocen enlaces normales, cortos, Shorts y directos; se conserva el tiempo inicial indicado en el enlace. También se pueden proyectar con **Proyectar** y al avanzar por el orden.

Necesitas Google Chrome instalado e internet. Se conserva el perfil de la ventana elegida, con sus extensiones, bloqueador y scripts. Si no hay una ventana de YouTube abierta, se abre YouTube en tu Chrome habitual. Las restricciones de los videos siguen dependiendo de YouTube.

Chrome se activa brevemente para cargar el enlace y después devuelve el foco a la consola si no cambiaste de ventana durante la carga. El enlace del portapapeles se conserva. No hace falta reiniciar Chrome ni habilitar el control remoto.

El doble clic envía la ventana a **Programa** en cuanto Chrome confirma el enlace del video. No vuelve a consultar el título ni espera la comprobación de reproducción para proyectar. Si el reproductor queda pausado, el programa pulsa su botón **Reproducir** en paralelo; si ya está reproduciendo, lo deja continuar. La página puede mostrar su carga mientras YouTube recibe el video. Los avisos de acceso o restricciones del video deben resolverse en YouTube.

## Verificación

El ensayo `--youtube-order-links` comprueba creación y persistencia de los enlaces, título visible en la fila, validación de dominios y que pegarlos no cambie Programa. Con `--native-youtube-links` comprueba además dos videos en una ventana de Chrome ya abierta sin control remoto, sin crear otra ventana ni modificar el portapapeles, su recuperación al estar minimizada, miniatura y llegada de la captura a Programa. Solo el ensayo usa un perfil temporal y el navegador silenciado.

Añadiendo `--native-youtube-autoplay`, el ensayo utiliza control remoto únicamente para observar el video e instalar una pausa de prueba que exige una pulsación real en el reproductor. La navegación y el inicio de reproducción siguen usando accesibilidad de Windows. Comprueba que el tiempo del video avance y que volver a comprobar un video ya en reproducción no añada pulsaciones ni lo pause.

El mismo ensayo retrasa ocho segundos los botones del reproductor y comprueba que un doble clic real en el orden lleve el elemento a Programa en menos de cuatro segundos, sin otra pulsación, y que después comience a reproducirse.

Al cambiar entre elementos que capturan la misma ventana y el mismo encuadre, se conserva el motor de captura activo. La navegación y descarga del nuevo video siguen dependiendo de YouTube. El archivo `youtube-timing.log` en el directorio de datos registra por solicitud los tiempos de navegación, proyección, captura visible y confirmación del reproductor, sin guardar enlaces ni títulos; permite investigar demoras que no aparezcan en el ensayo.
