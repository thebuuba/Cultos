"""Convert local EWB files and previously downloaded eBible VPL archives to Culto JSON.

Uses only the standard library. Texts stay in the requested output directory,
outside source control. Downloads and publisher notices must be reviewed first.
"""
import argparse
from collections import defaultdict
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import struct
from zipfile import ZipFile
import zlib

BOOKS = ("Génesis|Éxodo|Levítico|Números|Deuteronomio|Josué|Jueces|Rut|1 Samuel|2 Samuel|1 Reyes|2 Reyes|1 Crónicas|2 Crónicas|Esdras|Nehemías|Ester|Job|Salmos|Proverbios|Eclesiastés|Cantares|Isaías|Jeremías|Lamentaciones|Ezequiel|Daniel|Oseas|Joel|Amós|Abdías|Jonás|Miqueas|Nahúm|Habacuc|Sofonías|Hageo|Zacarías|Malaquías|Mateo|Marcos|Lucas|Juan|Hechos|Romanos|1 Corintios|2 Corintios|Gálatas|Efesios|Filipenses|Colosenses|1 Tesalonicenses|2 Tesalonicenses|1 Timoteo|2 Timoteo|Tito|Filemón|Hebreos|Santiago|1 Pedro|2 Pedro|1 Juan|2 Juan|3 Juan|Judas|Apocalipsis").split("|")
CODES = "GEN EXO LEV NUM DEU JOS JDG RUT 1SA 2SA 1KI 2KI 1CH 2CH EZR NEH EST JOB PSA PRO ECC SNG ISA JER LAM EZK DAN HOS JOL AMO OBA JON MIC NAM HAB ZEP HAG ZEC MAL MAT MRK LUK JHN ACT ROM 1CO 2CO GAL EPH PHP COL 1TH 2TH 1TI 2TI TIT PHM HEB JAS 1PE 2PE 1JN 2JN 3JN JUD REV".split()
BY_CODE = dict(zip(CODES, BOOKS, strict=True))
for alias, canonical in {"EZE": "EZK", "JOE": "JOL", "NAH": "NAM", "SOL": "SNG", "MAR": "MRK",
                         "JOH": "JHN", "PHI": "PHP", "JAM": "JAS", "1JO": "1JN", "2JO": "2JN", "3JO": "3JN"}.items():
    BY_CODE[alias] = BY_CODE[canonical]
TITLES = {
    "spaRV1909": "Reina-Valera 1909 (RV1909)",
    "spaonbv": "Biblica® Open Nueva Biblia Viva 2008",
    "spabes": "La Biblia en Español Sencillo 2026 (BES)",
    "sparvg": "Reina-Valera Gómez (RVG)",
    "spapddpt": "Palabra de Dios para ti (PDDPT)",
    "spabll": "Biblia libre Latinoamericano (BLL)",
    "spablm": "Biblia libre para el mundo (BLM)",
    "spavbl": "Versión Biblia Libre (VBL)",
    "spav1602p": "Valera 1602 Purificada",
}
NVI_LOCAL_SHA256 = "617e6d71f969a6c19fa37e9c8fe881682ca82b0d213abb1021850e10cd470b89"
NVI_RESTORED_VERSE_SOURCE = "https://www.biblegateway.com/passage/?search=N%C3%BAmeros+26%3A50-52&version=NVI"


