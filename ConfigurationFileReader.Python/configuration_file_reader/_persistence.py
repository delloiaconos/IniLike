"""Canonical dump and validated replacement of configuration files."""

import base64
import io
import os
import tempfile
from pathlib import Path

from ._support import NameMap, ordinalKey


_UNSET = object()


class Persistence:
    def dump(self, Writer=None):
        """Return a rewound StringIO, or write/flush a borrowed text writer."""
        owned = Writer is None
        writer = io.StringIO(newline="") if owned else Writer
        try:
            for kind, blocks in self._blocks.items():
                for name in sorted(blocks, key=ordinalKey):
                    prefix = {"section": "", "table": "TABLE:", "list": "LIST:", "dictionary": "DICT:", "text": "TEXT:", "encoded": "ENCODED:"}[kind]
                    writer.write(f"[{prefix}{name}]\n")
                    content = blocks[name]
                    if kind in ("section", "dictionary"):
                        for key in sorted(content, key=ordinalKey):
                            value = content[key]
                            writer.write(key + "=" + ("" if value is None else value) + "\n")
                    elif kind == "encoded":
                        writer.write(base64.b64encode(content).decode("ascii") + "\n")
                    else:
                        for row in content:
                            if row is not None and not isinstance(row, str):
                                raise TypeError("Rows must be strings or None.")
                            writer.write(("" if row is None else row) + "\n")
                        if kind == "text":
                            writer.write("[END]\n")
            writer.flush()
            if owned:
                writer.seek(0)
                return writer
            return None
        except BaseException:
            if owned:
                writer.close()
            raise

    def _sameData(self, Other):
        for kind, blocks in self._blocks.items():
            other_blocks = Other._blocks[kind]
            if len(blocks) != len(other_blocks):
                return False
            for name, content in blocks.items():
                if name not in other_blocks:
                    return False
                other = other_blocks[name]
                if isinstance(content, NameMap):
                    if len(content) != len(other) or any(key not in other or value != other[key] for key, value in content.items()):
                        return False
                elif content != other:
                    return False
        return True

    def save(self, File=_UNSET, FileName=_UNSET):
        """Save to the original file, a path, or a base path plus filename."""
        if File is _UNSET:
            if FileName is not _UNSET:
                raise TypeError("A base directory is required with FileName.")
            if self._path is None:
                raise ValueError("No original file is associated with this configuration. Specify a destination.")
            destination = self._path
        else:
            if File is None or FileName is None:
                raise TypeError("File and FileName cannot be None.")
            destination = Path(File) if FileName is _UNSET else Path(File) / FileName
            destination = Path(os.path.abspath(destination))
        descriptor, temporary = tempfile.mkstemp(prefix=".inilike-", suffix=".tmp", dir=destination.parent)
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="") as writer:
                self.dump(writer)
            try:
                reloaded = type(self)(temporary, CaseSensitive=self.CaseSensitive)
                if not self._sameData(reloaded):
                    raise ValueError("Serialized data differs from the registry.")
            except (ValueError, TypeError) as error:
                raise ValueError("The configuration cannot be represented by the INI-like format.") from error
            os.replace(temporary, destination)
        finally:
            if os.path.exists(temporary):
                os.unlink(temporary)
