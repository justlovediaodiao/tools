use std::{
    env,
    fs::{self, File},
    io::{self, BufRead, BufReader},
    path::{Path, PathBuf},
    time::Duration,
};

use chrono::{DateTime, Local};
use crossterm::event::{self, Event, KeyCode, KeyEventKind, KeyModifiers};
use ratatui::{
    DefaultTerminal, Frame,
    layout::{Constraint, Direction, Layout, Margin},
    style::{Color, Modifier, Style},
    text::{Line, Span},
    widgets::{List, ListItem, ListState, Paragraph},
};
use serde_json::Value;
use unicode_width::{UnicodeWidthChar, UnicodeWidthStr};

const UNKNOWN_TITLE: &str = "<unknown>";

#[derive(Clone, Debug)]
struct SessionEntry {
    session_id: String,
    session_file: PathBuf,
    sub_agents_dir: Option<PathBuf>,
    cwd: String,
    title: String,
    timestamp: DateTime<Local>,
    is_valid: bool,
}

#[derive(Clone, Copy, Debug)]
enum Target {
    Claude,
    Codex,
}

impl Target {
    fn parse(value: &str) -> Option<Self> {
        match value.to_ascii_lowercase().as_str() {
            "claude" => Some(Self::Claude),
            "codex" => Some(Self::Codex),
            _ => None,
        }
    }

    fn label(self) -> &'static str {
        match self { Self::Claude => "Claude", Self::Codex => "Codex" }
    }
}

struct App {
    target: Target,
    current_cwd: String,
    all: bool,
    sessions: Vec<SessionEntry>,
    filtered: Vec<usize>,
    state: ListState,
    status: String,
}

impl App {
    fn new(target: Target) -> io::Result<Self> {
        let current_cwd = env::current_dir()?.to_string_lossy().into_owned();
        let mut app = Self {
            target, current_cwd, all: false, sessions: vec![], filtered: vec![],
            state: ListState::default(), status: String::new(),
        };
        app.reload();
        Ok(app)
    }

    fn reload(&mut self) {
        self.sessions = load_sessions(self.target);
        self.filtered = self.sessions.iter().enumerate()
            .filter(|(_, s)| self.all || s.cwd == self.current_cwd)
            .map(|(i, _)| i).collect();
        let selected = self.state.selected().unwrap_or(0)
            .min(self.filtered.len().saturating_sub(1));
        self.state.select((!self.filtered.is_empty()).then_some(selected));
    }