def repair_local_nvi(verses, source_sha256):
    """Repairs reviewed only for this user's exact EWB, never arbitrary editions."""
    if source_sha256 != NVI_LOCAL_SHA256:
        raise ValueError("NVI source differs from the locally reviewed file; repairs require a new audit")
    def rows(book, chapter, number):
        return [v for v in verses if (v["book"], v["chapter"], v["verse"]) == (book, chapter, number)]
    duplicate = rows("Números", 26, 43)
    assert len(duplicate) == 2 and duplicate[1]["text"] == rows("Números", 26, 44)[0]["text"]
    verses.remove(duplicate[1])
    fragments = rows("1 Samuel", 13, 15)
    assert len(fragments) == 2 and fragments[0]["text"] != fragments[1]["text"]
    fragments[0]["text"] += " " + fragments[1]["text"]
    verses.remove(fragments[1])
    obadiah = [v for v in verses if v["book"] == "Abdías"]
    assert len(obadiah) == 42 and [v["verse"] for v in obadiah[2:23]] == list(range(1, 22))
    # The contiguous block has clean 1:1-2, and the complete original 1:7.
    # Other copies append a book heading or omit a clause; retain original text.
    for number in (1, 2):
        variants = rows("Abdías", 1, number)
        assert len(variants) == 2 and variants[0]["text"].endswith(" Abdías 1")
    variants = rows("Abdías", 1, 7)
    assert variants[0]["text"].replace("Los que se sientan a tu mesa ", "") == variants[1]["text"]
    verses = [v for v in verses if v["book"] != "Abdías"] + obadiah[2:23]
    assert not rows("Números", 26, 50)
    # Only externally restored text: the reviewed 21-word quotation, not a bulk import.
    verses.append(dict(book="Números", chapter=26, verse=50,
        text="Estos son los clanes de la tribu de Neftalí. Su número llegó a cuarenta y cinco mil cuatrocientos hombres."))
    verses.sort(key=lambda v: (BOOKS.index(v["book"]), v["chapter"], v["verse"]))
    return verses


