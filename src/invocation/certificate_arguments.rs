//! What `certificate` asks, option by option (ADR-0034, amendments
//! 2026-10-15 and 2026-10-22): a person's key and request, its signing, a
//! node's certificate from the cluster's authority or across an air gap,
//! and the authority set up, inspected, revoked from, exported and backed
//! up. `--out` is `database`'s too, and `--node` `report`'s.

use crate::command::Command;

/// Every option of `certificate`.
pub const OPTIONS: [&str; 15] = [
    "--operator",
    "--sign",
    "--out",
    "--node",
    "--cluster",
    "--authority",
    "--names",
    "--publish",
    "--inspect",
    "--revoke",
    "--reason",
    "--export",
    "--format",
    "--backup",
    "--import",
];

/// What a line asks of `certificate`, each option's value.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct CertificateAsk {
    pub operator: Option<String>,
    pub sign: Option<String>,
    pub out: Option<String>,
    pub node: Option<String>,
    pub cluster: Option<String>,
    pub authority: Option<String>,
    pub names: Option<String>,
    pub publish: Option<String>,
    pub inspect: Option<String>,
    pub revoke: Option<String>,
    pub reason: Option<String>,
    pub export: Option<String>,
    pub format: Option<String>,
    pub backup: Option<String>,
    pub import: Option<String>,
}

impl CertificateAsk {
    /// The acts the line names, as typed, in the order an act is told.
    #[must_use]
    pub fn acts(&self) -> Vec<&'static str> {
        [
            (&self.operator, "--operator"),
            (&self.sign, "--sign"),
            (&self.node, "--node"),
            (&self.inspect, "--inspect"),
            (&self.revoke, "--revoke"),
            (&self.export, "--export"),
            (&self.backup, "--backup"),
            (&self.import, "--import"),
        ]
        .into_iter()
        .filter(|(value, _)| value.is_some())
        .map(|(_, option)| option)
        .collect()
    }
}

/// Take the option at `index`: `None` where it is none of `certificate`'s;
/// else `Some`, with why where its value is missing.
pub fn take(
    args: &[String],
    index: &mut usize,
    ask: &mut Option<CertificateAsk>,
) -> Option<Result<(), String>> {
    let option = args[*index].as_str();

    if !OPTIONS.contains(&option) {
        return None;
    }

    let Some(value) = args
        .get(*index + 1)
        .filter(|value| !value.trim().is_empty())
    else {
        return Some(Err(match option {
            "--operator" => "--operator needs the person's name.".to_string(),
            "--node" | "--cluster" => format!("{option} needs a name."),
            "--names" => "--names needs the names, comma separated.".to_string(),
            "--publish" => "--publish needs the address, http://host:port.".to_string(),
            "--revoke" => "--revoke needs the certificate's serial.".to_string(),
            "--reason" => "--reason needs a word: keyCompromise, superseded, ...".to_string(),
            "--format" => "--format needs pem, der or crl.".to_string(),
            _ => format!("{option} needs a path."),
        }));
    };
    *index += 1;

    let taken = ask.get_or_insert_with(CertificateAsk::default);
    let value = Some(value.clone());

    match option {
        "--operator" => taken.operator = value,
        "--sign" => taken.sign = value,
        "--node" => taken.node = value,
        "--cluster" => taken.cluster = value,
        "--authority" => taken.authority = value,
        "--names" => taken.names = value,
        "--publish" => taken.publish = value,
        "--inspect" => taken.inspect = value,
        "--revoke" => taken.revoke = value,
        "--reason" => taken.reason = value,
        "--export" => taken.export = value,
        "--format" => taken.format = value,
        "--backup" => taken.backup = value,
        "--import" => taken.import = value,
        _ => taken.out = value,
    }

    Some(Ok(()))
}

/// Why `certificate`'s options cannot be obeyed as given — or, for another
/// command, that they are `certificate`'s — where `named` says whether the
/// line names an argument; `None` where they can.
#[must_use]
pub fn refusal(command: Command, ask: Option<&CertificateAsk>, named: bool) -> Option<String> {
    if command != Command::Certificate {
        let alone = |keep: fn(&mut CertificateAsk)| {
            ask.is_some_and(|ask| {
                let mut rest = ask.clone();
                keep(&mut rest);
                rest == CertificateAsk::default()
            })
        };
        let allowed = ask.is_none()
            || (command == Command::Database && alone(|rest| rest.out = None))
            || (command == Command::Report && alone(|rest| rest.node = None));

        return (!allowed).then(|| {
            "The options of 'certificate' only apply to 'certificate', --out to 'certificate' \
             and 'database', and --node to 'certificate' and 'report'."
                .to_string()
        });
    }

    let asked = ask.cloned().unwrap_or_default();
    let acts = asked.acts();

    if acts.len() > 1 {
        return Some(format!(
            "'certificate' takes one act a line; {} given.",
            acts.join(" and ")
        ));
    }

    let none = asked.authority.is_none();
    match acts.first().copied() {
        Some("--operator") if named => {
            Some("'certificate --operator <person>' takes no argument: the request is made.".into())
        }
        Some("--sign") if !named => {
            Some("'certificate <request> --sign <configuration>' needs the request.".into())
        }
        Some("--node") if named => Some("'certificate --node <node>' takes no argument.".into()),
        Some("--node") if !none && asked.cluster.is_none() => Some(
            "'certificate --node <node> --authority <folder>' needs --cluster <cluster>.".into(),
        ),
        Some(act @ ("--revoke" | "--export" | "--backup")) if none => {
            Some(format!("'certificate {act}' needs --authority <folder>."))
        }
        Some("--import") if !named => Some(
            "'certificate <chain> --import <folder>' needs the chain the authority signed.".into(),
        ),
        Some(act @ ("--inspect" | "--revoke" | "--export" | "--backup")) if named => {
            Some(format!("'certificate {act}' takes no argument."))
        }
        None if none => Some(
            "'certificate' needs an act: --operator, --sign, --node, --inspect, --revoke, \
             --export, --backup or --import, a carried request with --authority, or \
             --authority with --cluster to set the authority up."
                .into(),
        ),
        None if !named && asked.cluster.is_none() => Some(
            "'certificate --authority <folder>' needs --cluster <cluster> to set it up, or a \
             carried request to sign."
                .into(),
        ),
        None if named && asked.cluster.is_some() => Some(
            "'certificate <request> --authority <folder>' signs a carried request and takes no \
             --cluster."
                .into(),
        ),
        _ => None,
    }
}
