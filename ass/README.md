# ASS Timeline Adjuster

A command-line tool for adjusting ASS subtitle timelines.

## Build

```
dotnet publish
```

## Usage

```
ass <input.ass> <milliseconds> <output.ass>
```

Positive offsets delay subtitles; negative offsets move them earlier.
