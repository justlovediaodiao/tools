use std::{
    env,
    fs,
    io,
    path::{Path, PathBuf},
    time::Duration,
};

use chrono::{DateTime, Local, Utc};
use crossterm::event::{self, Event, KeyCode, KeyEventKind, KeyModifiers};
use ratatui::{
    DefaultTerminal, Frame,
    layout::{Constraint, Direction, Layout, Margin},
    style::{Color, Modifier, Style},
    text::{Line, Span},
    widgets::{Block, List, ListItem, ListState, Paragraph},
};
use rusqlite::{Connection, OpenFlags};
use unicode_width::{UnicodeWidthChar, UnicodeWidthStr};

const UNKNOWN_TITLE: &str = "<unknown>";

#[derive(Clone, Debug)]
struct SessionEntry {
    session_id: String,
    session_file: PathBuf,
    cwd: String,
    title: String,
    timestamp: DateTime<Local>,
    is_valid: bool,
}

struct App {
    database: Option<PathBuf>,
    current_cwd: String,
    show_all: bool,
    sessions: Vec<SessionEntry>,
    filtered: Vec<usize>,
    state: ListState,
    status: String,
}

impl App {
    fn new() -> io::Result<Self> {
        let current_cwd = env::current_dir()?.to_string_lossy().into_owned();
        let mut app = Self {
            database: find_database(),
            current_cwd,
            show_all: false,
            sessions: vec![],
            filtered: vec![],
            state: ListState::default(),
            status: String::new(),
        };
        app.reload();
        Ok(app)
    }

    fn reload(&mut self) {
        self.sessions = match load_sessions(self.database.as_deref()) {
            Ok(sessions) => sessions,
            Err(error) => {
                self.status = format!("Load failed: {error}");
                vec![]
            }
        };
        self.filtered = self
            .sessions
            .iter()
            .enumerate()
            .filter(|(_, session)| self.show_all || same_cwd(&session.cwd, &self.current_cwd))
            .map(|(index, _)| index)
            .collect();

        let selected = self
            .state
            .selected()
            .unwrap_or(0)
            .min(self.filtered.len().saturating_sub(1));
        self.state
            .select((!self.filtered.is_empty()).then_some(selected));
    }

    fn run(&mut self, terminal: &mut DefaultTerminal) -> io::Result<()> {
        loop {
            terminal.draw(|frame| self.draw(frame))?;
            if !event::poll(Duration::from_millis(250))? {
                continue;
            }
            let Event::Key(key) = event::read()? else {
                continue;
            };
            if key.kind != KeyEventKind::Press {
                continue;
            }

            match (key.code, key.modifiers) {
                (KeyCode::Esc, _) | (KeyCode::Char('c'), KeyModifiers::CONTROL) => break,
                (KeyCode::Char('a'), KeyModifiers::CONTROL) => {
                    self.show_all = !self.show_all;
                    self.state.select(Some(0));
                    self.reload();
                }
                (KeyCode::Char('z'), KeyModifiers::CONTROL) if self.show_all => {
                    self.delete_invalid()
                }
                (KeyCode::Up, _) => self.select_previous(),
                (KeyCode::Down, _) => self.select_next(),
                (KeyCode::Enter, _) if !self.filtered.is_empty() => self.delete_selected(),
                _ => {}
            }
        }
        Ok(())
    }

    fn select_previous(&mut self) {
        if self.filtered.is_empty() {
            return;
        }
        let index = self.state.selected().unwrap_or(0);
        self.state.select(Some(if index == 0 {
            self.filtered.len() - 1
        } else {
            index - 1
        }));
    }

    fn select_next(&mut self) {
        if self.filtered.is_empty() {
            return;
        }
        let index = self.state.selected().unwrap_or(0);
        self.state.select(Some((index + 1) % self.filtered.len()));
    }

    fn delete_selected(&mut self) {
        let entry = self
            .state
            .selected()
            .and_then(|index| self.filtered.get(index))
            .and_then(|index| self.sessions.get(*index))
            .cloned();

        if let Some(entry) = entry {
            let id = entry.session_id.clone();
            self.status = delete_session(&entry)
                .map(|_| format!("Deleted {id}"))
                .unwrap_or_else(|error| format!("Delete failed: {error}"));
            self.reload();
        }
    }

