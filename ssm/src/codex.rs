use std::{fs, path::PathBuf};

use rusqlite::{Connection, OpenFlags};

use crate::store::{SessionEntry, SessionStore, StoreResult, UNKNOWN_TITLE, home_dir};

pub(crate) struct CodexSessionStore {
    database: Option<PathBuf>,
}

impl CodexSessionStore {
    pub(crate) fn new() -> Self {
        Self {
            database: Self::find_database(),
        }
    }

    fn find_database() -> Option<PathBuf> {
        let codex_dir = home_dir().join(".codex");
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

    fn normalize_cwd(cwd: String) -> String {
        #[cfg(windows)]
        {
            cwd.strip_prefix(r"\\?\").unwrap_or(&cwd).to_owned()
        }
        #[cfg(not(windows))]
        {
            cwd
        }
    }
}

impl SessionStore for CodexSessionStore {
    fn label(&self) -> &'static str {
        "Codex"
    }

    fn load_sessions(&self) -> StoreResult<Vec<SessionEntry>> {
        let Some(database) = self.database.as_deref() else {
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
                row.get::<_, Option<String>>(5)?.unwrap_or_default(),
            ))
        })?;

        let mut sessions = vec![];
        for row in rows {
            let (session_id, session_file, cwd, title, activity_time, source) = row?;
            if !session_file.is_file() || source.to_lowercase().contains("subagent") {
                continue;
            }
            let cwd = Self::normalize_cwd(cwd);
            let Some(timestamp) = SessionEntry::local_datetime(activity_time) else {
                continue;
            };
            let is_valid = std::path::Path::new(&cwd).exists();
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

    fn delete_session(&self, entry: &SessionEntry) -> StoreResult<()> {
        Self::remove_file_if_exists(&entry.session_file)?;
        Ok(())
    }
}