class PlainHTML(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.parts = []

    def handle_data(self, text):
        if text.strip():
            self.parts.append(text.strip())


def verse(book, reference, text):
    match = re.fullmatch(r"(\d+):(\d+)(?:-(\d+))?", reference)
    if not match or not text.strip():
        raise ValueError(f"Invalid reference or empty text: {book} {reference}")
    chapter, first, last = match.groups()
    item = dict(book=book, chapter=int(chapter), verse=int(first), text=text)
    if last:
        item["endVerse"] = int(last)
    return item


def local_ewb(path, title):
    data = path.read_bytes()
    if not data.startswith(b"EasyWorship Bible Text\x1a"):
        raise ValueError("Not an EasyWorship binary Bible")
    verses = []
    encodings = set()
    for index, book in enumerate(BOOKS):
        header = 88 + index * 224
        start = struct.unpack_from("<I", data, header + 208)[0]
        length = struct.unpack_from("<I", data, header + 216)[0]
        block = data[start:start + length]
        if len(block) != length or block[-10:-6] != b"QK\x03\x04":
            raise ValueError(f"Invalid compressed book: {book}")
        raw = zlib.decompress(block[:-10])
        if len(raw) != struct.unpack_from("<I", block, len(block) - 6)[0]:
            raise ValueError(f"Book length mismatch: {book}")
        encoding = "utf-8"
        try:
            text = raw.decode(encoding)
        except UnicodeDecodeError:
            encoding = "cp1252"
            text = raw.decode(encoding)
        encodings.add(encoding)
        for line in text.splitlines():
            if line:
                reference, words = line.split(" ", 1)
                verses.append(verse(book, reference, words))
    nvi = path.stem.lower() == "nvi"
    if nvi:
        verses = repair_local_nvi(verses, hashlib.sha256(data).hexdigest())
    # Some local NVI records repeat a two-verse label on two distinct rows.
    # Preserve both texts and their source order, assigning the two coordinates.
    grouped = defaultdict(list)
    for item in verses:
        grouped[(item["book"], item["chapter"], item["verse"], item.get("endVerse"))].append(item)
    repairs = []
    for (book, chapter, first, last), rows in grouped.items():
        if len(rows) > 1:
            if last is None or len(rows) != last - first + 1 or len({row["text"] for row in rows}) != len(rows):
                raise ValueError(f"Ambiguous duplicate: {book} {chapter}:{first}")
            for number, row in enumerate(rows, first):
                row["verse"] = number
                row.pop("endVerse")
            repairs.append(f"{book} {chapter}:{first}-{last}: etiquetas repetidas distribuidas en orden; textos intactos.")
    attribution = (f"Fuente local del usuario: {path.name}. Conversión del contenedor EasyWorship comprimido a JSON UTF-8. "
                   f"Codificación detectada: {', '.join(sorted(encodings))}. No se reescribieron los textos. "
                   "Los versículos agrupados se conservan como rangos, sin duplicar su texto. " + " ".join(repairs))
    if path.stem.lower() == "lbla":
        attribution += " La Biblia de las Américas © 1986, 1995, 1997 The Lockman Foundation. Uso local del archivo existente."
    if path.stem.lower() == "nvi":
        attribution += (" Nueva Versión Internacional. Fuente local de EasyWorship; edición editorial no especificada en el archivo. Uso local del archivo existente. "
            f"Auditoría local 2026-11-18; SHA256 original: {NVI_LOCAL_SHA256}. "
            "Reparaciones: eliminado segundo Números 26:43, idéntico al 26:44; unidos dos fragmentos originales de 1 Samuel 13:15; "
            "Abdías: conservado bloque original continuo 1:1-21, sin copias adicionales con encabezados, puntuación alternativa o cláusula omitida en 1:7. "
            f"Único versículo restaurado desde fuente externa: Números 26:50, cita de 21 palabras verificada en {NVI_RESTORED_VERSE_SOURCE} el 2026-11-18. "
            "El resto del texto procede del archivo local. No se realizó un cotejo editorial completo de toda la Biblia.")
    return dict(name=title, attribution=attribution, verses=verses)


def ebible(path, source_id):
    with ZipFile(path) as archive:
        text = archive.read(source_id + "_vpl.txt").decode("utf-8-sig")
        notice = PlainHTML()
        notice.feed(archive.read(source_id + "_about.htm").decode("utf-8-sig"))
    verses = []
    empty = []
    excluded = set()
    for line in text.splitlines():
        if line.strip():
            code, reference, words = line.split(" ", 2)
            if code not in BY_CODE:
                excluded.add(code)
                continue
            if not words.strip():
                empty.append(code + " " + reference)
                continue
            verses.append(verse(BY_CODE[code], reference, words))
    notes = ""
    if empty:
        notes += "Referencias sin texto en la fuente VPL, no rellenadas: " + ", ".join(empty) + ".\n"
    if excluded:
        notes += "Esta copia incluye los 66 libros admitidos por Culto Studio; libros adicionales de la fuente no incluidos: " + ", ".join(sorted(excluded)) + ".\n"
    return dict(name=TITLES[source_id], attribution=(f"Fuente: https://ebible.org/bible/details.php?id={source_id}\n"
                "Conversión de formato VPL a JSON UTF-8, sin reescribir el texto de los versículos.\n" + notes + "\n".join(notice.parts)), verses=verses)


def validate(document):
    seen = set()
    chapters = set()
    for item in document["verses"]:
        assert item["book"] in BOOKS and item["chapter"] > 0 and item["verse"] > 0 and item["text"].strip()
        assert "\ufffd" not in item["text"], "Unicode replacement character in source"
        last = item.get("endVerse", item["verse"])
        assert item["verse"] <= last <= item["verse"] + 1000
        for number in range(item["verse"], last + 1):
            key = (item["book"], item["chapter"], number)
            assert key not in seen, f"Overlapping reference: {key}"
            seen.add(key)
        chapters.add((item["book"], item["chapter"]))
    assert len({item["book"] for item in document["verses"]}) == 66
    assert len(chapters) == 1189, f"Unexpected chapter count: {len(chapters)}"
    return len(seen)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archives", type=Path, required=True)
    parser.add_argument("--local-ewb", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--skip-invalid", action="store_true", help="Report invalid sources and continue with valid translations")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    inputs = [(args.archives / f"{key}_vpl.zip", lambda path, key=key: ebible(path, key)) for key in TITLES]
    inputs += [(args.local_ewb / file, lambda path, title=title: local_ewb(path, title)) for file, title in
               (("nvi.ewb", "Nueva Versión Internacional (NVI)"), ("lbla.ewb", "La Biblia de las Américas (LBLA)"))]
    manifest = []
    for path, convert in inputs:
        try:
            document = convert(path)
            count = validate(document)
        except (ValueError, AssertionError, UnicodeError) as error:
            if not args.skip_invalid:
                raise
            manifest.append(dict(source=str(path.resolve()), error=str(error)))
            print(json.dumps({"skipped": path.name, "error": str(error)}, ensure_ascii=True))
            continue
        output = args.output / (path.stem.replace("_vpl", "") + ".json")
        output.write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
        manifest.append(dict(name=document["name"], file=str(output.resolve()), records=len(document["verses"]),
                             coveredVerses=count, source=str(path.resolve()), sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                             jsonSha256=hashlib.sha256(output.read_bytes()).hexdigest()))
        print(json.dumps({"name": document["name"], "records": len(document["verses"]), "coveredVerses": count}, ensure_ascii=True))
    (args.output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
