# Musicolet Backup Format & Decode/Encode Guide

This document describes the file structure, formats and layout of a **[Musicolet](https://github.com/kunalkadam/Musicolet)**
backup bundle, and gives step-by-step instructions for decoding it and encoding
(edited) files back. All facts below were verified against a real backup
(`musiclet-2026-10-11 19-38-47`, app version **540**) with a lossless full
roundtrip of every file.

A working decoder for this exact format lives in
[`tools/musiclet-decode/decode.py`](../tools/musiclet-decode/decode.py).

## How to get a backup

In Musicolet: **Settings → Backup/Restore → Backup Now**. The app writes a
folder (which it also ships as a zip) into the app's data directory. A folder
or zip contains ~51 files (see inventory below). Musicolet also stores the
same (still encrypted) files live in the `Musicolet_Data` folder on storage.

## The obfuscation layer

Musicolet "encrypts" every file (except `hash`) with:

- Cipher: **Blowfish**, mode **ECB** (8-byte blocks)
- Key: the ASCII string **`JSTMUSIC_2`**
- Padding: **PKCS-style** — the plaintext is extended to a multiple of 8 bytes
  with `n` bytes of value `n` (`n` = 1..8; a full padding block is `08 08 08 08 08 08 08 08`).

Consequences that are diagnostic gold when identifying this format in the wild:

| Ciphertext pattern                        | Plaintext                              |
| ----------------------------------------- | -------------------------------------- |
| `B8 C3 DB 2C FF B6 D5 A3` repeating        | 8 zero bytes (SQLite zero-filled pages) |
| `68 66 06 2B 3B E4 CD 3C` (as final block) | full 8-byte padding block              |

Empty tables (e.g., `PCs_Y_2016..2022`) are literally one 8-byte padding block.
Because ECB encrypts equal plaintext blocks to equal ciphertext blocks, a hex
dump full of repeated 8-byte patterns is the fingerprint of this scheme —
there is no per-file salt or IV.

## Bundle layout

A backup is either a folder or a zip of that folder (flat, files at zip root,
no subdirectory). Layout:

```
0.musicolet.backup    manifest (JSON)         app version, timestamp, md5 checksums
hash                  32-hex ASCII, no EOL    md5 of the manifest's unpadded plaintext
DB_SONGS_LOG          SQLite (encrypted)      full library: paths, titles, artists, play counts...
DB_BDN                SQLite (encrypted)      TABLE_DEVICES: per-device backup registry
PCs_W_2026.40         SQLite (encrypted)      play counts, week 40 of 2026
PCs_M_2025.9 …        SQLite (encrypted)      play counts, months (PCs_M_<year>.<month 1-12>)
PCs_Y_2016 …          SQLite (encrypted)      play counts, years (PCs_Y_<year>)
<playlist name>.mpl   JSON (encrypted)        one file per playlist, 13..16 present
0.favs                JSON (encrypted)        favourites, same shape as .mpl
0.qstk                JSON (encrypted)        saved-queue stack
0.names               Java serialization      ArrayList<String> of playlist names
PP, RPN, SSP, USP     JSON (encrypted)        app preferences (4 pref groups)
OMV                   JSON (encrypted)        empty JSON array [] + padding
```

### Integrity chain

Both checksums are md5 over the **unpadded decrypted plaintext**:

- `0.musicolet.backup` contains `"md5": { "<filename>": "<md5>", ... }` for
  every other file (49 entries).
- The `hash` file = `md5(unpad(decrypt(0.musicolet.backup)))`.

Musicolet refuses to restore a backup whose checksums don't match — see
[Encoding back](#encoding-back).

## File formats

### Manifest — `0.musicolet.backup`

Compact JSON (no whitespace; Android `org.json`):

```json
{"v":540,"time":1791743935109,
 "md5":{"0.names":"770f2236bceebdc9aa814d9e82831862", ...},
 "equalizer":{"numberOfBands":5,"bandLevelRange0":-1500,"bandLevelRange1":1500},
 "db_file":"DB_SONGS_LOG","DB_BDN":"DB_BDN",
 "pcs":["PCs_M_2025.11", ...]}
```

- `v` — app version that wrote the backup; `time` — creation epoch **milliseconds**.
- `pcs` — list of all `PCs_*` period files that belong to this backup;
  `db_file`/`DB_BDN` — names of the library and device databases.
- `md5` — per-file checksums used during restore verification.

### SQLite family — `DB_SONGS_LOG`, `DB_BDN`, `PCs_*`

All decrypt to standard SQLite 3 files (header `SQLite format 3\0`).

**`DB_SONGS_LOG.TABLE_SONGS`** — the library; one row per song:

| Column             | Type / unit | Meaning                                                       |
| ------------------ | ----------- | ------------------------------------------------------------- |
| `COL_PATH`         | string      | `musicolet://media-store?p_v=<vol>&p_rp=<folder-uri>&p_dn=<fileuri>&p_id=<id>&p_mt=1` |
| `COL_TITLE`        | string      | track title (without extension)                                |
| `COL_ARTIST`       | string      | artist list, `/`-separated                                     |
| `COL_ALBUM`        | string      | album                                                          |
| `COL_GENRE`        | string      | genre                                                          |
| `COL_COMPOSER`     | string      | composer                                                       |
| `COL_YEAR`         | int         | year                                                           |
| `COL_DURATION`     | ms          | duration                                                       |
| `COL_TRACK_NO`     | int         | track number                                                   |
| `COL_DATE_ADDED`   | epoch ms    | added to library (may be refreshed on rescan)                  |
| `COL_DATE_MODIFIED`| epoch ms    | file mtime                                                     |
| `COL_NUM_PLAYED`   | int         | lifetime play count                                            |
| `COL_LAST_PLAYED`  | epoch ms    | last play (0 = never)                                          |
| `COL_LASTPOS`      | ms          | resume position                                                |
| `album_artist`     | string      | album artist (lower-case column name)                          |
| `COL_NUM_PLAYED_W` | int         | play count, current week                                       |
| `COL_NUM_PLAYED_M` | int         | play count, current month                                      |
| `COL_NUM_PLAYED_Y` | int         | play count, current year                                       |
| `COL_VIS_FLAGS`    | bitmask     | observed values: `3073` (normal), `3137` (`3073 \| 64`), `64`    |
| `COL_RATING`       | int         | star rating                                                    |
| `COL_ENDPOINTS`    | string      | loop/start endpoints (JSON)                                    |
| `COL_GAINS`        | string      | per-song equalizer gain                                        |
| `COL_LOGPATH`      | string      |                                                                |
| `COL_EIJ`          | string      |                                                                |

Notes:
- The `p_id` inside `COL_PATH` URIs is Musicolet's internal song id. It is
  **not** the SQLite `rowid` of this table (rowids are 1..N; `p_id` values span
  a larger key space). Playlist entries reference songs by an identical URI,
  so cross-referencing playlist entries ↔ library rows is done by matching the
  URI (or the `p_dn` file name).
- In one real backup, `COL_VIS_FLAGS = 3073` for every visible song, `3137` for
  58 songs and `64` for one; the app reports only the `3073` group as songs.

**`DB_BDN.TABLE_DEVICES`** — columns `COL_ID` (int), `COL_NAME` (string),
`COL_TYPE` (int, 0 observed): the registry of devices (Bluetooth/audio output
devices seen, e.g. `BZR_21_TD15E`).

**`PCs_W_<year>.<week>` / `PCs_M_<year>.<month>` / `PCs_Y_<year>`** — one table
`TABLE_SONGS` with columns `COL_PATH`, `COL_NUM_PLAYED`: plays **originating**
in that period, keyed by the same URI scheme. Years with no plays are a lone
8-byte padding block instead of a database.

### Playlist family — `<name>.mpl` and `0.favs`

Compact JSON; **parallel arrays** — index `i` in each array is one song:

```json
{"S_P":  ["musicolet://media-store?p_v=primary&p_rp=Music%2F2026&p_dn=The%20Bridge%20-%20Caroline%20Jones.mp3&p_id=23739&p_mt=1", ...],
 "S_T":  ["The Bridge", ...],
 "S_AL": ["Album Name", ...],
 "S_AR": ["Caroline Jones", ...],
 "S_D":  [194638, ...]}
```

- `S_P` = song URI, `S_T` = title, `S_AL` = album, `S_AR` = artist,
  `S_D` = duration (**ms**). `0.favs` (favourites) uses the same shape.
- Array order = playlist order. In practice the last entry is the most
  recently added song.
- `S_T/S_AL/S_AR` are optional on some entries (arrays may be shorter).

JSON string escaping is Android `org.json`-style: standard `"`,`\`, and
control-character escapes, **plus `\u003d`, `\u0026`, ... for `= & < > '`
inside strings** (HTML-safety escapes), while other non-ASCII characters stay
as raw UTF-8 (e.g. `é` → `c3 a9`). Any standards-compliant JSON parser reads
these files; the escaping style does not need to be reproduced byte-for-byte
when re-encoding (`encode-then-restore` only needs valid JSON + matching md5s).

### Queue stack — `0.qstk`

Compact JSON:

```json
{"S0_CPQ": 7,
 "S0_PQ": [ {"S0_PQ_T": "The Interrupters",   // queue label
             "S0_PQ_CPS": 17,                 // current position in the queue
             "S0_PQ_LKP": 160352,             // last-known position (ms)
             "S0_PQ_OQS": {"S_P": [...], "S_T": [...], ...}}, ...]}
```

`S0_CPQ` = which entry in `S0_PQ` is the currently active queue; each queue's
songs use the same parallel-array shape as playlists.

### Preferences — `PP`, `RPN`, `SSP`, `USP`

Compact JSON arrays of typed key/value pairs:

```json
[{"key":"k_b_qsgn1","value":true,"type":"Boolean"},
 {"key":"I_K_SRTBYF_UPL_Mood - Classics","value":-1,"type":"Integer"},
 ...]
```

- `PP` — main preferences (131 entries; `k_*` keys plus per-playlist view/sort
  keys like `I_K_SRTBYF_UPL_<playlist>`).
- `RPN` — 3 entries (`R_SM`, `RSM`, `RQM`).
- `SSP` — search query history: one key `SQH` with a stringified JSON array
  (`"[\"linkin park\",\"faun\",...]"`).
- `USP` — 17 entries incl. `FID` (epoch-ms install fingerprint), `LV`, and
  misc booleans.
- Types: `Boolean`, `Integer`, `Long`, `String`.

### Playlist names — `0.names`

Java object serialization (magic `ac ed 00 05`), an `ArrayList<String>`:

```
ac ed 00 05                        stream magic/version
73 72 00 13 "java.util.ArrayList"  TC_OBJECT TC_CLASSDESC ...
78 81 d2 1d 99 c7 61 9d            serialVersionUID
03 00 01 49 00 04 "size" ... 70    class fields (one int "size"), TC_ENDBLOCKDATA-free
00 00 00 10                        list size = 16
77 04 00 00 00 10                  TC_BLOCKDATA: the size int again
74 00 07 "Anthems"                 TC_STRING (2-byte length, modified-UTF8) per entry
74 00 08 "Clarinha" ...            (16 names, in playlist order)
78 02 02                           TC_ENDBLOCKDATA + PKCS padding
```

The 16 entries are the playlist names in list order, mirroring the `.mpl` files.

### `OMV` and `hash`

- `OMV` — decrypts to the two characters `[]` (an empty JSON array, "other
  media volumes") + padding.
- `hash` — plain ASCII md5 of the manifest's plaintext, used as a top-level
  bundle checksum.

## Decoding

With the tool in this repository (requires `pycryptodome`). The tool takes a
**folder**; for a zip, unzip first (the layout is flat):

```bash
pip install pycryptodome

unzip "musiclet-2026-10-11 19-38-47.zip" -d backup
python3 tools/musiclet-decode/decode.py backup --out out --raw
```

Outputs: `library.json`, `playlists.json`, `playlists/*.m3u`, `queues.json`,
`names.json`, `settings.json`, `counts/*.json`, `decrypted/` (raw plaintexts
with `--raw`), and `report.md` (includes md5 verification).

Minimal manual decode (single file):

```python
from Crypto.Cipher import Blowfish

data = open("DB_SONGS_LOG", "rb").read()
plain = Blowfish.new(b"JSTMUSIC_2", Blowfish.MODE_ECB).decrypt(data)
n = plain[-1]
plain = plain[:-n]              # PKCS-style trim (n in 1..8)
open("DB_SONGS_LOG.db", "wb").write(plain)
```

## Encoding back

Everything is symmetric — a decrypted, edited file can be re-encrypted and
restored, provided the checksums are rebuilt. Full recipe:

```python
from Crypto.Cipher import Blowfish
import hashlib, json, os

KEY = b"JSTMUSIC_2"

def encrypt(data: bytes) -> bytes:
    pad = 8 - len(data) % 8          # 1..8, PKCS-style; never omit padding
    return Blowfish.new(KEY, Blowfish.MODE_ECB).encrypt(data + bytes([pad]) * pad)

def decrypt_file(path):
    data = Blowfish.new(KEY, Blowfish.MODE_ECB).decrypt(open(path, "rb").read())
    n = data[-1]
    return data[:-n]

def md5_of_plaintext(path) -> str:
    return hashlib.md5(decrypt_file(path)).hexdigest()
```

Steps:

1. **Edit the plaintexts.** Decrypt a file, modify it, and keep the plaintext
   (unpadded!) aside. Editing rules worth knowing:
   - SQLite files: edit with any SQLite tool (e.g. `sqlite3`), e.g. rewrite
     `COL_NUM_PLAYED` values in `DB_SONGS_LOG`.
   - JSON files: keep them **compact** and *valid*; array order = playlist
     order; parallel arrays in playlists must stay index-aligned.
   - Empty tables stay a single 8-byte padding block (`08`×8 encrypted) — if
     you add data to `PCs_Y_2016`, write a real SQLite file instead.
   - Musicolet parses these on restore (the manifest md5s are the only
     byte-exactness requirement), but keep the overall shapes above.
2. **Rebuild the manifest.** For every file, compute
   `md5(plaintext-without-padding)` and write it into the `md5` map of
   `0.musicolet.backup`. Keep the rest of the manifest (`v`, `time`,
   `equalizer`, `db_file`, `DB_BDN`, `pcs`) — `pcs` must list exactly the
   `PCs_*` files present.
3. **Encrypt everything**: every content file *and* the manifest itself are
   encrypted with the same scheme. Files that decrypt to
   `len % 8 == 0` still receive a full padding block.
4. **Write `hash`**: plain ASCII `md5(manifest plaintext)` + newline is fine
   (the original has no trailing newline; it is not verified to have one).
5. **Zip (optional)**: `zip` the folder with the files at the zip root.
   The zip must contain only the ~51 files above — no extra entries.

(The `apply_edits_and_unpad` / `plaintext_of` helpers stand for your editing
step plus `unpad(decrypt_file(...))`; the md5 is always over the **unpadded**
plaintext that `encrypt()` receives.)

Sketch of the complete repack:

```python
backup_dir = "backup_edited"
manifest = json.loads(decrypt_file(os.path.join(backup_dir, "0.musicolet.backup")))
md5s = {}
for f in os.listdir(backup_dir):
    if f in ("hash",) or f.endswith(".zip"):
        continue
    raw = open(os.path.join(backup_dir, f), "rb").read()
    if f == "0.musicolet.backup":
        continue
    md5s[f] = hashlib.md5(apply_edits_and_unpad(f, raw)).hexdigest()  # from step 1
manifest["md5"] = md5s
manifest["pcs"] = [f for f in md5s if f.startswith("PCs_")]
manifest_bytes = json.dumps(manifest, ensure_ascii=False,
                            separators=(",", ":")).encode("utf-8")
open(os.path.join(backup_dir, "0.musicolet.backup"), "wb") \
    .write(encrypt(manifest_bytes))
open(os.path.join(backup_dir, "hash"), "w") \
    .write(hashlib.md5(manifest_bytes).hexdigest())
for f in md5s:
    path = os.path.join(backup_dir, f)
    open(path, "wb").write(encrypt(unpad(plaintext_of(f))))
```

The roundtrip `decrypt → unpad → encrypt(pad) → compare` was verified bitwise
for all 51 files of a real backup, and both checksum layers (per-file md5 map:
49/49; `hash` file: exact match) reproduce deterministically from plaintexts.

## Reference

- Decryption key/public parser originally documented by
  [`TzurSoffer/Musicolet_DB_To_Spotify`](https://github.com/TzurSoffer/Musicolet_DB_To_Spotify)
  (`DB_SONGS_LOG` = Blowfish-ECB `JSTMUSIC_2` → SQLite).
- Local tooling: [`tools/musiclet-decode/decode.py`](../tools/musiclet-decode/decode.py).