# Biblia importada en esta PC

El 11 de noviembre de 2026 se cargó **Reina-Valera 1960 (RVR1960)** en Culto Studio mediante su botón «Importar Biblia». Quedaron disponibles sin internet **66 libros, 1.189 capítulos y 31.102 versículos únicos**.

## Procedencia y conservación

La fuente fue el archivo local `Documentos\Easy Worshipp\Programa EasyWorship\Biblias\RV1960.ewb`. A pesar de su extensión, contiene XML legible, declarado ISO-8859-1, con estructura `bible/b/c/v`. La identificación RVR1960 procede del nombre y del cotejo de pasajes característicos; el archivo no incluye metadatos editoriales. No se realizó un cotejo editorial completo de cada versículo.

El original permanece intacto. Se creó una conversión UTF-8 en `%LOCALAPPDATA%\CultoStudio\Importaciones\Reina-Valera-1960.json`; la aplicación guarda su copia normalizada en `%LOCALAPPDATA%\CultoStudio\biblia.json`. El texto completo permanece en los datos locales del usuario, fuera de Git y de la distribución del ejecutable. La copia incluye atribución y notas de las dos correcciones siguientes.

## Correcciones documentadas

1. **2 Tesalonicenses 2:** el archivo fuente numeraba sus capítulos `1, 1, 3`. El segundo bloque, con 17 versículos, corresponde al capítulo 2. Se corrigieron sus 17 referencias, conservando los textos, tras contrastarlo con [RVR1960 en BibleGateway](https://www.biblegateway.com/passage/?search=2+Tesalonicenses+2&version=RVR1960).
2. **Proverbios 1:14:** la puntuación final contenía una coma seguida del carácter de control U+0097. Se sustituyó esa secuencia por un punto, conforme a [RVR1960 en YouVersion](https://www.bible.com/bible/149/PRO.1.14.RVR1960). No se cambiaron las palabras ni los espacios del versículo.

Una revisión independiente comparó los 31.102 registros: 31.101 textos se conservaron carácter por carácter, incluidos sus espacios, y el restante cambió solamente como se indica arriba. No hay referencias duplicadas, textos vacíos, saltos en la secuencia de capítulos/versículos ni caracteres de control. Los recuentos de versículos de cada uno de los 1.189 capítulos coinciden además con la Biblia local de FreeShow; esa otra traducción se utilizó únicamente para comparar recuentos, sin copiar su texto.

El importador normaliza los nombres de libro «Cantar de los Cantares» a «Cantares» y «Nahum» a «Nahúm». Se verificó que todos los textos y las coordenadas de la conversión se conservaron en `biblia.json`.

## Comprobaciones de uso

El `BibleCatalog` real cargó tanto la conversión como el archivo persistido y resolvió nueve consultas: Génesis 1:1, Éxodo 1:1, Cantares 1:1, Nahúm 1:1, Juan 3:16, los tres capítulos completos de 2 Tesalonicenses y Apocalipsis 22:21. También pasó la búsqueda por frase sin tildes y la creación de contenido preparado con el nombre de versión correcto.

En el ejecutable publicado se observaron el contador de 31.102 versículos, los 17 resultados de 2 Tesalonicenses 2:1–17, Apocalipsis 22:21 y la vista previa de Juan 3:16. Se dejó abierta la pestaña Biblia con Juan 3:16 preparado, sin abrir la salida ni añadir elementos al orden. No se repitieron las pruebas del programa porque esta operación sólo incorporó datos y documentación.

## Huellas SHA-256

| Archivo | SHA-256 |
|---|---|
| Original `RV1960.ewb` | `4445a9478e1b199cbedca91f4c8fd9165f1477205b14c8cdf721158904d0b12a` |
| Conversión `Reina-Valera-1960.json` | `1a287149aa6489ceeddeafc898343c5e994bdacaaa8e263145cadf92604bd68b` |
| Copia normalizada `biblia.json` | `52ed589e0506b1bf3cf25b9d4d048e0b7879e524670d5dfb6fbe9bdb5f7c97d5` |
