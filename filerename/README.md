# File Rename

Batch rename files.

The UI uses SukiUI 7.0.1 with a custom Indigo color theme and the system
light/dark variant. SukiWindow and GlassCard retain the library templates;
buttons use the built-in Flat, Outlined and Basic styles. Neutral surfaces,
text and status colors are centralized in `Themes/Palette.axaml`, with
presentation styles in `Themes/Presentation.axaml`.

### Run

From the repository root:

```sh
dotnet run --project filerename/filerename.csproj
```

### Build

dotnet 10 is needed. Build:

```
dotnet publish
```

### Usage

Use separator to split filename, and `{n}` placeholder to compose new filename.
