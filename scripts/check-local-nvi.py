"""Check the authorized local NVI conversion without writing or printing scripture."""
import hashlib
from pathlib import Path
import runpy
import struct
import sys
import zlib

module = runpy.run_path(str(Path(__file__).with_name("prepare-spanish-bibles.py")))
source = Path(sys.argv[1])
data = source.read_bytes()
raw = []
for index, book in enumerate(module["BOOKS"]):
    header = 88 + index * 224
    start = struct.unpack_from("<I", data, header + 208)[0]
    length = struct.unpack_from("<I", data, header + 216)[0]
    block = zlib.decompress(data[start:start + length - 10])
    try:
        text = block.decode("utf-8")
    except UnicodeDecodeError:
        text = block.decode("cp1252")
    raw += [module["verse"](book, *line.split(" ", 1)) for line in text.splitlines() if line]

document = module["local_ewb"](source, "Nueva Versión Internacional (NVI)")
assert len(document["verses"]) == 31042
assert module["validate"](document) == 31071
assert not any(ord(c) < 32 and c not in "\n\r\t" for verse in document["verses"] for c in verse["text"])

def rows(items, book, chapter, verse):
    return [row for row in items if (row["book"], row["chapter"], row["verse"]) == (book, chapter, verse)]

converted = document["verses"]
converted_rows = {tuple(sorted(row.items())) for row in converted}
for row in raw:
    book, chapter, number = row["book"], row["chapter"], row["verse"]
    if book == "Abdías" or (book, chapter, number) in {
        ("Números", 26, 43), ("Números", 26, 51), ("1 Samuel", 13, 15)
    }:
        continue
    assert tuple(sorted(row.items())) in converted_rows, f"Unrelated source row changed: {book} {chapter}:{number}"
assert rows(converted, "Números", 26, 43) == rows(raw, "Números", 26, 43)[:1]
assert rows(converted, "Números", 26, 51)[0]["text"] == rows(raw, "Números", 26, 51)[0]["text"]
assert rows(converted, "Números", 26, 52)[0]["text"] == rows(raw, "Números", 26, 51)[1]["text"]
assert rows(converted, "1 Samuel", 13, 15)[0]["text"] == " ".join(
    row["text"] for row in rows(raw, "1 Samuel", 13, 15))
assert [r for r in converted if r["book"] == "Abdías"] == [r for r in raw if r["book"] == "Abdías"][2:23]
restored = rows(converted, "Números", 26, 50)
assert len(restored) == 1 and not rows(raw, "Números", 26, 50)
assert module["NVI_RESTORED_VERSE_SOURCE"] in document["attribution"]
try:
    module["repair_local_nvi"](raw, "0" * 64)
except ValueError:
    pass
else:
    raise AssertionError("Repair accepted an unverified source fingerprint")
print(f"PASS local NVI: 66 books, 1189 chapters, 31042 records, 31071 coordinates; source SHA256 {hashlib.sha256(data).hexdigest()}")
