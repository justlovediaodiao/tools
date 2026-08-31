# RawV

A simple camera photo culling tool. When you export photos from your camera to your computer, they typically include both JPG + RAW formats. RawV lets you browse JPG previews and delete the RAW files together when you delete the JPG preview.

## Features

- **Open Folder or Files** - Quickly browse photos exported from your camera
- **Real-time Preview** - Supports JPG/PNG/WebP/HEIC and other common formats
- **One-click RAW Delete** - Automatically finds and deletes RAW files with the same name when deleting images
- **Keyboard Navigation** - Use arrow keys to browse quickly, Delete key to remove
- **Recycle Bin Delete** - All deletion operations move to the system recycle bin and can be undone
- **MTP Sync** - Sync photos from MTP device, Windows only

## Build

.NET 10 is required.
Build RawV with:

```bash
dotnet publish
```

### MTP sync support (Windows only)

MTP sync depends on the external Windows-only
[`fmtp`](https://github.com/justlovediaodiao/tools/tree/master/fmtp) CLI.
To include this feature, build `fmtp` on Windows and copy to RawV publish directory.