    fn run(&mut self, terminal: &mut DefaultTerminal) -> io::Result<()> {
        loop {
            terminal.draw(|frame| self.draw(frame))?;
            if !event::poll(Duration::from_millis(250))? { continue; }
            let Event::Key(key) = event::read()? else { continue };
            if key.kind != KeyEventKind::Press { continue; }
            match (key.code, key.modifiers) {
                (KeyCode::Esc, _) | (KeyCode::Char('c'), KeyModifiers::CONTROL) => break,
                (KeyCode::Char('a'), KeyModifiers::CONTROL) => {
                    self.all = !self.all; self.state.select(Some(0)); self.status.clear(); self.reload();
                }
                (KeyCode::Char('z'), KeyModifiers::CONTROL) if self.all => {
                    let count = self.sessions.iter().filter(|s| !s.is_valid).count();
                    if count == 0 { self.status = "No invalid sessions".into(); }
                    else { self.delete_invalid(count); }
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
        if self.filtered.is_empty() { return; }
        let i = self.state.selected().unwrap_or(0);
        self.state.select(Some(if i == 0 { self.filtered.len() - 1 } else { i - 1 }));
    }

    fn select_next(&mut self) {
        if self.filtered.is_empty() { return; }
        let i = self.state.selected().unwrap_or(0);
        self.state.select(Some((i + 1) % self.filtered.len()));
    }

    fn delete_selected(&mut self) {
        let entry = self.state.selected().and_then(|i| self.filtered.get(i))
            .and_then(|i| self.sessions.get(*i)).cloned();
        if let Some(entry) = entry {
            let id = entry.session_id.clone();
            self.status = delete_session(&entry).map(|_| format!("Deleted {id}"))
                .unwrap_or_else(|e| format!("Delete failed: {e}"));
            self.reload();
        }
    }

    fn delete_invalid(&mut self, count: usize) {
        self.status = self.sessions.iter().filter(|s| !s.is_valid).try_for_each(delete_session)
            .map(|_| format!("Deleted {count} invalid sessions"))
            .unwrap_or_else(|e| format!("Delete failed: {e}"));
        self.state.select(Some(0));
        self.reload();
    }

    fn draw(&mut self, frame: &mut Frame) {
        let [header, body, footer] = Layout::default().direction(Direction::Vertical)
            .constraints([Constraint::Length(5), Constraint::Min(2), Constraint::Length(1)])
            .areas(frame.area());
        let invalid = self.sessions.iter().filter(|s| !s.is_valid).count();
        let scope = if self.all {
            format!("All Projects · {} sessions · {invalid} invalid", self.filtered.len())
        } else { format!("{} · {} sessions", self.current_cwd, self.filtered.len()) };
        let title = vec![
            Line::styled(format!("{} Session Manager", self.target.label()), Style::default().fg(Color::White).add_modifier(Modifier::BOLD)),
            Line::raw(""), Line::styled(scope, Style::default().fg(Color::DarkGray)),
            Line::styled(self.status.as_str(), Style::default().fg(Color::Gray)),
        ];
        frame.render_widget(Paragraph::new(title), header.inner(Margin { horizontal: 2, vertical: 0 }));

        let width = body.width.saturating_sub(6) as usize;
        let items: Vec<ListItem> = if self.filtered.is_empty() {
            vec![ListItem::new(Line::styled("No sessions found in this scope.", Style::default().fg(Color::DarkGray)))]
        } else {
            self.filtered.iter().map(|i| {
                let s = &self.sessions[*i];
                let base = if s.is_valid { Style::default().fg(Color::White) } else { Style::default().fg(Color::DarkGray) };
                ListItem::new(vec![
                    Line::styled(shorten(&s.title, width), base.add_modifier(Modifier::BOLD)),
                    Line::styled(shorten(&format!("{} · {}", relative_time(s.timestamp), s.cwd), width), Style::default().fg(Color::DarkGray)),
                    Line::raw(""),
                ])
            }).collect()
        };
        let list = List::new(items).highlight_symbol("> ").highlight_style(
            Style::default().fg(Color::White).bg(Color::Rgb(58, 70, 88)).add_modifier(Modifier::BOLD));
        frame.render_stateful_widget(list, body.inner(Margin { horizontal: 2, vertical: 0 }), &mut self.state);

        let help = if self.all { "Ctrl+A: Scope · Ctrl+Z: Delete Invalid · ↑/↓: Select · Enter: Delete · Esc/Ctrl+C: Quit" }
            else { "Ctrl+A: Scope · ↑/↓: Select · Enter: Delete · Esc/Ctrl+C: Quit" };
        frame.render_widget(Paragraph::new(Line::from(Span::styled(help, Style::default().fg(Color::Gray).bg(Color::Rgb(17, 21, 29))))), footer);

    }
}

fn home_dir() -> PathBuf {
    env::var_os("HOME").or_else(|| env::var_os("USERPROFILE")).map(PathBuf::from).unwrap_or_default()
}

fn load_sessions(target: Target) -> Vec<SessionEntry> {
    let files = match target { Target::Claude => claude_files(), Target::Codex => codex_files() };
    let mut sessions: Vec<_> = files.iter().filter_map(|p| load_session(target, p)).collect();
    sessions.sort_by_key(|s| std::cmp::Reverse(s.timestamp));
    sessions
}

fn claude_files() -> Vec<PathBuf> {
    let root = home_dir().join(".claude/projects");
    read_dirs(&root).into_iter().flat_map(|dir| read_files(&dir))
        .filter(|p| p.extension().is_some_and(|e| e == "jsonl")).collect()
}

fn codex_files() -> Vec<PathBuf> {
    fn descend(path: &Path, depth: usize, out: &mut Vec<PathBuf>) {
        if depth == 0 {
            out.extend(read_files(path).into_iter().filter(|p| p.extension().is_some_and(|e| e == "jsonl")));
        } else { for dir in read_dirs(path) { descend(&dir, depth - 1, out); } }
    }
    let mut out = vec![]; descend(&home_dir().join(".codex/sessions"), 3, &mut out); out
}

fn read_dirs(path: &Path) -> Vec<PathBuf> {
    fs::read_dir(path).into_iter().flatten().flatten().map(|e| e.path()).filter(|p| p.is_dir()).collect()
}
fn read_files(path: &Path) -> Vec<PathBuf> {
    fs::read_dir(path).into_iter().flatten().flatten().map(|e| e.path()).filter(|p| p.is_file()).collect()
}

fn load_session(target: Target, path: &Path) -> Option<SessionEntry> {
    let file = File::open(path).ok()?;
    let mut id = None; let mut timestamp = None; let mut cwd = None; let mut title = None;
    for line in BufReader::new(file).lines().map_while(Result::ok) {
        let Ok(item) = serde_json::from_str::<Value>(&line) else { continue };
        if timestamp.is_none() { timestamp = json_str(&item, "timestamp").and_then(parse_timestamp); }
        match target {
            Target::Claude => {
                if cwd.is_none() { cwd = json_str(&item, "cwd").map(str::to_owned); }
                if title.is_none() && item.get("type").and_then(Value::as_str) == Some("user") {
                    title = item.pointer("/message/content").and_then(extract_claude_title);
                }
            }
            Target::Codex => {
                let payload = item.get("payload").unwrap_or(&Value::Null);
                if id.is_none() { id = json_str(payload, "id").map(str::to_owned); }
                if let Some(ts) = json_str(payload, "timestamp").and_then(parse_timestamp) { timestamp = Some(ts); }
                if cwd.is_none() { cwd = json_str(payload, "cwd").map(str::to_owned); }
                if title.is_none() && json_str(payload, "type") == Some("user_message") {
                    title = json_str(payload, "message").map(normalize_text);
                }
            }
        }
        if timestamp.is_some() && cwd.is_some() && title.is_some() && (matches!(target, Target::Claude) || id.is_some()) { break; }
    }
    let timestamp = timestamp.or_else(|| fs::metadata(path).ok()?.modified().ok().map(DateTime::<Local>::from))
        .unwrap_or_else(Local::now);
    let session_id = id.unwrap_or_else(|| path.file_stem().unwrap_or_default().to_string_lossy().into_owned());
    let cwd = cwd.unwrap_or_else(|| match target {
        Target::Claude => decode_claude_dir(path.parent().and_then(Path::file_name).unwrap_or_default().to_string_lossy().as_ref()),
        Target::Codex => String::new(),
    });
    let sub = path.parent().map(|p| p.join(&session_id)).filter(|p| p.is_dir());
    Some(SessionEntry { session_id, session_file: path.to_owned(), sub_agents_dir: sub,
        is_valid: Path::new(&cwd).exists(), cwd, title: title.unwrap_or_else(|| UNKNOWN_TITLE.into()), timestamp })
}

fn json_str<'a>(value: &'a Value, key: &str) -> Option<&'a str> { value.get(key)?.as_str() }
fn parse_timestamp(value: &str) -> Option<DateTime<Local>> { DateTime::parse_from_rfc3339(value).ok().map(|d| d.with_timezone(&Local)) }
fn decode_claude_dir(name: &str) -> String {
    if let Some(rest) = name.strip_prefix('-') { format!("/{}", rest.trim_start_matches('-').replace('-', "/")) } else { name.into() }
}
fn extract_claude_title(value: &Value) -> Option<String> {
    if let Some(text) = value.as_str() {
        if text.starts_with("<local-command") || text.starts_with("<command-name") { return None; }
        return Some(normalize_text(text));
    }
    value.as_array()?.iter().find_map(|item| {
        if json_str(item, "type") != Some("text") { return None; }
        let text = json_str(item, "text")?;
        (!text.starts_with("<ide_opened_file") && !text.starts_with("<ide_selection")).then(|| normalize_text(text))
    })
}
fn normalize_text(text: &str) -> String {
    let mut out = String::new(); let mut in_tag = false;
    for ch in text.chars() {
        match ch { '<' => in_tag = true, '>' if in_tag => { in_tag = false; out.push(' '); }, _ if !in_tag => out.push(ch), _ => {} }
    }
    out.split_whitespace().collect::<Vec<_>>().join(" ")
}
fn shorten(text: &str, width: usize) -> String {
    if text.width() <= width { return text.into(); }
    if width <= 3 { return take_width(text, width); }
    format!("{}...", take_width(text, width - 3).trim_end())
}
fn take_width(text: &str, width: usize) -> String {
    let mut used = 0; text.chars().take_while(|ch| { let w = ch.width().unwrap_or(0); let fits = used + w <= width; if fits { used += w; } fits }).collect()
}
fn relative_time(timestamp: DateTime<Local>) -> String {
    let seconds = (Local::now() - timestamp).num_seconds().max(0);
    let (n, unit) = if seconds < 60 { return "just now".into() }
        else if seconds < 3600 { (seconds / 60, "minute") }
        else if seconds < 86400 { (seconds / 3600, "hour") }
        else if seconds < 604800 { (seconds / 86400, "day") }
        else if seconds < 2592000 { (seconds / 604800, "week") }
        else if seconds < 31536000 { (seconds / 2592000, "month") }
        else { (seconds / 31536000, "year") };
    format!("{n} {unit}{} ago", if n == 1 { "" } else { "s" })
}
fn delete_session(entry: &SessionEntry) -> io::Result<()> {
    match fs::remove_file(&entry.session_file) { Ok(()) => {}, Err(e) if e.kind() == io::ErrorKind::NotFound => {}, Err(e) => return Err(e) }
    if let Some(dir) = &entry.sub_agents_dir { match fs::remove_dir_all(dir) { Ok(()) => {}, Err(e) if e.kind() == io::ErrorKind::NotFound => {}, Err(e) => return Err(e) } }
    Ok(())
}

fn main() -> io::Result<()> {
    let args: Vec<_> = env::args().collect();
    let Some(target) = args.get(1).and_then(|s| Target::parse(s)) else {
        eprintln!("Claude/Codex Session Manager\nUsage: {} claude|codex", args.first().map(String::as_str).unwrap_or("ssm"));
        std::process::exit(1);
    };
    let mut terminal = ratatui::init();
    let result = App::new(target).and_then(|mut app| app.run(&mut terminal));
    ratatui::restore();
    result
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test] fn normalizes_tags_and_space() { assert_eq!(normalize_text(" hi <tag>x</tag>  there "), "hi x there"); }
    #[test] fn shortens_unicode_by_cells() { assert_eq!(shorten("😀😃😄😁", 7), "😀😃..."); }
    #[test] fn decodes_claude_path() { assert_eq!(decode_claude_dir("-Users-ethan-work"), "/Users/ethan/work"); }
}
