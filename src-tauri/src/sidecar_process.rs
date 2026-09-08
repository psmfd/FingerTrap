//! Bounded stdin delivery with a kill handle independent of the pipe writer.
use std::io::Write;
use std::process::Command;
use std::sync::mpsc::{self, Receiver, SyncSender};
use std::sync::{Arc, Condvar, Mutex};
use std::time::Duration;

use shared_child::SharedChild;
use zeroize::Zeroizing;

const QUEUE_CAPACITY: usize = 8;
// Match the RPC body ceiling, allowing a small framing header.
const MAX_FRAME_BYTES: usize = 4 * 1024 * 1024 + 1024;
const WRITE_TIMEOUT: Duration = Duration::from_secs(5);

struct Frame {
    bytes: Zeroizing<Vec<u8>>,
    completed: SyncSender<Result<(), String>>,
}

pub struct WriteReceipt {
    completed: Receiver<Result<(), String>>,
    child: Arc<SharedChild>,
}

impl WriteReceipt {
    pub fn wait(self) -> Result<(), String> {
        match self.completed.recv_timeout(WRITE_TIMEOUT) {
            Ok(result) => result,
            Err(_) => {
                // A timed-out partial frame cannot be retried on the same stream.
                // The independent process handle also interrupts a blocked write.
                let _ = self.child.kill();
                Err("sidecar input delivery failed or timed out".into())
            }
        }
    }
}

/// Wait only on process-exit notification, never stdin or output EOF.
/// Returns true when the deadline required forced termination.
pub fn finish_shutdown(
    child: Option<&SharedChild>,
    terminated: &(Mutex<bool>, Condvar),
    grace: Duration,
) -> std::io::Result<bool> {
    let Some(child) = child else {
        return Ok(false);
    };
    let (lock, ready) = terminated;
    let guard = lock.lock().unwrap();
    let (guard, _) = ready
        .wait_timeout_while(guard, grace, |done| !*done)
        .unwrap();
    if *guard {
        return Ok(false);
    }
    drop(guard);
    child.kill()?;
    Ok(true)
}

pub struct SidecarProcess {
    pub child: Arc<SharedChild>,
    writer: SyncSender<Frame>,
}

impl SidecarProcess {
    pub fn spawn(command: &mut Command) -> std::io::Result<Self> {
        let child = Arc::new(SharedChild::spawn(command)?);
        let mut stdin = child
            .take_stdin()
            .ok_or_else(|| std::io::Error::other("sidecar stdin was not configured as a pipe"))?;
        let (writer, frames) = mpsc::sync_channel::<Frame>(QUEUE_CAPACITY);
        let writer_child = Arc::clone(&child);
        std::thread::spawn(move || {
            while let Ok(frame) = frames.recv() {
                let result = stdin.write_all(&frame.bytes).map_err(|e| e.to_string());
                let failed = result.is_err();
                let _ = frame.completed.send(result);
                if failed {
                    let _ = writer_child.kill();
                    break;
                }
            }
        });
        Ok(Self { child, writer })
    }

    pub fn enqueue(&self, payload: &[u8]) -> Result<WriteReceipt, String> {
        if payload.len() > MAX_FRAME_BYTES {
            return Err("sidecar input frame exceeds the size limit".into());
        }
        let (completed, receipt) = mpsc::sync_channel(1);
        self.writer
            .try_send(Frame {
                bytes: Zeroizing::new(payload.to_vec()),
                completed,
            })
            .map_err(|_| "sidecar input queue is full or closed".to_string())?;
        Ok(WriteReceipt {
            completed: receipt,
            child: Arc::clone(&self.child),
        })
    }
}

#[cfg(all(test, unix))]
mod tests {
    use super::*;
    use std::io::Read;
    use std::process::Stdio;
    use std::time::Instant;

    fn shell(script: &str) -> SidecarProcess {
        SidecarProcess::spawn(
            Command::new("/bin/sh")
                .args(["-c", script])
                .stdin(Stdio::piped())
                .stdout(Stdio::piped())
                .stderr(Stdio::null()),
        )
        .unwrap()
    }

    #[test]
    fn queued_frames_arrive_in_order_without_interleaving() {
        let process = shell("head -c 6");
        let first = process.enqueue(b"abc").unwrap();
        let second = process.enqueue(b"def").unwrap();
        first.wait().unwrap();
        second.wait().unwrap();
        let mut output = String::new();
        process
            .child
            .take_stdout()
            .unwrap()
            .read_to_string(&mut output)
            .unwrap();
        assert!(process.child.wait().unwrap().success());
        assert_eq!(output, "abcdef");
    }

    #[test]
    fn full_input_queue_does_not_block_kill_or_exit_observation() {
        // exec avoids leaving a shell descendant behind if the test fails.
        let process = shell("exec sleep 30");
        let payload = vec![b'x'; MAX_FRAME_BYTES];
        let mut pending = Vec::new();
        for _ in 0..QUEUE_CAPACITY + 2 {
            match process.enqueue(&payload) {
                Ok(receipt) => pending.push(receipt),
                Err(_) => break,
            }
        }
        assert!(pending.len() <= QUEUE_CAPACITY + 1);
        let start = Instant::now();
        assert!(finish_shutdown(
            Some(&process.child),
            &(Mutex::new(false), Condvar::new()),
            Duration::from_millis(50),
        )
        .unwrap());
        process.child.wait().unwrap();
        for receipt in pending {
            assert!(receipt.wait().is_err());
        }
        assert!(start.elapsed() < Duration::from_secs(2));
    }

    #[test]
    fn observed_exit_and_absent_child_do_not_wait_for_deadline() {
        let process = shell("exit 0");
        process.child.wait().unwrap();
        let start = Instant::now();
        assert!(!finish_shutdown(
            Some(&process.child),
            &(Mutex::new(true), Condvar::new()),
            Duration::from_secs(20)
        )
        .unwrap());
        assert!(!finish_shutdown(
            None,
            &(Mutex::new(false), Condvar::new()),
            Duration::from_secs(20)
        )
        .unwrap());
        assert!(start.elapsed() < Duration::from_secs(1));
    }

    #[test]
    fn dead_child_rejects_input_and_oversized_frames_are_rejected() {
        let process = shell("exit 0");
        process.child.wait().unwrap();
        assert!(process.enqueue(&vec![0; MAX_FRAME_BYTES + 1]).is_err());
        assert!(process.enqueue(b"frame").unwrap().wait().is_err());
    }
}
