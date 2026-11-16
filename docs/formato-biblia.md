# Importación de Biblia

El programa acepta JSON UTF-8 o Zefania XML con los 66 libros del canon protestante. Importa únicamente archivos que tengas autorización para utilizar en esta aplicación. No se necesita internet después de importar.

## JSON

```json
{
  "name": "Nombre real de la versión",
  "attribution": "Aviso y atribución proporcionados por la fuente autorizada",
  "verses": [
    { "book": "Juan", "chapter": 3, "verse": 16, "text": "Texto autorizado del versículo." },
    { "book": "Juan", "chapter": 3, "verse": 17, "text": "Texto autorizado del siguiente versículo." }
  ]
}
```

Este ejemplo describe la estructura; **sus frases no son texto bíblico**. Para RVR1960 usa el nombre y la atribución correctos de la fuente. Cada versículo debe tener un libro reconocido, capítulo y versículo enteros positivos y texto no vacío. Los duplicados se rechazan.

Los libros pueden usar su nombre en español, por ejemplo Génesis, Salmos o 1 Corintios. El importador normaliza nombres y mantiene el orden canónico. Un rango solicitado debe existir completo; no se proyectan rangos parciales silenciosamente.

## Zefania XML

```xml
<?xml version="1.0" encoding="utf-8"?>
<XMLBIBLE biblename="Nombre real de la versión">
  <INFORMATION>
    <title>Nombre real de la versión</title>
    <rights>Atribución de la fuente autorizada</rights>
  </INFORMATION>
  <BIBLEBOOK bnumber="43" bname="Juan">
    <CHAPTER cnumber="3">
      <VERS vnumber="16">Texto autorizado del versículo.</VERS>
    </CHAPTER>
  </BIBLEBOOK>
</XMLBIBLE>
```

`bnumber` va del 1 (Génesis) al 66 (Apocalipsis), `cnumber` identifica el capítulo y `vnumber` el versículo. El programa conserva la información de atribución y rechaza XML con DTD/entidades externas. Los archivos XML de otras aplicaciones con estructuras diferentes requieren conversión al formato documentado.

Al importar, se conserva una copia de cada versión en `%LOCALAPPDATA%\CultoStudio\Biblias`. La versión activa se guarda también en `biblia.json`; mover el archivo original después no afecta a la consulta. Una importación nueva no elimina las versiones anteriores. El selector «Versión» del panel permite cambiarlas sin modificar el contenido que ya está proyectado. El botón `+` admite seleccionar varios archivos a la vez. Una importación fallida conserva la Biblia que ya estaba disponible.

Para textos cuya fuente agrupa varios versículos en una sola unidad, JSON admite `endVerse`: por ejemplo, `verse: 5, endVerse: 6` identifica el texto conjunto 5–6. Se conserva ese texto una sola vez y se puede encontrar consultando cualquiera de los números del grupo. No se permiten rangos superpuestos. Los campos calculados para mostrar el número no se guardan en el archivo.

Las versiones instaladas y sus fuentes se documentan en [versiones-biblia.md](versiones-biblia.md).
