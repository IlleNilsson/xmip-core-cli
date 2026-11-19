//! One command line, parsed: the command, its one argument and its options.
//! Parsing knows nothing about the runtime or the console, which is what
//! lets it be tested with a list of words and nothing else. Options may sit
//! anywhere; the first bare word is the command and the second its
//! argument. A line that cannot be obeyed is its problem, which the caller
//! says and exits 2 for.

pub mod audit_arguments;
pub mod certificate_arguments;
pub mod database_arguments;
pub mod noun_arguments;
pub mod report_arguments;
pub mod secret_arguments;

use observe::Act;
use operate::surface::choice::SurfaceLine;
use operate::surface::listener::{local_refusal, remote_refusal};

use crate::command::Command;
use audit_arguments::AuditAsk;
use certificate_arguments::CertificateAsk;
use database_arguments::DatabaseAsk;
use noun_arguments::Selectors;
use report_arguments::ReportAsk;
use secret_arguments::SecretAsk;

/// What a noun's list asks, in the words every surface uses: the pattern,
/// where, which one, and in what order.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct NounAsk {
    pub pattern: Option<String>,
    pub location: Option<String>,
    pub sort: Option<String>,
    pub order: Option<String>,
    pub selectors: Selectors,
}

/// One line, parsed.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Invocation {
    pub command: Command,
    /// The command's argument, or empty for none.
    pub argument: String,
    /// One JSON document instead of text; `--follow` implies it.
    pub json: bool,
    /// JSON Lines as they change, with `health` or `measure`.
    pub follow: bool,
    pub remote: Option<String>,
    pub local: Option<String>,
    pub snapshot: Option<String>,
    /// Who acts, where the line says.
    pub who: Option<String>,
    /// What `audit` asks.
    pub audit: Option<AuditAsk>,
    /// What a noun's list asks, for `event-subscriptions`,
    /// `subscriptions` and `dead-messages`.
    pub noun: Option<NounAsk>,
    /// The act a noun's line takes on the one it names.
    pub act: Option<Act>,
    /// Where `message` and `secret` stand.
    pub location: Option<String>,
    /// Where each Send Port's page of `message`'s list starts.
    pub from: u64,
    /// The most Messages of each Send Port `message`'s list holds; 0 for a
    /// hundred.
    pub most: u32,
    pub certificate: Option<CertificateAsk>,
    pub database: Option<DatabaseAsk>,
    pub secret: Option<SecretAsk>,
    pub report: Option<ReportAsk>,
}

impl Invocation {
    /// Help, and nothing else said.
    #[must_use]
    pub fn help() -> Self {
        Self {
            command: Command::Help,
            argument: String::new(),
            json: false,
            follow: false,
            remote: None,
            local: None,
            snapshot: None,
            who: None,
            audit: None,
            noun: None,
            act: None,
            location: None,
            from: 0,
            most: 0,
            certificate: None,
            database: None,
            secret: None,
            report: None,
        }
    }

    /// What the line states about the surface to read.
    #[must_use]
    pub fn line(&self) -> SurfaceLine {
        SurfaceLine {
            remote: self.remote.clone(),
            local: self.local.clone(),
            snapshot: self.snapshot.clone(),
        }
    }

    /// The line `args` says.
    ///
    /// # Errors
    ///
    /// Why the line cannot be obeyed.
    pub fn parse(args: &[String]) -> Result<Self, String> {
        let mut read = Read::default();
        let mut index = 0;

        while index < args.len() {
            read.take(args, &mut index)?;
            index += 1;
        }

        read.finish()
    }
}

/// What the line said, option by option, before it is judged whole.
#[derive(Default)]
struct Read {
    words: Vec<String>,
    json: bool,
    follow: bool,
    remote: Option<String>,
    local: Option<String>,
    snapshot: Option<String>,
    who: Option<String>,
    audit: Option<AuditAsk>,
    audit_options: Vec<String>,
    selectors: Selectors,
    act: Option<String>,
    certificate: Option<CertificateAsk>,
    secret: Option<SecretAsk>,
    report: Option<ReportAsk>,
    noun_options: Vec<String>,
}

impl Read {
    fn take(&mut self, args: &[String], index: &mut usize) -> Result<(), String> {
        let arg = args[*index].clone();

        if let Some(taken) = audit_arguments::take(args, index, &mut self.audit) {
            taken?;
            self.audit_options.push(arg);
            return Ok(());
        }

        let taken = noun_arguments::take_selector(args, index, &mut self.selectors)
            .or_else(|| noun_arguments::take_act(&arg, &mut self.act))
            .or_else(|| certificate_arguments::take(args, index, &mut self.certificate))
            .or_else(|| secret_arguments::take(args, index, &mut self.secret))
            .or_else(|| report_arguments::take(args, index, &mut self.report));

        if let Some(taken) = taken {
            taken?;
            self.noun_options.push(arg);
            return Ok(());
        }

        let next = args.get(*index + 1).map(String::as_str);

        match arg.as_str() {
            "--json" => self.json = true,
            "--follow" => self.follow = true,
            "--remote" => {
                if let Some(refused) = remote_refusal(next) {
                    return Err(refused.to_string());
                }
                *index += 1;
                self.remote = next.map(str::to_string);
            }
            "--local" => {
                if let Some(refused) = local_refusal(next) {
                    return Err(refused.to_string());
                }
                *index += 1;
                self.local = next.map(str::to_string);
            }
            "--who" => {
                let Some(who) = next.filter(|who| !who.trim().is_empty()) else {
                    return Err("--who needs a name.".to_string());
                };
                *index += 1;
                self.who = Some(who.to_string());
            }
            "--snapshot" => {
                let Some(path) = next else {
                    return Err("--snapshot needs a path.".to_string());
                };
                *index += 1;
                self.snapshot = Some(path.to_string());
            }
            "--help" | "-h" => self.words.insert(0, "help".to_string()),
            // A status code is zero or negative, so '-22' is a word, not an
            // option.
            _ if arg.starts_with('-') && arg.parse::<i32>().is_err() => {
                return Err(format!("'{arg}' is not an xmip option. Try 'xmip help'."));
            }
            _ => self.words.push(arg),
        }

        Ok(())
    }

