# fmtp

~~Fuck MTP~~. Diff mtp device and local directory. Move local redundant files to `diff` directory.

Used to backup files. It helps to delete local redundant files.

## Build

```
dotnet publish -c Release -r win-x64 --no-self-contained /p:PublishSingleFile=true
```

Executable file will be generated at `bin/Release/net6.0/win-x64/publish/fmtp.exe`.


## Usage

```
fmtp.exe <local directory> <mtp device directory>
```
