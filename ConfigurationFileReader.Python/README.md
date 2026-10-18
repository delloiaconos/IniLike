# ConfigurationFileReader for Python

A standalone Python implementation of the IniLike C# library. It supports the
same stored block types, aliases, comment rules, includes, parameter operations,
explicit read results, and validated saving. Runtime dependencies: **Python 3.9+
standard library only**. No .NET runtime is needed to use the package.

## Installation

From the repository root:

```sh
python3 -m pip install ./ConfigurationFileReader.Python
```

The package uses setuptools for building. It can also be used directly without
installation by adding `ConfigurationFileReader.Python` to `PYTHONPATH`.

```python
from configuration_file_reader import ConfigurationFileReader

config = ConfigurationFileReader("examples/bench.ini")
name = config.getParameter("BENCH", "name", "Unnamed")
enabled = config.getParameter("BENCH", "enabled", False)
found, rows = config.tryGetTable("TEST_PHASES")
if found:
    for row in rows:
        print(row)
```

`ConfigurationFile` is an alias for `ConfigurationFileReader`, for callers porting
C# code. Methods retain camelCase names.

## Construction and input

```python
from pathlib import Path
from configuration_file_reader import ConfigurationFile

memory = ConfigurationFile()
config = ConfigurationFile(Path("examples") / "bench.ini")
config = ConfigurationFile(Path("examples"), "bench.ini")
config = ConfigurationFile("examples/bench.ini", CaseSensitive=False)

with open("examples/bench.ini", "rb") as source:
    config = ConfigurationFile(source)
    assert not source.closed
```

Paths may be strings or `os.PathLike` objects. File paths are made absolute at
construction. `ConfigurationFile(False)` creates an empty case-insensitive
registry; `ConfigurationFile(path, False)` selects that mode for loading.
`CaseSensitive` is read-only. Missing paths raise `ConfigurationError` with the
underlying I/O exception as `__cause__`.

Binary and text streams are read from their current position and remain open,
including after a loading error. Binary streams detect UTF-8/16/32 BOMs; without
a BOM, UTF-8 is assumed. Text streams are already decoded by the caller. LF,
CRLF and CR are supported, including on non-seekable streams. Invalid encoded
characters in binary input are replaced, following the C# StreamReader policy.
Stream-created registries have no original save destination, even for file streams.

## Format

