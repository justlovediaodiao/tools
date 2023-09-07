import io
import struct
from typing import NamedTuple, Literal


class UnexpectedProtobuf(Exception):
    pass


class Field(NamedTuple):
    number: str
    type: Literal['varint', 'i64', 'i32', 'string', 'bytes', 'protobuf']
    value: int | str | bytes | list['Field']

    def _format(self, field, indent) -> str:
        value = field.value
        if isinstance(value, list):
            lines = ['']
            for x in value:
                lines.append(self._format(x, indent + 2))
            value_str = '\n'.join(lines)
        elif isinstance(value, bytes):
            value_str = value.hex()
        else:
            value_str = str(value)

        return '{indent}[{number}] ({type}) {value}'.format(
            indent=' ' * indent,
            number=field.number,
            type=field.type,
            value=value_str)

    def __str__(self):
        return self._format(self, 0)


def parse(data: bytes) -> list[Field]:
    r = io.BytesIO(data)
    fields = []
    while True:
        try:
            f = read_field(r)
        except EOFError:
            raise UnexpectedProtobuf
        if f is None:
            break
        fields.append(f)
    return fields


def guess_bytes(data: bytes) -> tuple[str, list[Field] | str | bytes]:
    try:
        return 'protobuf', parse(data)
    except UnexpectedProtobuf:
        pass

    try:
        return 'string', data.decode()
    except UnicodeDecodeError:
        pass

    return 'bytes', data


def read_field(r: io.BytesIO) -> Field | None:
    try:
        tag = read_varint(r)
    except EOFError:
        return None
    field_number = tag >> 3
    wire_type = tag & 0x07
    match wire_type:
        case 0:
            wtype = 'varint'
            value = read_varint(r)
        case 1:
            wtype = 'i64'
            value = read_i64(r)
        case 2:
            value = read_len(r)
            wtype, value = guess_bytes(value)
        case 5:
            wtype = 'i32'
            value = read_i32(r)
        case _:
            raise UnexpectedProtobuf
    return Field(field_number, wtype, value)


def read_varint(r: io.BytesIO) -> int:
    value = 0
    i = 0
    while True:
        byte = read(r, 1)[0]
        value |= (byte & 0x7F) << (7 * i)
        i += 1
        if not byte & 0x80:
            break
    return value


def read_i32(r: io.BytesIO) -> int:
    v = read(r, 4)
    return struct.unpack('<f', v)[0]


def read_i64(r: io.BytesIO) -> int:
    v = read(r, 8)
    return struct.unpack('<d', v)[0]


def read_len(r: io.BytesIO) -> bytes:
    length = read_varint(r)
    return read(r, length)


def read(r: io.BytesIO, size: int) -> bytes:
    b = r.read(size)
    if len(b) == 0:
        raise EOFError
    if len(b) < size:
        raise UnexpectedProtobuf
    return b


if __name__ == '__main__':
    def main():
        import sys
        if len(sys.argv) != 2:
            print('Usage: python pb.py <filename>')
            return
        fname = sys.argv[1]
        with open(fname, 'rb') as fp:
            data = fp.read()
        fields = parse(data)
        for field in fields:
            print(field)
    main()
