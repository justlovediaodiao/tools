# Ass

Ass is a tool for adjusting timeline of ass subtitle files.

### Build

```
dotnet publish -p:PublishAot=true
```

### Usage

```
Ass.exe <file> <millisecond>
```

`millisecond` can be negative. The adjusted subtitle files are named as `*_fix.*`
