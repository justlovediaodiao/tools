#!/usr/bin/env python3
from __future__ import annotations

import os
import re
import sqlite3
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

from rich.cells import cell_len, set_cell_size
from rich.text import Text
from textual.app import App, ComposeResult
from textual.binding import Binding
from textual.containers import Container
from textual.reactive import reactive
from textual.widgets import Static


UNKNOWN_TITLE = "<unknown>"


def same_cwd(left: str, right: str) -> bool:
    if os.name == "nt":
        prefix = "\\\\?\\"
        return left.removeprefix(prefix).lower() == right.removeprefix(prefix).lower()
    return left == right


@dataclass
class SessionEntry:
    session_id: str
    session_file: Path
    cwd: str
    title: str
    timestamp: datetime
    is_valid: bool


class CodexSessionStore:
    label = "Codex"

    def __init__(self) -> None:
        self.database = self.find_database()

    def find_database(self) -> Path | None:
        codex_dir = Path.home() / ".codex"
        databases: list[tuple[int, Path]] = []
        for path in codex_dir.glob("state_*.sqlite"):
            match = re.fullmatch(r"state_(\d+)\.sqlite", path.name)
            if match:
                databases.append((int(match.group(1)), path))
        return max(databases, default=(0, None), key=lambda item: item[0])[1]

    def load_sessions(self) -> list[SessionEntry]:
        if self.database is None:
            return []

        sessions: list[SessionEntry] = []
        query = """
            SELECT
                id,
                rollout_path,
                cwd,
                COALESCE(NULLIF(name, ''), NULLIF(title, ''),
                         NULLIF(first_user_message, ''), ?) AS display_title,
                CASE
                    WHEN COALESCE(updated_at_ms, 0) > 0 THEN updated_at_ms / 1000.0
                    ELSE updated_at
                END AS activity_time,
                source
            FROM threads
            WHERE archived = 0
            ORDER BY activity_time DESC
        """

        connection = sqlite3.connect(f"{self.database.as_uri()}?mode=ro", uri=True)
        try:
            connection.row_factory = sqlite3.Row
            with connection:
                rows = connection.execute(query, (UNKNOWN_TITLE,)).fetchall()
        finally:
            connection.close()

        for row in rows:
            session_file = Path(row["rollout_path"])
            source = str(row["source"] or "").lower()
            if not session_file.is_file() or "subagent" in source:
                continue

            cwd = row["cwd"] or ""
            sessions.append(
                SessionEntry(
                    session_id=row["id"],
                    session_file=session_file,
                    cwd=cwd,
                    title=row["display_title"],
                    timestamp=datetime.fromtimestamp(row["activity_time"]).astimezone(),
                    is_valid=Path(cwd).exists(),
                )
            )

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

    @staticmethod
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
            title = prefix + self.shorten(entry.title, content_width)
            meta = f"{entry.timestamp:%Y-%m-%d %H:%M} · {entry.cwd}"
            meta = "  " + self.shorten(meta, content_width)

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

    def __init__(self, store: CodexSessionStore) -> None:
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

    def refresh_sessions(self) -> None:
        try:
            self.sessions = self.store.load_sessions()
        except sqlite3.Error as error:
            self.sessions = []
            self.set_status(f"Load failed: {error}")
        if self.show_all:
            self.filtered = list(self.sessions)
        else:
            self.filtered = [item for item in self.sessions if same_cwd(item.cwd, self.current_cwd)]

        self.selected_index = min(self.selected_index, max(len(self.filtered) - 1, 0))

        self.render_rows()
        self.refresh_scope()
        self.refresh_help()

    def render_rows(self) -> None:
        session_list = self.query_one("#list", SessionList)
        session_list.entries = list(self.filtered)
        session_list.selected_index = self.selected_index

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
        try:
            self.store.delete_session(entry)
        except OSError as error:
            self.set_status(f"Delete failed: {error}")
        else:
            self.set_status(f"Deleted {entry.session_id}")
        self.refresh_sessions()

    def action_delete_invalid(self) -> None:
        if self.show_all:
            self.delete_invalid()

    def delete_invalid(self) -> None:
        invalid_entries = [entry for entry in self.sessions if not entry.is_valid]
        if not invalid_entries:
            self.set_status("No invalid sessions")
            return

        try:
            for entry in invalid_entries:
                self.store.delete_session(entry)
        except OSError as error:
            self.selected_index = 0
            self.refresh_sessions()
            self.set_status(f"Delete failed: {error}")
            return

        self.selected_index = 0
        self.refresh_sessions()
        self.set_status(f"Deleted {len(invalid_entries)} invalid sessions")


def main() -> None:
    SessionManagerApp(CodexSessionStore()).run()


if __name__ == "__main__":
    main()
