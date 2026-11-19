//! What `secret` asks (ADR-0019, amendment 2026-10-29): the secrets a node
//! keeps listed, or one set — its name the argument, `--set`, its `--kind`
//! and its value from `--file` or standard input, never the line.

use std::io::Read;

/// The options of `secret` alone.
pub const OPTIONS: [&str; 2] = ["--kind", "--file"];

/// What a line asks of `secret`.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct SecretAsk {
    pub name: String,
    pub kind: Option<String>,
    pub file: Option<String>,
    pub set: bool,
}

impl SecretAsk {
    /// The value to set: the file's whole text where `--file` names one,
    /// else standard input, its line end taken off.
    ///
    /// # Errors
    ///
    /// The file or the input could not be read.
    pub fn value(&self, input: &mut dyn Read) -> std::io::Result<String> {
        if let Some(path) = &self.file {
            return std::fs::read_to_string(path);
        }

        let mut text = String::new();
        input.read_to_string(&mut text)?;
        Ok(text.trim_end_matches(['\r', '\n']).to_string())
    }
}

/// Take the option at `index`: `None` where it is none of `secret`'s; else
/// `Some`, with why where its value is missing.
pub fn take(
    args: &[String],
    index: &mut usize,
    ask: &mut Option<SecretAsk>,
) -> Option<Result<(), String>> {
    let option = args[*index].as_str();

    if !OPTIONS.contains(&option) {
        return None;
    }

    let Some(value) = args
        .get(*index + 1)
        .filter(|value| !value.trim().is_empty())
    else {
        return Some(Err(if option == "--kind" {
            "--kind needs a kind: password, key or certificate.".to_string()
        } else {
            "--file needs the path of the file holding the value.".to_string()
        }));
    };
    *index += 1;

    let asked = ask.get_or_insert_with(SecretAsk::default);
    if option == "--kind" {
        asked.kind = Some(value.clone());
    } else {
        asked.file = Some(value.clone());
    }

    Some(Ok(()))
}

/// What the line asks of `secret`: a list, or one set, from its act, its
/// argument `name` and its options.
///
/// # Errors
///
/// A name or an option with no `--set`, an act other than `--set`, or a
/// set naming no secret or no kind.
pub fn ask(act: Option<&str>, name: &str, asked: Option<&SecretAsk>) -> Result<SecretAsk, String> {
    let ask = asked.cloned().unwrap_or_default();

    let Some(act) = act else {
        return if !name.is_empty() || ask.kind.is_some() || ask.file.is_some() {
            Err(
                "'secret <name>' needs --set, --kind and the value from --file or standard \
                 input; 'secret' alone lists."
                    .to_string(),
            )
        } else {
            Ok(ask)
        };
    };

    if act != "set" {
        return Err(format!(
            "REFUSED: --{act}: a secret is set, and --set is its act."
        ));
    }
    if name.is_empty() || ask.kind.is_none() {
        return Err(
            "REFUSED: to set a secret, name it and its kind: 'secret <name> --set \
                    --kind password|key|certificate'."
                .to_string(),
        );
    }

    Ok(SecretAsk {
        name: name.to_string(),
        set: true,
        ..ask
    })
}
