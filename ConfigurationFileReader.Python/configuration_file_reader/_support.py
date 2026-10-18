"""Internal mappings, streaming and conversion support for the port."""

import codecs
import math
import re
import struct
from collections.abc import MutableMapping


class NullParameterValueException(ValueError):
    """A typed read without a default encountered a stored null."""

    def __init__(self, SectionName, ParameterName):
        self.SectionName = SectionName
        self.ParameterName = ParameterName
        super().__init__(f"Parameter '{ParameterName}' in section '{SectionName}' has a null value.")

    def __reduce__(self):
        return type(self), (self.SectionName, self.ParameterName)


class ConfigurationError(ValueError):
    """Loading failure with the innermost source location and include chain."""

    def __init__(self, Message, Source, Line, Cause=None):
        from pathlib import Path
        location = f"line {Line}" if Line else "before reading"
        super().__init__(f"Source '{Source}', {location}: {Message}")
        self.FilePath = getattr(Cause, "FilePath", Source)
        self.FileName = getattr(Cause, "FileName", Path(Source).name)
        self.LineNumber = getattr(Cause, "LineNumber", Line)
        self.ContextSource = Source


def nameKey(Name, Sensitive):
    if not isinstance(Name, str):
        raise TypeError("Names must be strings, not null.")
    if Sensitive:
        return Name
    # Simple, non-expanding Unicode casing; never equate 'ß' with 'SS'.
    return "".join(c.upper() if len(c.upper()) == 1 else c for c in Name)


def ordinalKey(Name):
    return Name.encode("utf-16-be", errors="surrogatepass")


class NameMap(MutableMapping):
    """Case-aware live mapping which preserves the first spelling of each key."""

    def __init__(self, Sensitive=True, Strings=False):
        self._sensitive = Sensitive
        self._strings = Strings
        self._data = {}

    def __getitem__(self, Key):
        return self._data[nameKey(Key, self._sensitive)][1]

    def __setitem__(self, Key, Value):
        normalized = nameKey(Key, self._sensitive)
        if self._strings and Value is not None and not isinstance(Value, str):
            raise TypeError("Parameter and dictionary values must be strings or None.")
        original = self._data.get(normalized, (Key, None))[0]
        self._data[normalized] = original, Value

    def __delitem__(self, Key):
        del self._data[nameKey(Key, self._sensitive)]

    def __iter__(self):
        return (item[0] for item in self._data.values())

    def __len__(self):
        return len(self._data)

    def add(self, Key, Value):
        if Key in self:
            raise ValueError(f"Duplicate name: '{Key}'.")
        self[Key] = Value

    def copy(self):
        result = NameMap(self._sensitive, self._strings)
        result.update(self)
        return result


def readLines(Stream):
    """Read LF/CRLF/CR from the current position without closing or seeking."""
    first = Stream.read(4)
    if isinstance(first, bytes):
        while len(first) < 4:
            more = Stream.read(4 - len(first))
            if not more:
                break
            first += more
        encoding = "utf-8-sig"
        if first.startswith((codecs.BOM_UTF32_LE, codecs.BOM_UTF32_BE)):
            encoding = "utf-32"
        elif first.startswith((codecs.BOM_UTF16_LE, codecs.BOM_UTF16_BE)):
            encoding = "utf-16"
        decoder = codecs.getincrementaldecoder(encoding)(errors="replace")
    elif isinstance(first, str):
        decoder = None
    else:
        raise TypeError("The stream must return text or bytes.")
    pending = ""
    chunk = first
    while True:
        eof = not chunk
        pending += decoder.decode(chunk, final=eof) if decoder else chunk
        start = 0
        index = 0
        while index < len(pending):
            char = pending[index]
            if char not in "\r\n":
                index += 1
                continue
            if char == "\r" and index + 1 == len(pending) and not eof:
                break
            yield pending[start:index]
            index += 2 if pending[index:index + 2] == "\r\n" else 1
            start = index
        pending = pending[start:]
        if eof:
            if pending:
                yield pending
            return
        chunk = Stream.read(8192)


_TYPES = {str: "string", bool: "bool", int: "int", float: "double"}
_FLOAT = re.compile(r"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$")
_INTEGER = re.compile(r"^[+-]?[0-9]+$")


def valueType(Type):
    result = _TYPES.get(Type, Type)
    if result not in ("string", "bool", "int", "long", "float", "double"):
        raise TypeError("ValueType must be str/bool/int/float or string/bool/int/long/float/double.")
    return result


def zeroValue(Type):
    return None if Type == "string" else False if Type == "bool" else 0.0 if Type in ("float", "double") else 0


def convertValue(Text, Type, Strict):
    if Type == "string":
        return True, Text
    text = Text.strip()
    if Type == "bool":
        if text.upper() in ("TRUE", "1"):
            return True, True
        return (not Strict or text.upper() in ("FALSE", "0")), False
    if Type in ("int", "long"):
        if not _INTEGER.fullmatch(text):
            return False, 0
        try:
            number = int(text)
        except ValueError:
            return False, 0
        bits = 32 if Strict and Type == "int" else 64
        if not -(1 << (bits - 1)) <= number < (1 << (bits - 1)):
            return False, 0
        if Type == "int" and not Strict:
            number = (number + (1 << 31)) % (1 << 32) - (1 << 31)
        return True, number
    text = text.replace(",", ".")
    special = text in ("NaN", "Infinity", "-Infinity")
    if not special and not _FLOAT.fullmatch(text):
        return False, 0.0
    number = float(text)
    if not math.isfinite(number) and (Strict or not special):
        return False, 0.0
    if Type == "float":
        maximum = 3.4028234663852886e38
        if Strict and abs(number) > maximum:
            return False, 0.0
        try:
            number = struct.unpack("!f", struct.pack("!f", number))[0]
        except OverflowError:
            number = math.copysign(math.inf, number)
    return True, number


def formatDefault(Value, Type):
    if Value is None or Type == "string":
        return Value
    if Type == "bool":
        return "TRUE" if Value else "FALSE"
    if Type in ("int", "long"):
        return str(Value)
    if math.isnan(Value):
        return "NaN"
    if math.isinf(Value):
        return "Infinity" if Value > 0 else "-Infinity"
    return format(Value, ".15g")
