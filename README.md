# IniLike

IniLike is a standalone C# library for reading INI-like configuration files organized into typed blocks.
It targets .NET Framework 3.5. 
The namespace and assembly name are **ConfigurationFilesReader**, and the public class is `ConfigurationFilesReader.ConfigurationFile`.

Start with [Usage](#usage), then use the references for [loading and includes](#including-other-files),
[source locations](#source-locations-for-parsed-elements), [collection access](#api-reference),
[typed dictionary values](#typed-dictionary-values), [read-only views and snapshots](#read-only-views-and-immutable-snapshots),
and [saving](#saving). Build and test instructions are [below](#build).
Console usage is in the [ConfigurationValidator guide](ConfigurationValidator/README.md).

## File format

A **block** is the general unit introduced by a bracketed header. A **section** is
specifically a key/value block (`[NAME]`, `[SEC:NAME]` or `[SECTION:NAME]`). Tables,
lists, dictionaries, text, encoded data and includes are other block types.

```ini
## Full-line comment
[SERVER]
host = localhost
port = 8080
enabled = TRUE

[DIRECTORIES]
data = ./data/

[TABLE:ITEMS]
first; 10; active
second; 20; inactive
```

- `[NAME]`, `[SEC:NAME]` and `[SECTION:NAME]` are equivalent headers for section blocks
  containing `key = value` pairs.
- `[TABLE:NAME]`, `[TBL:NAME]` and `[TAB:NAME]` are equivalent table headers containing text rows.
  The library returns a `List<string>`;
  callers are responsible for splitting rows into columns using their specified
  column separator. Separators such as `;` are preserved, including at row edges.
  Blank or whitespace-only rows and rows starting with a `parComment` prefix
  after trimming are intentionally ignored, so they do not occupy table rows.
  A bracketed row is interpreted as a block header and ends the current table;
  literal bracketed rows cannot be escaped.
- `[LIST:NAME]` and `[LST:NAME]` are equivalent list headers. Every data line is
  one string item: `mach1;` remains `mach1;`, and `x1=mach1.volt1` remains the
  complete string `x1=mach1.volt1`. Items are not parsed as key/value pairs;
  order, punctuation and duplicates are preserved. Lists use the same whitespace
  trimming and comment rules as tables.
- `[DICT:NAME]` and `[DICTIONARY:NAME]` are equivalent dictionary headers.
  Entries are string keys and string values: `x = 3` stores `"3"`, and
  `y = 3.14` stores `"3.14"`. They use the same key/value parsing, trimming and
  comment rules as sections, splitting entries at the first `=` and ignoring lines without `=`.
  Duplicate keys throw; case-only differences are distinct only with `CaseSensitive=true`.
- `[TEXT:NAME]` and `[TXT:NAME]` are equivalent plain-text block headers.
  Content supports Unicode and preserves whitespace, blank lines, `##` comment-looking
  lines, `=` and trailing punctuation. Any bracketed header starts a new block,
  just as in lists; `[END]` or `[END:NAME]` closes the text block. Header recognition
  uses trimmed lines, so bracketed lines cannot be stored as literal text.
  Text blocks have their own name space, using the selected case mode. `getText(name)` returns
  their lines joined with `\n` (without an added final newline); original CR/LF
  line-ending style is not retained. Empty and missing blocks return `""`;
  `listTexts()` distinguishes an existing empty block from a missing one.
- `[ENCODED:NAME]` and `[ENC:NAME]` contain one Base64-encoded binary payload.
  `[BASE64:NAME]` and `[B64:NAME]` remain accepted aliases for the same block type.
  Data may span multiple lines, even within a four-character group. Blank lines
  and comments are ignored; spaces, tabs and line breaks within the
  encoded data are accepted. Padding `=` is retained and punctuation is not
  stripped. Standard Base64 decoding uses `Convert.FromBase64String`; malformed
  input (including URL-safe `-`/`_` variants) throws `FormatException` during loading,
  with the block name in the diagnostic. Empty blocks decode to an empty byte array.
  `getEncoded(name)` returns decoded bytes, without interpreting them as text.
  Encoded blocks have their own name space, using the selected case mode, and follow the same
  header transitions and optional `END` markers as lists.
- `[INCLUDE:filename.ini]` loads another configuration file into the current
  registry. `INC`, `INPUT`, `LINK` and `LOAD` are equivalent identifiers. Absolute
  paths are accepted; by default, relative paths (including nested includes) resolve from the
  process's current working directory, not the including file's directory.
  Filenames are trimmed but are not quoted or stripped of punctuation.
  Included files use the same parser settings and have independent block state;
  their contents cannot continue a block in the including file. All block types
  share their existing namespaces across files, so duplicate block names still throw.
  Include blocks are processed immediately and are not stored in a block registry. Following
  non-header lines are ignored until the next block, and `END` is optional.
  `[END:filename.ini]` must match the trimmed filename as written in the include
  header, using the selected case mode for label checks. Missing/unreadable files
  and parsing errors propagate. Empty filenames and include cycles throw
  `FormatException`; nesting is limited to 128 simultaneously open files to
  prevent unbounded recursion through filesystem aliases. Repeated non-recursive
  includes are processed again and are subject to normal duplicate checks.
- Any block may end with `[END]` or `[END:NAME]`. The closing name, when supplied
  for an active block, must match its name (after trimming); a mismatch throws
  `FormatException`. Closing markers do not create blocks or become block content. Subsequent
  data is ignored until another block header. A closing marker outside
  block also resets the parser to that state. Without a closing marker, the
  next block header or end of file ends the block. Saving uses the canonical
  `[NAME]`, `[TABLE:NAME]`, `[LIST:NAME]` and `[DICT:NAME]` forms without closing markers. Text blocks are saved as `[TEXT:NAME]` followed
  by raw content lines and `[END]`. Encoded blocks use `[ENCODED:NAME]` and a single
  canonical Base64 data line without delimiters or an end marker.
- Block names and section/dictionary keys are **case-sensitive by default**.
  Pass `false` for the constructor's `CaseSensitive` argument to ignore case. The `SEC:`, `SECTION:`,
  `TABLE:`, `TBL:`, `TAB:`, `LIST:`, `LST:`, `DICT:`, `DICTIONARY:`, `TEXT:`, `TXT:`, `ENCODED:`, `ENC:`, `BASE64:`, `B64:`, `INCLUDE:`, `INC:`, `INPUT:`, `LINK:`, `LOAD:` and `END` keywords
  must be uppercase. Sections, tables, lists, dictionaries, text and encoded blocks have separate name spaces and may
  share a name.
- Comments use the nonempty prefixes in `parComment` (default `##`), matched
  ordinally and case-sensitively. Outside raw TEXT content, the first matching
  prefix anywhere on a line and everything after it are removed before parsing.
  Whitespace before the comment is trimmed; a now-empty line is ignored.
  Both `[TABLE:T] ## description` and `[END:T] ## finished` are valid headers,
  and `key=value ## note` stores `value`. A separating space is not required.
  Null or empty prefix entries are ignored. Quotes do not protect comment prefixes.
  Raw TEXT content preserves comment-like text and whitespace, but block headers
  and END markers inside TEXT also accept trailing comments. For example,
  `[END] ## finished` ends TEXT, while `message ## literal` remains unchanged.
- Outside text content, leading and trailing whitespace is trimmed from lines, block names, keys,
  and values. Trailing `;`, `,`, and `.` are literal content in section and
  dictionary values, table rows and list items; they are never stripped.
- Section and dictionary entries split at the **first** `=` only. Additional
  `=` characters belong to the value: `expression = a=b;` stores `a=b;`.
  Lines without `=` are ignored; empty keys and values remain supported.
- Quoting, escaping and multiline parameter values are not supported.
  Quotes remain part of the value. A line such as `; comment` is treated as
  data inside a table.
- Duplicate block names within their respective name spaces, or duplicate keys
  within a section or dictionary, cause an exception. Repeated list items are allowed.
- Lines before the first block header are ignored.
- Relative configuration file paths are resolved against the working directory.
  Paths stored as values are returned as text without resolution.

Punctuation is preserved: `value...` remains `value...`, and a single `.` remains `.`.
For numeric and boolean values, write `port = 8080` and `enabled = TRUE`;
`8080;` and `TRUE;` are literal strings rather than valid numeric/boolean inputs.
The parser supports the format described here rather than a complete INI specification.

## Usage

Example configuration files are in [`examples/`](examples/):

| File | Demonstrates |
| --- | --- |
| [example.ini](examples/example.ini) | Basic parameters, numeric values and tables. |
| [sections.ini](examples/sections.ini) | Plain, `SEC` and `SECTION` headers; boolean `0`/`1`; optional named and unnamed `END`. |
| [tables.ini](examples/tables.ini) | `TABLE`/`TBL`, test phases, blank columns and empty tables. |
| [lists.ini](examples/lists.ini) | `LIST`/`LST`, plain items and literal assignments, repeated items and empty lists. |
| [punctuation.ini](examples/punctuation.ini) | Literal trailing semicolons, commas and periods in sections, dictionaries, tables and lists. |
| [dictionaries.ini](examples/dictionaries.ini) | `DICT`/`DICTIONARY`, string key/value entries and optional endings. |
| [texts.ini](examples/texts.ini) | Raw Unicode text, blank lines, punctuation and transitions using `TEXT`/`TXT`. |
| [encoded.ini](examples/encoded.ini) | `ENCODED`/`ENC`, legacy aliases, wrapped Base64 data and empty blocks. |
| [bench.ini](examples/bench.ini) | Combined sections, tables and lists, including shared names across block types. |

Load an example from the repository root with
`new ConfigurationFile("examples/bench.ini")`. Automatic creation and saving are
runtime properties configured in C#; they are not directives in the INI file.

Reference `ConfigurationFilesReader.dll` and use the public API:

```csharp
using ConfigurationFilesReader;
using System.Collections.Generic;

var config = new ConfigurationFile("config.ini") { autoUpdateRegistry = true };
string host = config.getParameter("SERVER", "host", "localhost");
int port = config.getParameter("SERVER", "port", 8080);
bool enabled = config.getParameter("SERVER", "enabled", false);
List<string> items = config.getTable("ITEMS");

// Create or update a parameter in memory. The source file is unchanged.
config.setParameter("DIRECTORIES", "data", "./other-data/");
```

To ignore case in block names and section/dictionary keys:

```csharp
var config = new ConfigurationFile("config.ini", false);
string host = config.getParameter("server", "HOST", "localhost");
```

Every constructor has an overload with a final `bool CaseSensitive` argument,
including `new ConfigurationFile(false)` for an empty registry. Existing overloads
use `true`. The public `CaseSensitive` property is read-only; choose the mode before
loading. The option applies to all block types, included configurations, key lookups,
updates, duplicate detection, distinct parameter listings and named `END` labels.
Matching uses ordinal comparison, independent of the current culture. Names that
differ only in case are duplicates in insensitive mode and still cause an exception
within the same namespace. Original spelling and values are preserved; listings
remain sorted ordinally. Keywords and comment prefixes keep their existing matching
rules, and include paths keep the filesystem's path rules. Save validation uses the
same mode; the option is not written into the file, so select it again when reloading.

To create a configuration entirely in memory:

```csharp
var config = new ConfigurationFile { autoUpdateRegistry = true };
config.setParameter("SERVER", "port", "8080");
int port = config.getParameter("SERVER", "port", 80);
```

To load directly from a readable stream:

```csharp
using (var stream = System.IO.File.OpenRead("config.ini"))
{
    var config = new ConfigurationFile(stream);
    string host = config.getParameter("SERVER", "host", "localhost");
    config.save("copy.ini");
}
```

`ConfigurationFile(Stream Source)` reads from the current position to the end,
using UTF-8 by default with BOM-based encoding detection, just like file loading.
Non-seekable streams are supported. The caller retains ownership: the stream stays
open on success and on errors, and its position is not restored. Null streams throw
`ArgumentNullException`; unreadable streams throw `ArgumentException`. Parsing and
I/O errors propagate to the caller.

A stream-loaded configuration has no original file, even when passed a `FileStream`.
Saving always requires an explicit destination; automatic creation with
`autoSaveRegistry=true` fails before modifying the registry. Both runtime properties
default to false. The same parser and file-format rules apply to files and streams.

### Including other files

```ini
[INCLUDE:filename.ini]

[INC:config2.ini]
[END]

[LINK:settings/common.ini]
[END:settings/common.ini]
```

By default, these paths resolve against the process's working directory.
Includes also work when constructing from a stream. Loaded content is available
through the existing getters and listing methods; there is no separate include
registry. Full paths can be used when the working directory is not fixed.

Pass `ConfigurationLoadOptions` after the source argument to select an INCLUDE
policy before loading. String paths, `FileInfo` and streams accept these options,
with an optional final `bool CaseSensitive`. Null options retain the defaults.
The root filename itself is resolved normally, independently of INCLUDE options.

```csharp
var options = new ConfigurationLoadOptions {
    IncludeBaseDirectory = @"/application/config"
};
var config = new ConfigurationFile("root.ini", options);
List<string> files = config.listLoadedFiles();
```

`IncludeBaseDirectory` is normalized to an absolute path when construction starts.
All relative includes, including nested ones, resolve against that same directory;
absolute includes remain absolute. Options are copied before loading. Without an
explicit base directory, relative paths retain the legacy working-directory behavior.
The existing `DirectoryInfo` constructor's base directory still applies only to the
root filename.

For custom mapping, set `IncludeResolver` to a
`Func<string, string, string>`. Its arguments are the trimmed INCLUDE filename and
the including file's absolute path (null for an anonymous stream). It returns a
nonempty file path. Relative results resolve against `IncludeBaseDirectory` when
provided, otherwise the working directory. The resolver runs for every directive,
including absolute filenames and nested includes. For paths relative to each
including file, for example:

```csharp
var options = new ConfigurationLoadOptions {
    IncludeResolver = (name, source) => System.IO.Path.Combine(
        source == null ? @"/application/config" : System.IO.Path.GetDirectoryName(source), name)
};
var config = new ConfigurationFile("root.ini", options);
```

An empty directive still throws `FormatException`; a null/empty resolver result
throws `ArgumentException`. Resolver exceptions retain their cause and the directive's
source/line diagnostics. Resolved paths use the existing duplicate, cycle and depth
checks; a resolver changes file selection, not parser rules or stream ownership.

`listLoadedFiles()` returns a sorted, independent `List<string>` snapshot of unique
absolute paths successfully parsed by this instance. It includes the root file for
path and `FileStream` sources, plus all transitive includes, including empty files.
Anonymous streams contribute only included files; empty in-memory configurations
return an empty list. Repeated paths are deduplicated using ordinal comparison
(case-insensitive on Windows), independently of `CaseSensitive`. Paths are normalized
lexically; symbolic links are not resolved to filesystem identities. Repeated includes
are still parsed and can cause duplicate-block errors. Construction failures throw
instead of exposing a partial configuration or dependency list. Save/dump does not
change this snapshot's source set or add output/validation temporary files. A saved,
flattened configuration loaded afresh tracks only its own input files.

Dump/save writes the combined data as ordinary blocks, replacing include directives
with their loaded content. It never writes back to the included files automatically.
The original save destination remains the top-level file; configurations loaded
from a stream still need an explicit destination. As with other save overloads,
explicitly choosing an included file as a destination replaces that file.

### Source locations for parsed elements

The immutable `SourceLocation` class identifies where an element was parsed:

| Property | Meaning |
| --- | --- |
| `SourceName` | Absolute filename by default, a supplied stream label, or `<stream>`. |
| `FilePath` | Absolute physical filename, or null for an anonymous/named in-memory stream. |
| `LineNumber` | One-based physical line number, including blank/comment lines. Stream lines count from the starting read position. |

These side-effect-free lookups are available on both `ConfigurationFile` and
`ConfigurationSnapshot`:

| Method | Location |
| --- | --- |
| `tryGetSectionSource(Name, out Location)` | Section header. |
| `tryGetTableSource(Name, out Location)` | TABLE header. |
| `tryGetListSource(Name, out Location)` | LIST header. |
| `tryGetDictionarySource(Name, out Location)` | DICT header. |
| `tryGetTextSource(Name, out Location)` | TEXT header. |
| `tryGetEncodedSource(Name, out Location)` | ENCODED header. |
| `tryGetParameterSource(Name, Key, out Location)` | Section parameter's key/value line. |
| `tryGetDictionaryValueSource(Name, Key, out Location)` | Dictionary entry's key/value line. |
| `tryGetTableRowSource(Name, Index, out Location)` | Table row's data line. |
| `tryGetListItemSource(Name, Index, out Location)` | List item's data line. |

All return true with a location when provenance is available, otherwise false
and null. Names/keys use the selected case mode; null names/keys throw
`ArgumentNullException`. Row/item indices are zero-based; negative or out-of-range
indices return false. Empty parsed blocks retain header locations. Ignored lines,
comments and END markers do not create metadata. INCLUDE is expanded rather than
stored: imported elements point to their own file and line, including nested includes.

```csharp
SourceLocation location;
if (config.tryGetDictionaryValueSource("CHANNELS", "x", out location))
{
    Console.WriteLine("{0}:{1}", location.SourceName, location.LineNumber);
}
```

Locations describe original declarations, not edit history. Existing section or
dictionary keys retain their declaration location when their value changes. A
removed dictionary key returns false; reusing an originally parsed key retains
that original declaration location. Newly created blocks/keys have no parsed
location. Existing live `List<string>` accessors cannot notify the library of
arbitrary edits: a row/item lookup succeeds only when the original string object
still occupies its original index. Appending rows preserves earlier locations;
new rows, replaced strings and rows shifted to other indices have no location at
those indices. Restoring the original string object/index restores its location.
Equal text in a newly allocated string does not establish provenance.

Snapshots retain an independent copy of provenance, with immutable location
objects, alongside their copied data. Edits to the original cannot change snapshot
locations. Creating a snapshot after edits preserves the same availability rules.
Saving does not reassign locations; reloading saved output assigns locations from
that new source. Source metadata is not serialized by dump/save.

To name an in-memory stream, set `ConfigurationLoadOptions.SourceName` before
constructing the configuration:

```csharp
var options = new ConfigurationLoadOptions { SourceName = "uploaded settings" };
var config = new ConfigurationFile(stream, options);
```

The label is preserved verbatim in element locations and syntax-error diagnostics.
Null selects the default; empty/whitespace-only labels throw `ArgumentException`.
This option applies only to the root stream. It can also override a `FileStream`'s
display name while retaining its physical `FilePath`. Included files keep their
own filenames. Labels never become filesystem paths, INCLUDE resolver source
arguments, dependency entries, or save destinations; anonymous streams still pass
null as the resolver's source-path argument. Stream ownership is unchanged.

### Loading diagnostics

Parser errors include the absolute source path (including filename) and a one-based
line number in the exception message. Blank lines and comments count as lines.
For example, a duplicate key can report `Source '/configs/settings.ini', line 5:`
followed by the original error message. Nested include failures retain the failing
file's location and add the locations of the include headers that led to it.
File-open errors report `before reading` instead of assigning a fictitious row.
Path constructors open the file directly: missing files throw `FileNotFoundException`,
missing parent directories throw `DirectoryNotFoundException`, and access or other
I/O failures retain their standard exception types, just as when opening a
`FileStream`. The diagnostic wrapper retains the original open exception as
`InnerException`; it does not replace I/O failures with a generic `Exception`.

The exception's `Data` dictionary also exposes the innermost error location:

| Key | Value |
| --- | --- |
| `FilePath` | Absolute physical path; for a stream without a filename, its supplied label or `<stream>`. |
| `FileName` | Physical filename without directories, or the stream label / `<stream>`. |
| `LineNumber` | One-based line number; `0` if the file could not be opened. |
| `SourceName` | Innermost failing source's display name, honoring a supplied stream label. |

A `FileStream` supplies its filename for diagnostics without becoming the original
save destination. Stream line numbers are counted from the starting read position.
Read failures identify the line being attempted. Base64 decoding failures identify
the encoded block's opening header, since the payload is decoded as a whole.
Common exception types (`FormatException`, `ArgumentException`, file-not-found,
directory-not-found, access-denied and I/O exceptions) are preserved, and the
original exception is retained as `InnerException`. These diagnostics apply to
loading, including the parser invoked by save's round-trip validation; API argument
checks without a source file do not have a file location.

## API reference

### Constructors and settings

All constructors load immediately except the empty-container forms. Every form
accepts an optional final `bool CaseSensitive`; the default is true. See
[Usage](#usage) for matching and stream-ownership rules.

| Constructor | Source and destination |
| --- | --- |
| `ConfigurationFile()` / `ConfigurationFile(bool CaseSensitive)` | Empty container without an original save destination. |
| `ConfigurationFile(string FileName)` | File path relative to the working directory, or an absolute path. |
| `ConfigurationFile(FileInfo FilePath)` | File represented by the typed path. |
| `ConfigurationFile(DirectoryInfo BaseDirectory, string FileName)` | Directory and filename combined with `Path.Combine`; an absolute filename overrides the base. |
| `ConfigurationFile(Stream Source)` | Reads from the current position; no original save destination, even for a `FileStream`. |
| `ConfigurationFile(Source, ConfigurationLoadOptions Options[, bool CaseSensitive])` | Selects [INCLUDE resolution](#including-other-files) and a [stream source name](#source-locations-for-parsed-elements) before loading. Null options select defaults. |

In the options form, `Source` is a string path, `FileInfo`, or `Stream`.
Relative paths are resolved when their path objects are created. Constructor
argument checks, [loading exceptions](#loading-diagnostics), and
[automatic-save destination rules](#saving) apply to these forms.

| Setting | Behavior |
| --- | --- |
| `CaseSensitive` | Read-only name-matching mode chosen by the constructor. |
| `autoUpdateRegistry` | Get/set, false by default. Enables creation of missing section parameters; see [Saving](#saving). |
| `autoSaveRegistry` | Get/set, false by default. Saves newly created parameters to the original file; see [Saving](#saving). |
| `parComment`, `parSeparator` | Public readonly arrays with mutable elements; see [Parser settings and automatic updates](#parser-settings-and-automatic-updates). |

### Stored block access

These getters never create blocks or save files. Null names throw
`ArgumentNullException`. Sections and dictionaries are separate: parameter APIs
operate only on sections.

| Getter | Existing block | Missing block |
| --- | --- | --- |
| `getTable(Name)` | Live mutable `List<string>` of rows, including for an empty table. | Unattached empty list. |
| `getList(Name)` | Live mutable `List<string>` of items. | Unattached empty list. |
| `getDictionary(Name)` | Live mutable `Dictionary<string,string>`. | Unattached empty dictionary. |
| `getText(Name)` | String with raw lines joined by LF, without an added final newline. | Empty string. |
| `getEncoded(Name)` | Independent decoded byte-array copy. | Empty array. |
| `getEncoded(Name, bool encoded)` | Decoded bytes when false; canonical Base64 ASCII bytes when true. Always a copy. | Empty array. |

Editing a live collection changes memory only and requires explicit `save()`.
Editing an unattached collection or byte-array copy does not change the registry.
For protected access, use [read-only views and snapshots](#read-only-views-and-immutable-snapshots).

`listSections`, `listTables`, `listLists`, `listDictionaries`, `listTexts` and
`listEncoded` return independent `List<string>` name snapshots sorted with
`StringComparer.Ordinal`. `listParameters(Section)` lists names in one section;
missing/empty sections return an empty list and null throws `ArgumentNullException`.
`listParameters()` returns distinct names across sections, deduplicated using the
selected case mode and sorted ordinally. Changing a name list never changes the
configuration. Dependency paths have their own [loaded-file list](#including-other-files).

```csharp
Dictionary<string, string> channels = config.getDictionary("CHANNELS");
channels["key1"] = "updated";
string notes = config.getText("NOTES");
byte[] payload = config.getEncoded("PAYLOAD");
string base64 = System.Text.Encoding.ASCII.GetString(config.getEncoded("PAYLOAD", true));
config.save();
```

### Parameter values and defaults

`getParameter(Section, Parameter, DefaultVal)` returns stored text or converts it
to the type of the supplied default. Missing entries return the default; storing
missing defaults is controlled by [automatic updates](#saving).

| Default type | Stored-value conversion |
| --- | --- |
| `string` | Returns stored text unchanged, including null. |
| `bool` | Trimmed `TRUE` (case-insensitive) and `1` are true. Every other non-null value is false, even with a true default. |
| `double` / `float` | Invariant-culture parsing after replacing commas with periods; parsing failures return the default. The float overload casts from double and can overflow. |
| `long` / `int` | Invariant-culture integer parsing; parsing failures return the default. The int overload casts from long and can wrap outside the int range. |

Typed default getters return the supplied default for stored nulls without
mutation or saving. These legacy conversions remain distinct from the stricter
[typed parameter try-reads](#presence-and-explicit-read-results) and configurable
[dictionary conversions](#typed-dictionary-values).

### Presence and explicit read results

Presence checks and `tryGet` reads never create entries or write files, regardless
of `autoUpdateRegistry` and `autoSaveRegistry`. Names use the selected case mode.
Null names throw `ArgumentNullException`; empty names remain valid.

| Presence check | Explicit read | Result when present |
| --- | --- | --- |
| `containsSection(Name)` | `tryGetSection(Name, out Parameters)` | Independent `Dictionary<string,string>` snapshot, preserving the name comparer. |
| `containsTable(Name)` | `tryGetTable(Name, out Rows)` | Live `List<string>`, like `getTable`. |
| `containsList(Name)` | `tryGetList(Name, out Items)` | Live `List<string>`, like `getList`. |
| `containsDictionary(Name)` | `tryGetDictionary(Name, out Entries)` | Live `Dictionary<string,string>`. |
| `containsText(Name)` | `tryGetText(Name, out Text)` | Raw lines joined with LF. |
| `containsEncoded(Name)` | `tryGetEncoded(Name, out Data)` | Independent decoded `byte[]`. |
| `containsParameter(Section, Parameter)` | `tryGetParameter(Section, Parameter, out Value)` | Stored string, or a successfully converted typed value. |

Block reads return true even for empty blocks. Missing blocks return false and a
null output, unlike the existing getters' empty collections/strings.
`checkSection` remains available with the same presence semantics as `containsSection`.
INCLUDE directives are expanded during loading and have no stored block to query;
query the imported blocks using their respective APIs.

`tryGetEncoded(Name, bool Encoded, out byte[] Data)` also supports canonical Base64
ASCII bytes when `Encoded=true`; it returns an independent array in either mode.

String parameter reads return true for an existing key even when its value is null
or empty. A missing key/section returns false and null. Typed overloads support
`bool`, `int`, `long`, `float` and `double`; false means missing, invalid or
out of range, and the output is reset to false/zero. An existing null value
throws `NullParameterValueException`, with the output also reset to false/zero. Use `containsParameter` and
the string overload to distinguish these cases. Typed parameter try-reads:

- Accept only `TRUE`/`FALSE` (ordinal, case-insensitive) and `1`/`0` for booleans.
- Parse integers in invariant culture and reject values outside the destination range.
- Trim whitespace and normalize decimal commas to periods for floating-point values.
  Reject NaN, infinity and overflow, including values outside the float range.
  Ordinary floating-point rounding and underflow still apply.

`NullParameterValueException` derives from `InvalidOperationException` in the
`ConfigurationFilesReader` namespace. It is serializable; its read-only
`SectionName` and `ParameterName` properties identify the null lookup. A stored
null is distinct from a missing key or an empty string. Reading it never replaces
it or saves the registry. Typed default getters handle null as described
[above](#parameter-values-and-defaults); typed dictionary reads use their own
[explicit status](#typed-dictionary-values).

```csharp
List<string> rows;
if (config.tryGetTable("ITEMS", out rows))
{
    // The table exists, even when rows.Count is zero.
}

int port;
if (!config.tryGetParameter("SERVER", "port", out port))
{
    // Missing or invalid: handle explicitly without storing a default.
}
```

Live collection edits follow the existing explicit-save rules; section snapshot
edits do not alter the registry. Use parameter setters to update a section.

### Read-only views and immutable snapshots

Use these accessors to share collections without permitting edits through the
returned reference. Existing mutable accessors keep their behavior.

| Accessor | Result |
| --- | --- |
| `getTableReadOnly(Name)` | Live `ReadOnlyCollection<string>` of table rows. |
| `getListReadOnly(Name)` | Live `ReadOnlyCollection<string>` of list items. |
| `getDictionaryReadOnly(Name)` | Live `ReadOnlyStringDictionary` of dictionary entries. |

A **view** reflects later changes made through the configuration's mutable APIs.
Mutation attempts through collection interfaces throw `NotSupportedException`;
casting the wrapper cannot recover the original mutable collection. Dictionary
keys and values also reject mutations. `ReadOnlyStringDictionary` supports
indexing, `Count`, `Keys`, `Values`, enumeration, `containsKey` and `tryGetValue`,
plus the standard generic `IDictionary<string,string>` read operations.
Names follow the configuration's case mode. Missing blocks return empty read-only
collections without creating data; use `containsTable`, `containsList` or
`containsDictionary` to distinguish missing from existing empty blocks. Null names
throw `ArgumentNullException`. These reads never save or create entries.

Use `createSnapshot()` for independent, stable data:

```csharp
var liveRows = config.getTableReadOnly("ITEMS");
ConfigurationSnapshot snapshot = config.createSnapshot();
config.getTable("ITEMS").Add("another row");
// liveRows reflects the addition; snapshot.getTable("ITEMS") remains unchanged.
```

A **snapshot** copies all stored sections, tables, lists, dictionaries, text, binary
data and loaded-file paths in memory, preserving nulls, empty blocks, original
spelling, case mode, row order and values that cannot be serialized. It performs
no parsing, I/O, default creation or saving. It has no setters, mutable registry
properties, or save methods. Changes to the original configuration cannot change
an existing snapshot. It does not retain caller streams, file handles or resolver
callbacks.

`ConfigurationSnapshot` provides:

- Read-only `CaseSensitive`; `containsSection/Table/List/Dictionary/Text/Encoded`,
  `containsParameter`, and `containsDictionaryValue` presence checks.
- `getTable` and `getList` returning `ReadOnlyCollection<string>`;
  `getDictionary` and `getSection` returning `ReadOnlyStringDictionary`.
  Missing blocks return empty read-only collections; presence checks distinguish
  them from existing empty blocks.
- `getText` and both `getEncoded` overloads with existing missing-block semantics.
  Binary results are defensive byte-array copies: editing a returned array cannot
  alter snapshot data.
- `listSections/Tables/Lists/Dictionaries/Texts/Encoded/LoadedFiles` and both
  `listParameters` overloads returning sorted read-only collections.
- String and typed `tryGetParameter` and `tryGetDictionaryValue` overloads with
  the same conversions and null behavior as the configuration. Typed dictionary
  reads also support the overload taking conversion options and an output status.

Snapshot collection wrappers reject edits, including through generic collection
interfaces. Returned dictionary keys/values cannot alter snapshot storage; binary
arrays are mutable copies, as described above.
The implementation uses .NET Framework 3.5 collection interfaces and wrappers;
no newer `IReadOnly*` interfaces or immutable-collection packages are required.
After construction, snapshots can be shared for concurrent reads. Creating a
snapshot or enumerating a live view while the original configuration is being
modified requires caller synchronization, like other configuration operations.

### Typed dictionary values

`containsDictionaryValue(DictionaryName, Key)` tests entry presence, including a
stored null. `tryGetDictionaryValue(DictionaryName, Key, out string Value)` returns
raw text: true for present entries (including null/empty), false and null for missing
entries. Names use the configuration's case mode; null names throw
`ArgumentNullException`. Section parameter APIs remain separate.

Typed `tryGetDictionaryValue` overloads support `bool`, `int`, `long`, `float` and
`double`. They return true only on successful conversion. False resets the output
to false/zero; it never substitutes a caller default. Use the status overload to
distinguish missing entries from invalid values:

```csharp
var options = new DictionaryConversionOptions {
    NumericCulture = System.Globalization.CultureInfo.GetCultureInfo("it-IT"),
    DecimalSeparator = DecimalSeparatorPolicy.Culture,
    BooleanValues = BooleanRepresentations.TrueFalse | BooleanRepresentations.YesNo,
    AllowNonFinite = false
};
double value;
DictionaryValueStatus status;
if (config.tryGetDictionaryValue("CHANNELS", "x", options, out value, out status))
{
    // Use the converted value.
}
else if (status == DictionaryValueStatus.InvalidValue)
{
    // The entry exists, but its text cannot be converted under this policy.
}
```

Each typed overload has these forms:

- `(DictionaryName, Key, out Value)` uses the defaults below.
- `(DictionaryName, Key, Options, out Value)` selects explicit policies.
- `(DictionaryName, Key, Options, out Value, out Status)` also reports the result.

`DictionaryValueStatus` is `Success`, `MissingDictionary`, `MissingKey`, `NullValue`
or `InvalidValue`. Typed dictionary reads return false for stored nulls with
`NullValue`; raw string reads still return true and null. Existing parameter reads
retain their documented null exceptions and legacy conversions.

`DictionaryConversionOptions` controls conversions independently of process culture:

| Option | Default and behavior |
| --- | --- |
| `NumericCulture` | `CultureInfo.InvariantCulture`. Supplies numeric signs, decimal separator in Culture mode, and floating-point special symbols. Null is invalid. |
| `DecimalSeparator` | `DotOrComma` accepts either decimal separator, rejecting text containing both. `Culture` uses the selected culture's decimal separator; `Dot` or `Comma` accepts only that separator. Applies to floating-point reads. |
| `BooleanValues` | `TrueFalse \| OneZero`. Flags also offer `YesNo` and `OnOff`; combine to opt in, or use `None` to reject all forms. Comparisons are ordinal and case-insensitive. |
| `AllowNonFinite` | false. When true, explicit culture-specific NaN and positive/negative infinity symbols are accepted for floats/doubles. Numeric overflow is still rejected. |

All conversions trim surrounding whitespace. Integers use `NumberStyles.Integer`
and enforce their destination type's bounds. Floating-point values allow signs,
decimals and exponents (`NumberStyles.Float`); thousands/grouping separators are
always rejected to avoid ambiguous decimal interpretation. Finite float overflow
is rejected; normal rounding and underflow are permitted. Empty and malformed text
return `InvalidValue`. Null options select defaults; unknown enum values/flag bits
throw `ArgumentException`, and null `NumericCulture` throws `ArgumentNullException`.
Options are validated even when an entry is absent.

All these reads are side-effect-free, including with automatic creation/saving
enabled. They never create dictionaries/entries, replace text, or save files.
Policies apply only to these new dictionary accessors. Application schemas, domain
ranges, table columns and cross-reference validation remain the caller's responsibility.

### Saving

The two runtime properties default to false and are independent:

| Operation | `autoUpdateRegistry=false` | `autoUpdateRegistry=true` |
| --- | --- | --- |
| `setParameter` | Updates existing parameters; skips missing entries. | Updates or creates parameters and their sections. |
| `addParameter` | Makes no changes. | Creates missing parameters and sections; preserves existing values. |
| `getParameter` | Reads existing values; returns missing defaults without storing them. | Reads existing values; stores missing defaults. |

`setParameter(Section, Parameter, Value)` stores text unchanged and preserves
original name spelling. `addParameter(Section, Parameter, DefaultVal)` never
replaces an existing value, including null. Null section/key names throw
`ArgumentNullException`; empty names and values are allowed.
Constructor bool arguments select case sensitivity, not automatic updates or saving.

To save newly created entries automatically:

```csharp
var config = new ConfigurationFile("config.ini") {
    autoUpdateRegistry = true,
    autoSaveRegistry = true
};
config.addParameter("SERVER", "newKey", "value");
```

`setParameter`, `addParameter`, and missing-default reads through `getParameter`
trigger a save only when they create a parameter or section. Updating an existing
value, adding an already existing parameter, inspecting sections, changing table
rows, or toggling either property does not trigger a save. Changing either flag
alone creates no entries. A triggered save writes
the entire current registry, including earlier unsaved changes.

Automatic saves always target the original file, even after an explicit save to
another destination. Creating entries with automatic saving enabled on an object
without an original file throws `InvalidOperationException` before changing it.
If saving fails, the new parameter and any section created for it are removed;
earlier in-memory changes remain. Save errors propagate to the caller.

`dump()` serializes the registry without writing a file:

```csharp
using (var dump = config.dump())
using (var reader = new System.IO.StreamReader(dump.BaseStream))
{
    string text = reader.ReadToEnd();
}
```

The returned writer owns its memory stream. Alternatively, `dump(writer)` writes
to a caller-owned `StreamWriter`, preserving its encoding and newline settings.
A null writer throws `ArgumentNullException`. Dumping uses the same sorted
sections, parameters and ordered table rows as saving.
It emits raw registry content without checking whether the parser can reload it;
`save` performs that validation before replacing the destination.

```csharp
config.setParameter("SERVER", "host", "example.com");
config.save(); // Overwrite the original file.
config.save("copy.ini");
config.save(new System.IO.FileInfo("backup.ini"));
config.save(new System.IO.FileInfo("backups"), "config.ini");
```

All overloads save the current in-memory sections, parameters, table rows, list
items, dictionary entries, text blocks and Base64 data.
Explicit destinations do not change the original path used by `save()`. An
in-memory configuration always needs an explicit destination, even after its
first save. `save(FileInfo Path, string FileName)` treats `Path.FullName` as a
base directory and uses `Path.Combine`; a rooted filename overrides that base.
Parent directories must already exist; null arguments are rejected. Save operations
include all dictionaries, with names and keys sorted ordinally, and enforce the
same round-trip validation for their values.

Output uses UTF-8 without a BOM, ordinally sorted section/parameter/table names,
and the current order of table rows and list items. Comments, source ordering and original
formatting outside text content are not preserved. Text content retains its raw
lines, including whitespace and comment-looking lines. Saving uses the standard `=`
separator and adds no punctuation terminators to any block. Existing trailing
punctuation is preserved exactly. Base64 decoded bytes are included in round-trip
validation.

`save` obtains the serialized bytes from `dump()`, copies them to a temporary file
in the destination directory, closes that file, and reloads it with
the standard parser and checks that all names and values are preserved. Data the
format cannot represent (for example null values, multiline values, or
leading/trailing whitespace or literal comment prefixes outside text blocks) causes `InvalidOperationException` rather than
a lossy save. No quoting or escaping is introduced.

An existing destination is replaced with `File.Replace`; a new destination is
created with `File.Move`. Failed serialization or validation leaves the destination
untouched and temporary files are cleaned up. Filesystem errors propagate;
replacement requires filesystem support and does not fall back to truncating
the destination. Concurrent changes to a configuration are not supported.

## Limitations

### Parser settings and automatic updates

`parComment` (default `##`) and `parSeparator` (default `=`) are public readonly
arrays. Their existing elements remain mutable, but construction loads the input
immediately; changing them later does not reparse it. There is no public reload
method or constructor option for custom prefixes or separators.
`CaseSensitive` is selected at construction, as shown in [Usage](#usage).

Automatic creation and saving are documented together under [Saving](#saving).

### Value and format limitations

Null values, empty names and empty strings can be stored in memory. See
[Parameter values and defaults](#parameter-values-and-defaults),
[explicit parameter reads](#presence-and-explicit-read-results), and
[typed dictionary values](#typed-dictionary-values) for conversion and null rules.
Save rejects data that cannot round-trip, including nulls; see [Saving](#saving).

The parser is permissive: lines outside blocks and section/dictionary lines without
`=` are ignored. Unknown bracketed identifiers become plain section names;
keywords remain uppercase even in case-insensitive name mode. TABLE stores raw
row strings rather than validating column counts or types. See [File format](#file-format)
for comment handling, whitespace trimming and bracketed-row restrictions.

Internally created readers and writers are disposed through `using` blocks, including on exceptions.
Caller-supplied streams remain open.
Concurrent changes are not synchronized.

## ConfigurationValidator

The console application validates configuration files, lists names and rewrites
configurations to a file or standard output. See the
[ConfigurationValidator guide](ConfigurationValidator/README.md) for build and
usage examples, command-line options, exit codes and example validation.

## Build

Run commands from the repository root. Building requires a toolchain with .NET
Framework 3.5 reference assemblies and compatible targets. GNU Make drives the
repository targets; tests additionally require Python 3 and Mono (`mcs` and `mono`
on `PATH`). No Python packages or test frameworks are required.

| Command | Purpose |
| --- | --- |
| `make` / `make ms-build` | Build `IniLike.sln` with MSBuild (default target). |
| `make x-build` | Build explicitly with deprecated xbuild. |
| `make test` | Build temporarily and run library and console regression tests. |
| `make test-examples` | Validate all examples; see the [validator guide](ConfigurationValidator/README.md#validate-all-examples). |
| `make clean` | Delete both projects' entire `bin` and `obj` directories, including all configurations. |

Build and test default to Release. Use `CONFIGURATION=Debug` to select Debug.
`MSBUILD=/path/to/msbuild` and `XBUILD=/path/to/xbuild` select executables; these
must be paths, not commands with arguments. `PYTHON=/path/to/python3` selects the
test interpreter. For example:

```sh
make ms-build MSBUILD=/path/to/msbuild
make test CONFIGURATION=Debug PYTHON=python3
```

`make test` and `make test-examples` prefer MSBuild and fall back to xbuild only
when MSBuild is unavailable. An explicit `MSBUILD` override disables fallback.
A missing explicitly selected executable or an actual build failure is an error.
`XBUILD` selects the fallback executable. The explicit build targets never switch tools, and the test
runner selects its tool independently of a previous build target.

To build without Make:

```sh
msbuild IniLike.sln /p:Configuration=Release
```

The solution contains the library and console validator projects. The resulting
library is `ConfigurationFilesReader/bin/Release/ConfigurationFilesReader.dll`.
For console invocation and output files, see the
[validator guide](ConfigurationValidator/README.md#build-and-basic-usage).

## Automated tests

For prerequisites, tool selection and Make overrides, see [Build](#build).
`make test` builds the solution from source in a temporary directory, compiles
`tests/ConfigurationFileTests.cs`, and runs the library and console checks. Each
library test uses an isolated directory. Build artifacts stay outside the
repository; the runner prints a summary and returns nonzero on failure. A separate
build is unnecessary before testing.

The runner can also be invoked directly:

```sh
python3 tests/run.py
python3 tests/run.py --configuration Debug
python3 tests/run.py --msbuild /path/to/msbuild
```

`--msbuild` disables fallback just like the Make override; `--xbuild` selects the
fallback executable. Coverage includes all block types, parser compatibility,
legacy and explicit conversions, automatic-save rollback, input preservation,
INCLUDE resolution/dependencies, read-only views/snapshots, source metadata and
named-stream diagnostics, plus console options and rewrite behavior.

## Source layout

The implementation uses one partial class across eleven source files in
`ConfigurationFilesReader/`: `ConfigurationFile.cs` holds shared settings and
constructors; `ConfigurationFile.Parsing.cs` handles block identification and loading;
`ConfigurationFile.Persistence.cs` handles dump/save and round-trip validation.
`ConfigurationFile.Sections.cs`, `ConfigurationFile.Tables.cs`,
`ConfigurationFile.Dictionaries.cs`, `ConfigurationFile.Lists.cs`,
`ConfigurationFile.Texts.cs` and `ConfigurationFile.Encoded.cs` hold the
respective storage and access methods, including parameter methods in Sections.
`ConfigurationFile.ReadOnly.cs` supplies collection views and snapshot creation;
`ConfigurationSnapshot.cs` and `ReadOnlyStringDictionary.cs` define the immutable
read API and the .NET 3.5 dictionary wrapper.
`ConfigurationFile.Sources.cs` stores provenance and provides source-location lookups.

## License

IniLike is licensed under the GNU Lesser General Public License, version 3
(LGPL-3.0). See [LICENSE.md](LICENSE.md) for the license text.
