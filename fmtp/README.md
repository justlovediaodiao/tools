# fmtp

Sync a local directory with one MTP device directory:

- Copy new files from MTP to local.
- Move local-only files to `diff` for review before deletion.

## Build

```powershell
cmake -S . -B build -A x64
cmake --build build --config Release --target fmtp
```

## Usage

```powershell
fmtp.exe <local directory> <mtp full path>
```

### Programmatic output

Use `-p` to output UTF-8 JSON Lines. Each line is flushed immediately and can
be parsed as an independent JSON object.

```powershell
fmtp.exe -p <local directory> <mtp full path>
```

The `info`, `error`, and `success` types all include a `message` field for
display:

```json
{"type":"info","message":"Connect to MTP device: Phone"}
{"type":"error","message":"Failed to download new-photo.jpg: Failed to read MTP resource stream"}
{"type":"success","message":"Completed successfully."}
```

File-count progress (`copy` and `move` are counted separately):

```json
{"type":"move","current":1,"total":2,"file":"old-photo.jpg"}
{"type":"copy","current":2,"total":3,"file":"new-photo.jpg"}
```

`current` is the number of processed files, `total` is the number to process,
and `file` is the current file. A copy failure emits `error` and processing
continues.

- Success: exit code `0`, final type `success`.
- Failure: exit code `1`, final type `error`.
