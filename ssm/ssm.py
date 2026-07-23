#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import re
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Protocol

from rich.cells import cell_len, set_cell_size
from rich.text import Text
from textual.app import App, ComposeResult
from textual.binding import Binding
from textual.containers import Container
from textual.reactive import reactive
from textual.widgets import Static


UNKNOWN_TITLE = "<unknow>"


@dataclass
class SessionEntry:
    session_id: str
    session_file: Path
    sub_agents_dir: Path | None
    project_dir: Path
    cwd: str
    title: str
    local_time: str
    timestamp: datetime
    is_valid: bool


class SessionStore(Protocol):
    label: str

    def load_sessions(self) -> list[SessionEntry]:
        ...

    def delete_session(self, entry: SessionEntry) -> None:
        ...


def shorten(text: str, width: int) -> str:
    text = " ".join(text.split())
    if width <= 1:
        return text[:width]
    if cell_len(text) <= width:
        return text
    if width <= 3:
        return set_cell_size(text, width)
    clipped = set_cell_size(text, width - 3).rstrip()
    return f"{clipped}..."


def normalize_text(text: str) -> str:
    text = re.sub(r"<[^>]+>", " ", text)
    return " ".join(text.split())


def parse_iso_timestamp(value: str) -> datetime | None:
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone()
    except ValueError:
        return None


def format_relative_time(timestamp: datetime) -> str:
    now = datetime.now(timestamp.tzinfo)
    delta = now - timestamp
    seconds = max(int(delta.total_seconds()), 0)
    if seconds < 60:
        return "just now"
    if seconds < 3600:
        minutes = seconds // 60
        return f"{minutes} minute{'s' if minutes != 1 else ''} ago"
    if seconds < 86400:
        hours = seconds // 3600
        return f"{hours} hour{'s' if hours != 1 else ''} ago"
    if seconds < 86400 * 7:
        days = seconds // 86400
        return f"{days} day{'s' if days != 1 else ''} ago"
    if seconds < 86400 * 30:
        weeks = seconds // (86400 * 7)
        return f"{weeks} week{'s' if weeks != 1 else ''} ago"
    if seconds < 86400 * 365:
        months = seconds // (86400 * 30)
        return f"{months} month{'s' if months != 1 else ''} ago"
    years = seconds // (86400 * 365)
    return f"{years} year{'s' if years != 1 else ''} ago"