    fn delete_invalid(&mut self) {
        let invalid: Vec<_> = self
            .sessions
            .iter()
            .filter(|session| !session.is_valid)
            .cloned()
            .collect();
        if invalid.is_empty() {
            self.status = "No invalid sessions".into();
            return;
        }

        let count = invalid.len();
        let result = invalid.iter().try_for_each(delete_session);
        self.state.select(Some(0));
        self.reload();
        self.status = result
            .map(|_| format!("Deleted {count} invalid sessions"))
            .unwrap_or_else(|error| format!("Delete failed: {error}"));
    }

    fn draw(&mut self, frame: &mut Frame) {
        let area = frame.area();
        frame.render_widget(
            Block::default().style(
                Style::default()
                    .fg(Color::White)
                    .bg(Color::Rgb(9, 11, 16)),
            ),
            area,
        );
        let [header, body, footer] = Layout::default()
            .direction(Direction::Vertical)
            .constraints([
                Constraint::Length(5),
                Constraint::Min(2),
                Constraint::Length(1),
            ])
            .areas(area);

        let scope = if self.show_all {
            let invalid = self
                .sessions
                .iter()
                .filter(|session| !session.is_valid)
                .count();
            format!(
                "All Projects · {} sessions · {invalid} invalid",
                self.filtered.len()
            )
        } else {
            format!("{} · {} sessions", self.current_cwd, self.filtered.len())
        };
        let title = vec![
            Line::styled(
                "Codex Session Manager",
                Style::default()
                    .fg(Color::Rgb(239, 242, 246))
                    .add_modifier(Modifier::BOLD),
            ),
            Line::raw(""),
            Line::styled(scope, Style::default().fg(Color::Rgb(112, 119, 132))),
            Line::styled(
                self.status.as_str(),
                Style::default().fg(Color::Rgb(127, 135, 148)),
            ),
        ];
        frame.render_widget(
            Paragraph::new(title),
            header.inner(Margin {
                horizontal: 2,
                vertical: 0,
            }),
        );

        let width = (body.width.saturating_sub(6) as usize).max(16);
        let selected = self.state.selected();
        let items = if self.filtered.is_empty() {
            vec![ListItem::new(Line::styled(
                "No sessions found in this scope.",
                Style::default().fg(Color::Rgb(111, 119, 133)),
            ))]
        } else {
            self.filtered
                .iter()
                .enumerate()
                .map(|(visible_index, index)| {
                    let session = &self.sessions[*index];
                    let is_selected = selected == Some(visible_index);
                    let (title_style, meta_style) = if is_selected {
                        (
                            Style::default()
                                .fg(Color::White)
                                .bg(Color::Rgb(58, 70, 88))
                                .add_modifier(Modifier::BOLD),
                            Style::default().fg(Color::Rgb(143, 152, 164)),
                        )
                    } else if session.is_valid {
                        (
                            Style::default()
                                .fg(Color::Rgb(239, 239, 239))
                                .add_modifier(Modifier::BOLD),
                            Style::default().fg(Color::Rgb(120, 120, 120)),
                        )
                    } else {
                        (
                            Style::default()
                                .fg(Color::Rgb(106, 111, 118))
                                .add_modifier(Modifier::BOLD),
                            Style::default().fg(Color::Rgb(90, 95, 103)),
                        )
                    };
                    ListItem::new(vec![
                        Line::styled(
                            format!(
                                "{}{}",
                                if is_selected { "> " } else { "  " },
                                Self::shorten(&session.title, width)
                            ),
                            title_style,
                        ),
                        Line::styled(
                            format!(
                                "  {}",
                                Self::shorten(
                                    &format!(
                                        "{} · {}",
                                        session.timestamp.format("%Y-%m-%d %H:%M"),
                                        session.cwd
                                    ),
                                    width,
                                )
                            ),
                            meta_style,
                        ),
                        Line::raw(""),
                    ])
                })
                .collect()
        };
        let list = List::new(items);
        frame.render_stateful_widget(
            list,
            body.inner(Margin {
                horizontal: 2,
                vertical: 0,
            }),
            &mut self.state,
        );

        let help = if self.show_all {
            "Ctrl+A: Toggle Scope .  Ctrl+Z: Delete Invalid .  Up/Down: Select .  Enter: Delete .  Esc/Ctrl+C: Quit"
        } else {
            "Ctrl+A: Toggle Scope .  Up/Down: Select .  Enter: Delete .  Esc/Ctrl+C: Quit"
        };
        frame.render_widget(
            Paragraph::new(Line::from(Span::raw(help))).style(
                Style::default()
                    .fg(Color::Rgb(166, 175, 188))
                    .bg(Color::Rgb(17, 21, 29)),
            ),
            footer,
        );
    }