The [IniLike format reference](../README.md#file-format) describes the shared rules:

- SECTION/SEC/plain headers, TABLE/TBL/TAB, LIST/LST, DICT/DICTIONARY, TEXT/TXT,
  and ENCODED/ENC/BASE64/B64 have separate name spaces.
- END is optional, accepts an optional matching name, and never creates a section.
  A new header or EOF also ends a block. Header keywords remain uppercase.
- Section and dictionary entries split only at the first `=`. Trailing `;`, `,`
  and `.` are literal data. Surrounding whitespace is trimmed outside TEXT.
- `##` starts a comment anywhere in headers or data. Raw TEXT content preserves
  comment-looking text; its header and END lines still accept comments.
- TABLE/LIST store row strings. They ignore blank/comment-only rows and interpret
  bracketed rows as headers. The caller supplies the column separator and validates columns.
- INCLUDE/INC/INPUT/LINK/LOAD paths resolve from the working directory, including
  nested includes. Included blocks share registries but not active parser state.
  Duplicate names, cycles, and nesting beyond 128 active files are rejected.
- Names are case-sensitive by default. Insensitive matching preserves original
  spelling and does not alter values. Listings and serialized keys use UTF-16
  ordinal order, matching C# ordering rather than locale sorting.

`parComment` and `parSeparator` are read-only properties exposing mutable lists
(defaults `["##"]` and `["="]`). Changing them after construction does not reload
input. There is no public reload operation.

## Presence and collection access

| Block | Names | Presence | Read with explicit result | Existing getter |
| --- | --- | --- | --- | --- |
| Section | `listSections()` | `containsSection(name)` / `checkSection(name)` | `tryGetSection(name)` | Parameter methods below |
| Table | `listTables()` | `containsTable(name)` | `tryGetTable(name)` | `getTable(name)` |
| List | `listLists()` | `containsList(name)` | `tryGetList(name)` | `getList(name)` |
| Dictionary | `listDictionaries()` | `containsDictionary(name)` | `tryGetDictionary(name)` | `getDictionary(name)` |
| Text | `listTexts()` | `containsText(name)` | `tryGetText(name)` | `getText(name)` |
| Encoded | `listEncoded()` | `containsEncoded(name)` | `tryGetEncoded(name, Encoded=False)` | `getEncoded(name, encoded=False)` |

`tryGet*` methods return `(True, value)` for existing blocks, **including empty
ones**, and `(False, None)` for missing blocks. Presence/read methods never create
entries or save. Null/non-string names raise `TypeError`; empty string names are valid.
INCLUDE directives are not stored blocks; query the imported blocks instead.

Table/list/dictionary getters expose live collections. Missing table/list getters
return detached empty lists; missing dictionary getters return a detached empty
mapping. Dictionaries and section snapshots implement `collections.abc.MutableMapping`,
including indexing, iteration, `get`, `update`, and `copy`, with the selected case
comparison. A section read returns an independent snapshot. Name listings are
independent sorted lists.

Text getters return lines joined with LF, or `""` when missing. Encoded getters
return immutable Python `bytes`, or `b""` when missing. With `Encoded=True` (try-read)
or `encoded=True` (getter), the result is canonical Base64 ASCII bytes. Mutating
live collections changes memory only; call `save()` to persist those edits.

## Parameters and Python type selection

```python
config.autoUpdateRegistry = True
config.setParameter("SERVER", "port", "8080")
config.addParameter("SERVER", "host", "localhost")

port = config.getParameter("SERVER", "port", 80)
found, port = config.tryGetParameter("SERVER", "port", int)
present = config.containsParameter("SERVER", "port")
keys = config.listParameters("SERVER")
all_keys = config.listParameters()
```

`getParameter(section, key, DefaultVal, ValueType=None)` infers the requested type
from the default. `tryGetParameter(section, key, ValueType=str)` defaults to a
string read. There are no C# overloads or `out` arguments in Python:

| Python selector | Explicit string selector | C# equivalent |
| --- | --- | --- |
| `str` | `"string"` | string |
| `bool` | `"bool"` | bool |
| `int` | `"int"` | 32-bit int |
| — | `"long"` | 64-bit long |
| `float` | `"double"` | 64-bit double |
| — | `"float"` | 32-bit float |

For example, `config.getParameter("S", "n", 0, "long")` selects the 64-bit integer
getter. Parameter/dictionary storage accepts strings or `None`, as C# accepts
string/null. Automatic defaults are formatted as strings before storage.

Legacy getters retain C# behavior: missing values return defaults, an existing
null returns a typed default, unrecognized booleans return false, and the int
getter casts a parsed 64-bit integer to a wrapping 32-bit result. Decimal commas
are normalized to periods. Float conversion can overflow to infinity.

Typed try-reads return false with zero/false for missing, malformed or out-of-range
values. They reject NaN and infinity. A stored `None` raises
`NullParameterValueException` with `SectionName` and `ParameterName`. String
try-reads return `(True, None)` for a present null and `(False, None)` when absent.
`containsParameter` reports a stored null as present. No try-read stores defaults.

`autoUpdateRegistry` and `autoSaveRegistry` default to false. When automatic update
is enabled, parameter getters/setters/additions create missing sections and keys.
`addParameter` never overwrites an existing entry. `setParameter` updates existing
entries even with automatic update disabled. Automatic save runs only for new
entries and targets the original file. Failure rolls back the new key and its
newly created section. Enabling a flag does not create or save anything.

## Dump and save

```python
with config.dump() as output:
    text = output.read()  # StringIO, positioned at zero

with open("dump.ini", "w", encoding="utf-8", newline="") as writer:
    config.dump(writer)  # Writes and flushes; leaves writer open

config.save()                         # Original source file
config.save("copy.ini")
config.save(Path("backups"), "copy.ini")
```

`dump()` returns a caller-owned **text `StringIO`**, replacing C#'s memory-backed
StreamWriter. A supplied writer must accept text. Output uses LF, sorted block
names and keys, original row order, canonical headers, and no added punctuation.
Dumping writes raw registry data without round-trip validation.

`save()` writes UTF-8 without BOM to a temporary file beside the destination,
reloads it with the same case mode, compares all data, and uses `os.replace` only
after validation succeeds. Existing destinations survive serialization/validation
failures and temporary files are removed. Parent directories must already exist.
Replacement follows platform filesystem and permission rules. Alternate destinations
do not change the original path. Memory/stream registries always need a destination.
Comments and formatting outside TEXT are not preserved; includes are flattened.

## Errors and compatibility boundaries

Loading errors raise `ConfigurationError`, a `ValueError` subclass, with
`FilePath`, `FileName`, `LineNumber`, and the original exception in `__cause__`.
Nested includes retain the innermost location and add the parent include locations
to the message. File-open errors use line zero; Base64 errors use the block header's
line. Argument errors use Python `TypeError`/`ValueError`. Save validation raises
`ValueError`; filesystem failures during saving propagate as `OSError` subclasses.

This port uses Python's Unicode tables and simple, non-expanding uppercase mapping
for insensitive names. Unusual Unicode case pairs and whitespace classifications
can differ from a particular Mono/.NET version. ASCII names and the Unicode
fixtures in the test suite are compared against C#. Floating-point formatting
and extreme rounding may also vary between runtimes; Python floats are binary64.
Explicit float conversion rounds to binary32. No claim of binary or exception-type
identity with .NET is made. `None` as the constructor source creates an empty
registry; unlike a typed C# null path, it is not a filename error.

There is no locking or concurrent-mutation guarantee. This project implements the
library; the existing C# validator remains the command-line application.

## Tests

From the repository root:

```sh
make test-python
# Equivalent direct command:
python3 -B -m unittest discover -s ConfigurationFileReader.Python/tests -v
```

Tests use the standard library and temporary directories. When `mcs` and `mono`
are available, additional tests compile the current C# library and compare canonical
dumps for every example, parser acceptance, typed conversion results, and files
saved by Python. Those cross-language tests are explicitly skipped otherwise.
The C# regression suite remains available through `make test`.

## License

GNU Lesser General Public License, version 3 (LGPL-3.0).
See [LICENSE.md](LICENSE.md), copied from the repository license for standalone packaging.
