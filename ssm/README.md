# ssm

A Codex and DeepSeek Harness session manager built with Ratatui. It is a Rust rewrite of `ssm.py`.

## Usage

```text
ssm <codex|dsh>
```

- `Ctrl+A`: Switch between the current directory and all projects
- `Up` / `Down`: Select a session
- `Enter`: Delete the selected session
- `Ctrl+Z`: Delete all invalid sessions in the all-projects view
- `Esc` / `Ctrl+C`: Quit

## Build

Install Rust, then run:

```sh
cargo build --release
```