    fn shorten(text: &str, width: usize) -> String {
        let text = text.split_whitespace().collect::<Vec<_>>().join(" ");
        if width <= 1 {
            return text.chars().take(width).collect();
        }
        if text.width() <= width {
            return text;
        }
        if width <= 3 {
            return Self::take_width(&text, width);
        }
        format!("{}...", Self::take_width(&text, width - 3).trim_end())
    }

    fn take_width(text: &str, width: usize) -> String {
        let mut used = 0;
        text.chars()
            .take_while(|character| {
                let character_width = character.width().unwrap_or(0);
                let fits = used + character_width <= width;
                if fits {
                    used += character_width;
                }
                fits
            })
            .collect()
    }
}

fn same_cwd(left: &str, right: &str) -> bool {
    #[cfg(windows)]
    {
        let prefix = r"\\?\";
        left.strip_prefix(prefix)
            .unwrap_or(left)
            .eq_ignore_ascii_case(right.strip_prefix(prefix).unwrap_or(right))
    }
    #[cfg(not(windows))]
    {
        left == right
    }
}

fn find_database() -> Option<PathBuf> {
    #[cfg(windows)]
    let home_dir = env::var_os("USERPROFILE")?;
    #[cfg(not(windows))]
    let home_dir = env::var_os("HOME")?;

    let codex_dir = PathBuf::from(home_dir).join(".codex");
    fs::read_dir(codex_dir)
        .ok()?
        .flatten()
        .filter_map(|entry| {
            let path = entry.path();
            let version = path
                .file_name()?
                .to_str()?
                .strip_prefix("state_")?
                .strip_suffix(".sqlite")?
                .parse::<u64>()
                .ok()?;
            Some((version, path))
        })
        .max_by_key(|(version, _)| *version)
        .map(|(_, path)| path)
}

fn load_sessions(database: Option<&Path>) -> rusqlite::Result<Vec<SessionEntry>> {
    let Some(database) = database else {
        return Ok(vec![]);
    };
    let connection = Connection::open_with_flags(database, OpenFlags::SQLITE_OPEN_READ_ONLY)?;
    let mut statement = connection.prepare(
        "
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
        ",
    )?;

    let rows = statement.query_map(rusqlite::params![UNKNOWN_TITLE], |row| {
        Ok((
            row.get::<_, String>(0)?,
            PathBuf::from(row.get::<_, String>(1)?),
            row.get::<_, String>(2)?,
            row.get::<_, String>(3)?,
            row.get::<_, f64>(4)?,
            row.get::<_, String>(5)?,
        ))
    })?;

    let mut sessions = vec![];
    for row in rows {
        let (session_id, session_file, cwd, title, activity_time, source) = row?;
        if !session_file.is_file() || source.to_lowercase().contains("subagent") {
            continue;
        }

        let seconds = activity_time.trunc() as i64;
        let nanoseconds = (activity_time.fract() * 1_000_000_000.0) as u32;
        let Some(timestamp) = DateTime::<Utc>::from_timestamp(seconds, nanoseconds)
            .map(|timestamp| timestamp.with_timezone(&Local))
        else {
            continue;
        };
        let is_valid = Path::new(&cwd).exists();
        sessions.push(SessionEntry {
            session_id,
            session_file,
            cwd,
            title,
            timestamp,
            is_valid,
        });
    }
    Ok(sessions)
}

fn delete_session(entry: &SessionEntry) -> io::Result<()> {
    match fs::remove_file(&entry.session_file) {
        Ok(()) => Ok(()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(error),
    }
}

fn main() -> io::Result<()> {
    let mut terminal = ratatui::init();
    let result = App::new().and_then(|mut app| app.run(&mut terminal));
    ratatui::restore();
    result
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn shortens_unicode_by_cells() {
        assert_eq!(App::shorten("😀😃😄😁", 7), "😀😃...");
    }

    #[test]
    fn normalizes_whitespace_when_shortening() {
        assert_eq!(App::shorten(" a\n b ", 10), "a b");
    }
}
