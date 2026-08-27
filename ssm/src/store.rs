use std::{
    env,
    error::Error,
    io,
    path::{Path, PathBuf},
};

use chrono::{DateTime, Local, Utc};

pub(crate) const UNKNOWN_TITLE: &str = "<unknown>";
pub(crate) type StoreResult<T> = Result<T, Box<dyn Error>>;

#[derive(Clone, Debug)]
pub(crate) struct SessionEntry {
    pub(crate) session_id: String,
    pub(crate) session_file: PathBuf,
    pub(crate) cwd: String,
    pub(crate) title: String,
    pub(crate) timestamp: DateTime<Local>,
    pub(crate) is_valid: bool,
}

impl SessionEntry {
    pub(super) fn local_datetime(timestamp: f64) -> Option<DateTime<Local>> {
        if !timestamp.is_finite() {
            return None;
        }
        let mut seconds = timestamp.floor() as i64;
        let mut nanoseconds = ((timestamp - seconds as f64) * 1_000_000_000.0).round() as u32;
        if nanoseconds == 1_000_000_000 {
            seconds = seconds.checked_add(1)?;
            nanoseconds = 0;
        }
        DateTime::<Utc>::from_timestamp(seconds, nanoseconds)
            .map(|timestamp| timestamp.with_timezone(&Local))
    }
}

pub(crate) trait SessionStore {
    fn label(&self) -> &'static str;
    fn load_sessions(&self) -> StoreResult<Vec<SessionEntry>>;
    fn delete_session(&self, entry: &SessionEntry) -> StoreResult<()>;

    fn remove_file_if_exists(path: &Path) -> io::Result<()>
    where
        Self: Sized,
    {
        match std::fs::remove_file(path) {
            Ok(()) => Ok(()),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
            Err(error) => Err(error),
        }
    }
}

pub(super) fn home_dir() -> PathBuf {
    #[cfg(windows)]
    let home_dir = env::var_os("USERPROFILE");
    #[cfg(not(windows))]
    let home_dir = env::var_os("HOME");
    home_dir.map(PathBuf::from).unwrap_or_default()
}
