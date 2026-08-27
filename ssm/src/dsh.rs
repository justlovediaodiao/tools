use std::{
    fs,
    io::{self, BufRead, BufReader, Write},
    path::{Path, PathBuf},
};

use chrono::{DateTime, Local};
use serde_json::Value;

use crate::store::{SessionEntry, SessionStore, StoreResult, UNKNOWN_TITLE, home_dir};

pub(crate) struct DshSessionStore {
    dsh_dir: PathBuf,
    workspace_file: PathBuf,
}

impl DshSessionStore {
    pub(crate) fn new(dsh_dir: Option<PathBuf>) -> Self {
        let dsh_dir = dsh_dir.unwrap_or_else(|| home_dir().join(".dsh"));
        let workspace_file = dsh_dir.join("storages").join("workspace.json");
        Self {
            dsh_dir,
            workspace_file,
        }
    }

    fn project_key(cwd: &str) -> String {
        let mut readable = String::with_capacity(cwd.len());
        let mut replacing_separator = false;
        for character in cwd.chars() {
            if matches!(character, '/' | '\\' | ':') {
                if !replacing_separator {
                    readable.push('-');
                }
                replacing_separator = true;
            } else {
                readable.push(character);
                replacing_separator = false;
            }
        }

        let mut key = String::new();
        for character in readable.chars() {
            if character != '~'
                && (character.is_ascii_alphanumeric() || matches!(character, '.' | '_' | '-'))
            {
                key.push(character);
            } else {
                key.push('~');
                key.push_str(&format!("{:04X}", character as u32));
            }
        }
        let key = key.trim_start_matches('-');
        let key = if key.is_empty() { "root" } else { key };
        format!("--{}--", key.chars().take(251).collect::<String>())
    }

    fn session_file(&self, session_id: &str, cwd: &str) -> PathBuf {
        let session_dir = self
            .dsh_dir
            .join("sessions")
            .join(Self::project_key(cwd))
            .join(session_id);
        let compressed = session_dir.join("session.jsonl.zstd");
        if compressed.is_file() {
            compressed
        } else {
            session_dir.join("session.jsonl")
        }
    }

    fn read_session_metadata(path: &Path) -> StoreResult<(String, String, DateTime<Local>)> {
        let file = fs::File::open(path)?;
        let reader: Box<dyn BufRead> = if path
            .file_name()
            .and_then(|name| name.to_str())
            .is_some_and(|name| name.ends_with(".zstd"))
        {
            Box::new(BufReader::new(zstd::stream::read::Decoder::new(file)?))
        } else {
            Box::new(BufReader::new(file))
        };
        let mut records = reader.lines();
        let header: Value = serde_json::from_str(
            &records
                .next()
                .ok_or_else(|| Self::invalid_data("empty session file"))??,
        )?;
        let cwd = match header.get("cwd") {
            None => String::new(),
            Some(cwd) => cwd
                .as_str()
                .ok_or_else(|| Self::invalid_data("session cwd is not a string"))?
                .to_owned(),
        };
        let mut title = UNKNOWN_TITLE.to_owned();
        let mut activity_time = Self::json_number(&header, "createdAt")?;

        for line in records {
            let record: Value = serde_json::from_str(&line?)?;
            if record.get("time").is_some() {
                activity_time = Self::json_number(&record, "time")?;
            }
            if record.get("type").and_then(Value::as_str) == Some("session/title") {
                title = record
                    .pointer("/data/title")
                    .and_then(Value::as_str)
                    .ok_or_else(|| {
                        Self::invalid_data("session/title record has no string title")
                    })?
                    .to_owned();
            }
        }

        let timestamp = SessionEntry::local_datetime(activity_time / 1000.0)
            .ok_or_else(|| Self::invalid_data("invalid session timestamp"))?;
        Ok((cwd, title, timestamp))
    }

    fn read_workspace(&self) -> StoreResult<Value> {
        Ok(serde_json::from_reader(fs::File::open(
            &self.workspace_file,
        )?)?)
    }