    fn finish(self) -> Result<Invocation, String> {
        let Some(word) = self.words.first() else {
            return Ok(Invocation {
                json: self.json,
                follow: self.follow,
                remote: self.remote,
                local: self.local,
                snapshot: self.snapshot,
                who: self.who,
                ..Invocation::help()
            });
        };

        let Some((command, fewest, most)) = Command::named(word) else {
            return Err(format!("'{word}' is not an xmip command. Try 'xmip help'."));
        };
        let arguments = self.words.len() - 1;

        if arguments < fewest || arguments > most {
            return Err(if most == 0 {
                format!("'{word}' takes no argument. Try 'xmip help'.")
            } else if fewest == 0 {
                format!("'{word}' takes at most one argument. Try 'xmip help'.")
            } else {
                format!("'{word}' takes exactly one argument. Try 'xmip help'.")
            });
        }

        self.judged(command, arguments == 1)
    }

    fn judged(self, command: Command, named: bool) -> Result<Invocation, String> {
        if self.follow && !matches!(command, Command::Health | Command::Measure) {
            return Err("--follow only applies to 'health' or 'measure'.".to_string());
        }
        if self.who.is_some()
            && command != Command::Pause
            && !(command.is_noun() && self.act.is_some())
        {
            return Err(
                "--who only applies to 'pause' and to an act of 'event-subscriptions', \
                        'subscriptions', 'dead-messages', 'message' or 'secret'."
                    .to_string(),
            );
        }

        let act = self.act.as_deref();
        let refused = noun_arguments::misplaced(command, act)
            .or_else(|| noun_arguments::foreign(command, &self.audit_options))
            .or_else(|| noun_arguments::stray(command, &self.noun_options))
            .or_else(|| certificate_arguments::refusal(command, self.certificate.as_ref(), named));
        if let Some(refused) = refused {
            return Err(refused);
        }

        let argument = if named {
            self.words[1].clone()
        } else {
            String::new()
        };
        let noun_act = match command {
            Command::Subscriptions => noun_arguments::subscription_act(act)?,
            Command::DeadMessages => noun_arguments::dead_message_act(act)?,
            Command::Message => noun_arguments::message_act(act, named)?,
            Command::EventSubscriptions => noun_arguments::event_subscription_act(act),
            _ => None,
        };
        let audit = self.audit.clone().unwrap_or_default();
        let out = self.certificate.as_ref().and_then(|ask| ask.out.as_deref());
        let database = (command == Command::Database)
            .then(|| {
                database_arguments::ask(&argument, audit.from.as_deref(), audit.to.as_deref(), out)
            })
            .transpose()?;
        let secret = (command == Command::Secret)
            .then(|| secret_arguments::ask(act, &argument, self.secret.as_ref()))
            .transpose()?;
        let node = self
            .certificate
            .as_ref()
            .and_then(|ask| ask.node.as_deref());
        let report = (command == Command::Report)
            .then(|| report_arguments::ask(&argument, self.report.as_ref(), node))
            .transpose()?;

        let pattern = named.then(|| argument.clone());
        let listed = matches!(
            command,
            Command::EventSubscriptions | Command::Subscriptions | Command::DeadMessages
        );
        let message = command == Command::Message;

        Ok(Invocation {
            command,
            json: self.json || self.follow,
            follow: self.follow,
            remote: self.remote,
            local: self.local,
            snapshot: self.snapshot,
            who: self.who,
            audit: (command == Command::Audit).then(|| AuditAsk {
                pattern: pattern.clone(),
                ..audit.clone()
            }),
            noun: listed.then(|| NounAsk {
                pattern,
                location: audit.location.clone(),
                sort: audit.sort.clone(),
                order: audit.order.clone(),
                selectors: self.selectors,
            }),
            act: noun_act,
            location: matches!(command, Command::Message | Command::Secret)
                .then(|| audit.location.clone())
                .flatten(),
            from: if message { u64::from(audit.offset) } else { 0 },
            most: if message { audit.limit } else { 0 },
            certificate: (command == Command::Certificate)
                .then(|| self.certificate.unwrap_or_default()),
            database,
            secret,
            report,
            argument,
        })
    }
}
