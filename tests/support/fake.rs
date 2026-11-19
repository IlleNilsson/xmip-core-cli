//! A surface a test writes: its records, its figures, its run, its lists and
//! what an act comes to, every answer composed by the surface's own rules,
//! as the command line's tests have always read.

#![allow(dead_code, reason = "each test file uses its own part of the fake")]

use std::sync::Mutex;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};

use observe::{Act, Count, Counted, Health, HealthRecord, Noun, Subscription};
use operate::surface::acted::{ActOutcome, Acted, ScopeAction, ScopeOperation};
use operate::surface::failed_message_list::FailedMessageList;
use operate::surface::run_header::RunHeader;
use operate::surface::scope_tree;
use operate::surface::{OperatorSurface, SurfaceChange};
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::run::{self, Writers};
use xmip_core_cli::surface_open::Surface;

/// When every record a fake holds was observed: 2026-09-11T12:00:00Z.
pub const SEEN: i64 = 1_789_128_000 * 1_000_000_000;

/// What the fake says it is.
pub const SOURCE: &str = "FAKE — a test wrote these";

/// What a fake tells each read: its count, and the records about to be
/// read, which it may change.
pub type OnRead = Box<dyn Fn(usize, &Mutex<Vec<HealthRecord>>) + Send + Sync>;

/// A surface a test wrote.
pub struct FakeSurface {
    pub records: Mutex<Vec<HealthRecord>>,
    /// Each counted kind's figure, the last given standing.
    pub measurements: Vec<(Counted, u64)>,
    pub reads: AtomicUsize,
    pub started: RunHeader,
    pub failed: FailedMessageList,
    pub listed: Vec<Subscription>,
    pub acted: (ActOutcome, String),
    /// Told each read, its count, before the records are read: a test
    /// changes them, or stops a follow, there.
    pub on_read: Option<OnRead>,
}

impl FakeSurface {
    /// A fake holding `records`.
    pub fn new(records: Vec<HealthRecord>) -> Self {
        Self {
            records: Mutex::new(records),
            measurements: Vec::new(),
            reads: AtomicUsize::new(0),
            started: RunHeader::default(),
            failed: FailedMessageList::unlisted(),
            listed: Vec::new(),
            acted: (ActOutcome::Refused, "a fake takes no act".to_string()),
            on_read: None,
        }
    }
}

impl OperatorSurface for FakeSurface {
    fn source(&self) -> String {
        SOURCE.to_string()
    }

    fn health(&self, scope: &str) -> Vec<HealthRecord> {
        let reads = self.reads.fetch_add(1, Ordering::AcqRel) + 1;
        if let Some(told) = &self.on_read {
            told(reads, &self.records);
        }
        let records = self.records.lock().expect("records");
        let beneath: Vec<HealthRecord> = records
            .iter()
            .filter(|record| scope_tree::beneath(&record.scope, scope))
            .cloned()
            .collect();
        scope_tree::worst_first(&beneath)
    }

    fn measure(&self, scope: &str, counted: Counted) -> Option<Count> {
        let given = self
            .measurements
            .iter()
            .rev()
            .find(|(kind, _)| *kind == counted);
        given.map(|&(_, value)| Count {
            scope: scope.to_string(),
            counted,
            value,
            window_start_unix_nanos: SEEN - 60_000_000_000,
            window_end_unix_nanos: SEEN,
            observed_unix_nanos: SEEN,
        })
    }

    fn pause_scope(&self, _scope: &str, _who: &str) -> String {
        "a fake cannot be paused".to_string()
    }

    fn resume_scope(&self, _scope: &str) -> String {
        "a fake cannot be resumed".to_string()
    }

    // A fake applies nothing it is asked to pause or resume.
    fn control(&self, scope: &str, action: ScopeAction, who: &str) -> ScopeOperation {
        let result = match action {
            ScopeAction::Pause => self.pause_scope(scope, who),
            ScopeAction::Resume => self.resume_scope(scope),
        };

        ScopeOperation {
            scope: scope.to_string(),
            action,
            applied: false,
            result,
        }
    }

    fn run(&self) -> RunHeader {
        self.started.clone()
    }

    fn watch(&self, stop: &AtomicBool, each: &mut dyn FnMut(&SurfaceChange)) {
        let mut revision = 0;
        while !stop.load(Ordering::Acquire) {
            each(&SurfaceChange {
                revision,
                source: SOURCE.to_string(),
            });
            revision += 1;
            std::thread::yield_now();
        }
    }

    fn subscriptions(&self) -> Vec<Subscription> {
        self.listed.clone()
    }

    fn failed_messages(&self, _scope: &str, _from: u64, _most: u32) -> FailedMessageList {
        self.failed.clone()
    }

    fn act(&self, _node: &str, noun: Noun, _target: &str, _act: Act, _who: &str) -> Acted {
        match noun {
            Noun::Subscription => Acted {
                outcome: self.acted.0,
                said: self.acted.1.clone(),
            },
            _ => operate::surface::declined(SOURCE, noun),
        }
    }
}

/// A leaf at `scope`, observed when every fake record was.
pub fn leaf(scope: &str, health: Health, severity: u8, evidence: &str) -> HealthRecord {
    HealthRecord {
        scope: scope.to_string(),
        health,
        severity,
        evidence: evidence.to_string(),
        observed_unix_nanos: SEEN,
    }
}

/// What a command answered: its exit, its output and its error.
pub struct Answered {
    pub exit: i32,
    pub output: String,
    pub error: String,
}

/// `line` answered over `surface`, as the executable answers it.
pub fn answered(line: &[&str], surface: impl Fn() -> Surface) -> Answered {
    let args: Vec<String> = line.iter().map(ToString::to_string).collect();
    let invocation = match Invocation::parse(&args) {
        Ok(invocation) => invocation,
        Err(problem) => {
            return Answered {
                exit: 2,
                output: String::new(),
                error: problem,
            };
        }
    };
    let audit = xaudit::program_audit::ProgramAudit::new("xmip-test", None::<audit_file::FileSink>);
    let (mut output, mut error, mut input) = (Vec::new(), Vec::new(), std::io::empty());
    let stop = AtomicBool::new(false);
    let open = |_: &Invocation| Ok(surface());
    let exit = run::answer(
        &invocation,
        &audit,
        &open,
        &mut Writers {
            output: &mut output,
            error: &mut error,
            input: &mut input,
            stop: &stop,
        },
    );

    Answered {
        exit,
        output: String::from_utf8(output).expect("UTF-8"),
        error: String::from_utf8(error).expect("UTF-8"),
    }
}
