# fmtp

~~Fuck MTP~~. 

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
