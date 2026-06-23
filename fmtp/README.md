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
fmtp.exe <local directory> <mtp full path>
```

The MTP path must include the device name as its first segment:

```
fmtp.exe C:\Photos "Phone\Internal storage\DCIM"
```