class ClaudeSessionStore:
    label = "Claude"
    projects_dir = Path.home() / ".claude" / "projects"

    @staticmethod
    def decode_project_dir_name(name: str) -> str:
        if not name.startswith("-"):
            return name
        return "/" + name.lstrip("-").replace("-", "/")

    def iter_session_files(self) -> list[Path]:
        if not self.projects_dir.exists():
            return []

        files: list[Path] = []
        for project_dir in sorted(self.projects_dir.iterdir()):
            if not project_dir.is_dir():
                continue
            for path in sorted(project_dir.iterdir()):
                if path.is_file() and path.suffix == ".jsonl":
                    files.append(path)
        return files
    
    def extract_title_from_content(self, content) -> str | None:
        if isinstance(content, str):
            if content.startswith("<local-command") or content.startswith("<command-name"):
                return None
            return normalize_text(content)

        if isinstance(content, list):
            for item in content:
                if isinstance(item, dict) and item.get("type") == "text":
                    text = item.get("text")
                    if isinstance(text, str):
                        if text.startswith("<ide_opened_file") or text.startswith("<ide_selection"):
                            continue
                        return normalize_text(text)
        return None


    def load_session(self, session_file: Path) -> SessionEntry | None:
        first_timestamp: datetime | None = None
        title: str | None = None
        cwd: str | None = None

        try:
            with session_file.open("r", encoding="utf-8") as handle:
                for raw_line in handle:
                    raw_line = raw_line.strip()
                    if not raw_line:
                        continue
                    try:
                        item = json.loads(raw_line)
                    except json.JSONDecodeError:
                        continue

                    if first_timestamp is None and isinstance(item.get("timestamp"), str):
                        first_timestamp = parse_iso_timestamp(item["timestamp"])

                    if not cwd and isinstance(item.get("cwd"), str):
                        cwd = item["cwd"]

                    if item.get("type") == "user":
                        message = item.get("message")
                        if isinstance(message, dict):
                            content = message.get("content")
                            extracted = self.extract_title_from_content(content)
                            if extracted:
                                title = extracted
                                break
        except OSError:
            return None

        if first_timestamp is None:
            first_timestamp = datetime.fromtimestamp(session_file.stat().st_mtime).astimezone()

        cwd = cwd or self.decode_project_dir_name(session_file.parent.name)
        title = title or UNKNOWN_TITLE
        session_id = session_file.stem
        sub_agents_dir = session_file.parent / session_id

        return SessionEntry(
            session_id=session_id,
            session_file=session_file,
            sub_agents_dir=sub_agents_dir if sub_agents_dir.is_dir() else None,
            project_dir=session_file.parent,
            cwd=cwd,
            title=title,
            local_time=first_timestamp.strftime("%Y-%m-%d %H:%M"),
            timestamp=first_timestamp,
            is_valid=Path(cwd).exists(),
        )

    def load_sessions(self) -> list[SessionEntry]:
        sessions: list[SessionEntry] = []
        for session_file in self.iter_session_files():
            entry = self.load_session(session_file)
            if entry is not None:
                sessions.append(entry)

        sessions.sort(key=lambda item: item.timestamp, reverse=True)
        return sessions

    def delete_session(self, entry: SessionEntry) -> None:
        entry.session_file.unlink(missing_ok=True)
        if entry.sub_agents_dir and entry.sub_agents_dir.exists():
            shutil.rmtree(entry.sub_agents_dir)


class CodexSessionStore:
    label = "Codex"
    sessions_dir = Path.home() / ".codex" / "sessions"

    def iter_session_files(self) -> list[Path]:
        if not self.sessions_dir.exists():
            return []
        return sorted(self.sessions_dir.glob("*/*/*/*.jsonl"))

    def load_session(self, session_file: Path) -> SessionEntry | None:
        session_id: str | None = None
        first_timestamp: datetime | None = None
        title: str | None = None
        cwd: str | None = None

        try:
            with session_file.open("r", encoding="utf-8") as handle:
                for raw_line in handle:
                    raw_line = raw_line.strip()
                    if not raw_line:
                        continue
                    try:
                        item = json.loads(raw_line)
                    except json.JSONDecodeError:
                        continue

                    payload = item.get("payload")
                    if not isinstance(payload, dict):
                        payload = {}

                    if first_timestamp is None and isinstance(item.get("timestamp"), str):
                        first_timestamp = parse_iso_timestamp(item["timestamp"])

                    if session_id is None and isinstance(payload.get("id"), str):
                        session_id = payload["id"]

                    if isinstance(payload.get("timestamp"), str):
                        parsed_payload_ts = parse_iso_timestamp(payload["timestamp"])
                        if parsed_payload_ts is not None:
                            first_timestamp = parsed_payload_ts

                    if not cwd and isinstance(payload.get("cwd"), str):
                        cwd = payload["cwd"]

                    if title is None and payload.get("type") == "user_message":
                        message = payload.get("message")
                        if isinstance(message, str):
                            title = normalize_text(message)

                    if session_id and first_timestamp and cwd and title:
                        break
        except OSError:
            return None

        if first_timestamp is None:
            first_timestamp = datetime.fromtimestamp(session_file.stat().st_mtime).astimezone()

        title = title or UNKNOWN_TITLE
        session_id = session_id or session_file.stem
        cwd = cwd or ""

        return SessionEntry(
            session_id=session_id,
            session_file=session_file,
            sub_agents_dir=None,
            project_dir=session_file.parent,
            cwd=cwd,
            title=title,
            local_time=first_timestamp.strftime("%Y-%m-%d %H:%M"),
            timestamp=first_timestamp,
            is_valid=Path(cwd).exists(),
        )

    def load_sessions(self) -> list[SessionEntry]:
        sessions: list[SessionEntry] = []
        for session_file in self.iter_session_files():
            entry = self.load_session(session_file)
            if entry is not None:
                sessions.append(entry)

        sessions.sort(key=lambda item: item.timestamp, reverse=True)
        return sessions

    def delete_session(self, entry: SessionEntry) -> None:
        entry.session_file.unlink(missing_ok=True)


