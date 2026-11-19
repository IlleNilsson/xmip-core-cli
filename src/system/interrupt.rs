//! Ctrl+C, the operator's to end a follow with: once asked for, it sets a
//! flag the follow reads and the process ends cleanly after it, its audit
//! record written; without it the process dies mid-line.
//!
//! In `src/system/`, the one folder of this crate that may hold unsafe
//! code: the handler is the operating system's to call.
#![allow(unsafe_code)]

use std::sync::Once;
use std::sync::atomic::{AtomicBool, Ordering};

/// Set once Ctrl+C was pressed.
static INTERRUPTED: AtomicBool = AtomicBool::new(false);

/// The flag Ctrl+C sets, its handler installed the first time it is asked
/// for.
pub fn stop() -> &'static AtomicBool {
    static INSTALLED: Once = Once::new();
    INSTALLED.call_once(install);
    &INTERRUPTED
}

#[cfg(windows)]
fn install() {
    use windows_sys::Win32::Foundation::{FALSE, TRUE};
    use windows_sys::Win32::System::Console::{
        CTRL_BREAK_EVENT, CTRL_C_EVENT, SetConsoleCtrlHandler,
    };
    use windows_sys::core::BOOL;

    unsafe extern "system" fn handled(event: u32) -> BOOL {
        if event == CTRL_C_EVENT || event == CTRL_BREAK_EVENT {
            INTERRUPTED.store(true, Ordering::Release);
            TRUE
        } else {
            FALSE
        }
    }

    // SAFETY: `handled` is a plain function with the signature the call
    // takes, and it only stores to an atomic.
    unsafe {
        SetConsoleCtrlHandler(Some(handled), TRUE);
    }
}

#[cfg(unix)]
fn install() {
    use nix::sys::signal::{SaFlags, SigAction, SigHandler, SigSet, Signal, sigaction};

    extern "C" fn handled(_: nix::libc::c_int) {
        INTERRUPTED.store(true, Ordering::Release);
    }

    let action = SigAction::new(
        SigHandler::Handler(handled),
        SaFlags::SA_RESTART,
        SigSet::empty(),
    );
    // SAFETY: the handler only stores to an atomic, which is
    // async-signal-safe.
    unsafe {
        let _ = sigaction(Signal::SIGINT, &action);
    }
}

#[cfg(not(any(windows, unix)))]
fn install() {}
