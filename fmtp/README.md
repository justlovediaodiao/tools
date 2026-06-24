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