class SessionList(Static):
    DEFAULT_CSS = """
    SessionList {
        height: 1fr;
        padding: 0 1;
        color: $text;
    }
    """

    entries: list[SessionEntry] = reactive([])
    selected_index: int = reactive(0)

    def render(self) -> Text:
        text = Text()
        if not self.entries:
            text.append("No sessions found in this scope.", style="#6f7785")
            return text

        height = max(self.size.height, 2)
        item_height = 3
        items_per_page = max(height // item_height, 1)
        content_width = max(self.size.width - 3, 16)
        start = 0
        if self.selected_index >= items_per_page:
            start = self.selected_index - items_per_page + 1
        end = min(start + items_per_page, len(self.entries))
        visible = self.entries[start:end]

        for idx, entry in enumerate(visible, start=start):
            prefix = "> " if idx == self.selected_index else "  "
            title = prefix + shorten(entry.title, content_width)
            meta = f"{format_relative_time(entry.timestamp)} · {entry.cwd}"
            meta = "  " + shorten(meta, content_width)

            title_style = "bold #efefef"
            meta_style = "#787878"
            if not entry.is_valid:
                title_style = "bold #6a6f76"
                meta_style = "#5a5f67"
            if idx == self.selected_index:
                title_style = "bold #ffffff on #3a4658"
                meta_style = "#8f98a4"

            text.append(title, style=title_style)
            text.append("\n")
            text.append(meta, style=meta_style)
            if idx != end - 1:
                text.append("\n\n")

        return text


class SessionManagerApp(App[None]):
    ENABLE_COMMAND_PALETTE = False
    BASE_HELP = "Ctrl+A: Toggle Scope .  Up/Down: Select .  Enter: Delete .  Esc/Ctrl+C: Quit"
    ALL_HELP = "Ctrl+A: Toggle Scope .  Ctrl+Z: Delete Invalid .  Up/Down: Select .  Enter: Delete .  Esc/Ctrl+C: Quit"

    CSS = """
    Screen {
        background: #090b10;
        color: $text;
    }

    #root {
        width: 100%;
        height: 1fr;
    }

    #hero {
        padding: 1 3 0 3;
        height: auto;
    }

    #title {
        color: #eff2f6;
        text-style: bold;
        margin: 0;
    }

    #subtitle {
        display: none;
    }

    #scope {
        color: #707784;
        margin: 1 0 0 0;
    }

    #status {
        color: #7f8794;
        margin: 1 0 0 0;
        height: auto;
    }

    #help {
        dock: bottom;
        height: auto;
        padding: 0 3;
        background: #11151d;
        color: #a6afbc;
    }
    """

    BINDINGS = [
        Binding("ctrl+a", "toggle_scope", "Toggle Scope"),
        Binding("ctrl+z", "delete_invalid", "Delete Invalid"),
        Binding("up", "move_up", "Up"),
        Binding("down", "move_down", "Down"),
        Binding("enter", "delete_selected", "Delete"),
        Binding("escape", "quit", "Quit"),
        Binding("ctrl+c", "quit", "Quit"),
    ]

    def __init__(self, store: SessionStore) -> None:
        super().__init__()
        self.store = store
        self.current_cwd = os.getcwd()
        self.show_all = False
        self.sessions: list[SessionEntry] = []
        self.filtered: list[SessionEntry] = []
        self.selected_index = 0

    def compose(self) -> ComposeResult:
        with Container(id="root"):
            with Container(id="hero"):
                yield Static(f"{self.store.label} Session Manager", id="title")
                yield Static("", id="subtitle")
                yield Static("", id="scope")
                yield Static("", id="status")
            yield SessionList(id="list")
            yield Static("", id="help")

    def on_mount(self) -> None:
        self.refresh_sessions()

    def refresh_sessions(self, keep_session_id: str | None = None) -> None:
        self.sessions = self.store.load_sessions()
        if self.show_all:
            self.filtered = list(self.sessions)
        else:
            self.filtered = [item for item in self.sessions if item.cwd == self.current_cwd]

        if keep_session_id:
            for idx, entry in enumerate(self.filtered):
                if entry.session_id == keep_session_id:
                    self.selected_index = idx
                    break
            else:
                self.selected_index = min(self.selected_index, max(len(self.filtered) - 1, 0))
        else:
            self.selected_index = min(self.selected_index, max(len(self.filtered) - 1, 0))

        self.render_rows()
        self.refresh_scope()
        self.refresh_help()

    def render_rows(self) -> None:
        session_list = self.query_one("#list", SessionList)
        session_list.entries = list(self.filtered)
        session_list.selected_index = self.selected_index
        if not self.filtered:
            self.query_one("#status", Static).update("")

    def refresh_scope(self) -> None:
        scope = "All Projects" if self.show_all else self.current_cwd
        total = len(self.filtered)
        if self.show_all:
            invalid = sum(1 for entry in self.filtered if not entry.is_valid)
            scope_text = f"{scope} · {total} sessions · {invalid} invalid"
        else:
            scope_text = f"{scope} · {total} sessions"
        self.query_one("#scope", Static).update(scope_text)

    def set_status(self, text: str) -> None:
        self.query_one("#status", Static).update(text)

    def refresh_help(self) -> None:
        help_text = self.ALL_HELP if self.show_all else self.BASE_HELP
        self.query_one("#help", Static).update(help_text)

    def update_selection(self) -> None:
        session_list = self.query_one("#list", SessionList)
        session_list.selected_index = self.selected_index
        session_list.refresh()

    def action_move_up(self) -> None:
        if not self.filtered:
            return
        self.selected_index = (self.selected_index - 1) % len(self.filtered)
        self.update_selection()

    def action_move_down(self) -> None:
        if not self.filtered:
            return
        self.selected_index = (self.selected_index + 1) % len(self.filtered)
        self.update_selection()

    def action_toggle_scope(self) -> None:
        self.show_all = not self.show_all
        self.selected_index = 0
        self.refresh_sessions()

    def action_delete_selected(self) -> None:
        if not self.filtered:
            return
        entry = self.filtered[self.selected_index]
        self.store.delete_session(entry)
        self.set_status(f"Deleted {entry.session_id}")
        self.refresh_sessions()

    def action_delete_invalid(self) -> None:
        if not self.show_all:
            self.set_status("Available only in all-projects view")
            return

        invalid_entries = [entry for entry in self.sessions if not entry.is_valid]
        if not invalid_entries:
            self.set_status("No invalid sessions")
            return

        for entry in invalid_entries:
            self.store.delete_session(entry)

        self.selected_index = 0
        self.refresh_sessions()
        self.set_status(f"Deleted {len(invalid_entries)} invalid sessions")


def build_store(name: str) -> SessionStore | None:
    normalized = name.strip().lower()
    if normalized == "claude":
        return ClaudeSessionStore()
    if normalized == "codex":
        return CodexSessionStore()
    return None


def main() -> None:
    if len(sys.argv) != 2:
        print(f"Claude/Codex Session Manager\nUsage: {Path(sys.argv[0]).name} claude|codex")
        raise SystemExit(1)

    store = build_store(sys.argv[1])
    if store is None:
        print(f"Unsupported target: {sys.argv[1]}")
        print(f"Usage: {Path(sys.argv[0]).name} claude|codex")
        raise SystemExit(1)

    SessionManagerApp(store).run()


if __name__ == "__main__":
    main()
