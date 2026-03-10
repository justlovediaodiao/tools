# fmtp

~~Fuck MTP~~. Windows only.

Sync local directory with MTP device directory:

- Copy new files from MTP to local.
- Move local-only files to `diff` folder for review before deletion.


## Build

```
dotnet publish
```

## Usage

```
fmtp.exe <local directory> <mtp device directory>
```

## Powershell Version

No dotnet runtime or a self-contained executable (tens of MB). Only a few KB PowerShell script, delivering the same functionality.

**Usage:**

```
fmtp.ps1 <local directory> <mtp device directory>
```