    fn write_workspace(&self, workspace_data: &Value) -> StoreResult<()> {
        let parent = self
            .workspace_file
            .parent()
            .ok_or_else(|| Self::invalid_data("workspace path has no parent"))?;
        let mut temporary_file = tempfile::NamedTempFile::new_in(parent)?;
        serde_json::to_writer_pretty(temporary_file.as_file_mut(), workspace_data)?;
        temporary_file.as_file_mut().write_all(b"\n")?;
        temporary_file.as_file_mut().sync_all()?;
        temporary_file.persist(&self.workspace_file)?;
        Ok(())
    }

    fn invalid_data(message: impl Into<String>) -> io::Error {
        io::Error::new(io::ErrorKind::InvalidData, message.into())
    }

    fn json_number(value: &Value, field: &str) -> io::Result<f64> {
        value
            .get(field)
            .and_then(Value::as_f64)
            .ok_or_else(|| {
                Self::invalid_data(format!("missing or invalid numeric field {field}"))
            })
    }
}

impl SessionStore for DshSessionStore {
    fn label(&self) -> &'static str {
        "DeepSeek Harness"
    }

    fn load_sessions(&self) -> StoreResult<Vec<SessionEntry>> {
        if !self.workspace_file.is_file() {
            return Ok(vec![]);
        }
        let workspace_data = self.read_workspace()?;
        let workspaces = workspace_data
            .pointer("/tables/workspaces")
            .and_then(Value::as_object)
            .ok_or_else(|| {
                Self::invalid_data("workspace.json has no tables.workspaces object")
            })?;
        let mut sessions = vec![];

        for workspace in workspaces.values() {
            let workspace_path = workspace
                .get("path")
                .and_then(Value::as_str)
                .ok_or_else(|| Self::invalid_data("workspace has no string path"))?;
            let session_ids = workspace
                .get("sessionIds")
                .and_then(Value::as_array)
                .ok_or_else(|| Self::invalid_data("workspace has no sessionIds array"))?;
            for session_id in session_ids {
                let session_id = session_id
                    .as_str()
                    .ok_or_else(|| Self::invalid_data("session id is not a string"))?;
                let session_file = self.session_file(session_id, workspace_path);
                let (cwd, title, timestamp) = Self::read_session_metadata(&session_file)?;
                let is_valid = Path::new(&cwd).is_dir();
                sessions.push(SessionEntry {
                    session_id: session_id.to_owned(),
                    session_file,
                    cwd,
                    title,
                    timestamp,
                    is_valid,
                });
            }
        }

        sessions.sort_by(|left, right| right.timestamp.cmp(&left.timestamp));
        Ok(sessions)
    }

    fn delete_session(&self, entry: &SessionEntry) -> StoreResult<()> {
        let mut workspace_data = self.read_workspace()?;
        let workspaces = workspace_data
            .pointer_mut("/tables/workspaces")
            .and_then(Value::as_object_mut)
            .ok_or_else(|| {
                Self::invalid_data("workspace.json has no tables.workspaces object")
            })?;
        for workspace in workspaces.values_mut() {
            let session_ids = workspace
                .get_mut("sessionIds")
                .and_then(Value::as_array_mut)
                .ok_or_else(|| Self::invalid_data("workspace has no sessionIds array"))?;
            session_ids.retain(|session_id| {
                session_id.as_str() != Some(entry.session_id.as_str())
            });
        }
        let archived_ids = workspace_data
            .pointer_mut("/global/archivedSessionIds")
            .and_then(Value::as_array_mut)
            .ok_or_else(|| {
                Self::invalid_data("workspace.json has no global.archivedSessionIds array")
            })?;
        archived_ids
            .retain(|session_id| session_id.as_str() != Some(entry.session_id.as_str()));

        self.write_workspace(&workspace_data)?;
        Self::remove_file_if_exists(&entry.session_file)?;
        if let Some(session_dir) = entry.session_file.parent() {
            let _ = fs::remove_dir(session_dir);
        }
        Ok(())
    }
}
