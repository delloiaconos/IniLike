"""Shared parser for files, borrowed streams, and nested includes."""

import base64
import os

from ._support import ConfigurationError, NameMap, nameKey, readLines


_IDENTIFIERS = {
    "INCLUDE": "include", "INC": "include", "INPUT": "include", "LINK": "include", "LOAD": "include",
    "END": None,
    "SECTION": "section", "SEC": "section",
    "TABLE": "table", "TBL": "table", "TAB": "table",
    "LIST": "list", "LST": "list",
    "DICT": "dictionary", "DICTIONARY": "dictionary",
    "TEXT": "text", "TXT": "text",
    "ENCODED": "encoded", "ENC": "encoded", "BASE64": "encoded", "B64": "encoded",
}


class Parsing:
    def _stripComment(self, Line):
        positions = [Line.find(prefix) for prefix in self.parComment if prefix]
        return Line[:min((pos for pos in positions if pos >= 0), default=len(Line))]

    @staticmethod
    def _header(Line):
        if not (Line.startswith("[") and Line.endswith("]")):
            return None
        header = Line[1:-1]
        if header == "END":
            return None, None
        keyword, separator, name = header.partition(":")
        if separator and keyword in _IDENTIFIERS:
            return _IDENTIFIERS[keyword], name.strip()
        return "section", header.strip()

    def _loadFile(self, FileName):
        if not os.fspath(FileName):
            raise ValueError("An INCLUDE block requires a filename.")
        path = os.path.abspath(FileName)
        identity = os.path.normcase(path)
        if identity in self._loading:
            raise ValueError(f"Include cycle detected for file '{path}'.")
        if len(self._loading) >= 128:
            raise ValueError(f"Maximum include nesting depth (128 files) exceeded at '{path}'.")
        self._loading.add(identity)
        try:
            with open(path, "rb") as reader:
                self._loadReader(reader, path)
        except ConfigurationError:
            raise
        except (OSError, ValueError, TypeError) as error:
            raise ConfigurationError(str(error), path, 0, error) from error
        finally:
            self._loading.remove(identity)

    def _decode(self, Name, Pieces, Source, Line):
        try:
            # Convert.FromBase64String ignores these four whitespace characters.
            payload = "".join(Pieces).translate(str.maketrans("", "", " \t\r\n"))
            self._blocks["encoded"][Name] = base64.b64decode(payload, validate=True)
        except (ValueError, UnicodeError) as error:
            raise ConfigurationError(f"Invalid Base64 data in block '{Name}'.", Source, Line, error) from error

    def _loadReader(self, Reader, Source="<stream>"):
        kind = None
        parent = ""
        encoded = []
        line_number = 0
        block_line = 0
        lines = iter(readLines(Reader))
        try:
            while True:
                line_number += 1
                try:
                    raw = next(lines)
                except StopIteration:
                    line_number -= 1
                    break
                line = self._stripComment(raw).strip()
                if kind != "text" and not line:
                    continue
                header = self._header(line)
                if header is not None:
                    next_kind, name = header
                    if next_kind is None and kind is not None and name is not None:
                        if nameKey(name, self.CaseSensitive) != nameKey(parent, self.CaseSensitive):
                            raise ValueError(f"End label does not match the current block: '{parent}'.")
                    if kind == "encoded":
                        self._decode(parent, encoded, Source, block_line)
                        encoded = []
                    block_line = line_number
                    kind, parent = next_kind, name if next_kind else ""
                    if kind == "include":
                        self._loadFile(parent)
                    elif kind in self._blocks:
                        content = NameMap(self.CaseSensitive, Strings=True) if kind in ("section", "dictionary") else b"" if kind == "encoded" else []
                        self._blocks[kind].add(parent, content)
                elif kind == "encoded":
                    encoded.append(line)
                elif kind == "text":
                    self._blocks[kind][parent].append(raw)
                elif kind in ("section", "dictionary"):
                    positions = [line.find(char) for char in self.parSeparator]
                    position = min((pos for pos in positions if pos >= 0), default=-1)
                    if position >= 0:
                        self._blocks[kind][parent].add(line[:position].strip(), line[position + 1:].strip())
                elif kind in ("table", "list"):
                    self._blocks[kind][parent].append(line)
            if kind == "encoded":
                self._decode(parent, encoded, Source, block_line)
        except (OSError, ValueError, TypeError) as error:
            if isinstance(error, ConfigurationError) and error.ContextSource == Source:
                raise
            raise ConfigurationError(str(error), Source, line_number, error) from error
