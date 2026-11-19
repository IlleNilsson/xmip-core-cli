//! `xmip certificate`: a person's certificate — a fresh key and a request
//! naming the person (`--operator`), and its signing by the authority a
//! node's configuration names (`--sign`; ADR-0034, amendment 2026-10-15); a
//! node's key and certificate from the cluster's own authority (`--node`;
//! ADR-0015, amendment 2026-10-21), or its request, signing and import
//! across an air gap; and the authority set up, inspected, revoked from,
//! exported and backed up (ADR-0034, amendment 2026-10-22). Every act is the
//! runtime's — `provision`'s, `runtime::operator_certificate`'s and
//! `operate::authority`'s — and audited as every command is; none is an
//! MCP tool.

mod answer;
mod authority_act;

use std::io::Write;
use std::path::{Path, PathBuf};

use operate::{authority, certificate};
use serde_json::{Value, json};

use crate::invocation::Invocation;
use crate::invocation::certificate_arguments::CertificateAsk;
use answer::Answer;

/// Answer the line's `certificate`.
pub fn run(invocation: &Invocation, output: &mut dyn Write, error: &mut dyn Write) -> i32 {
    let asked = invocation.certificate.clone().unwrap_or_default();
    let mut answer = Answer {
        json: invocation.json,
        output,
        error,
    };

    act(&asked, &invocation.argument, &mut answer)
}

/// The one act `asked` names, on the file `request` the line names.
fn act(asked: &CertificateAsk, request: &str, answer: &mut Answer<'_>) -> i32 {
    let authority = asked.authority.as_deref().map(full).unwrap_or_default();
    let names: Vec<String> = asked
        .names
        .as_deref()
        .unwrap_or_default()
        .split(',')
        .map(str::trim)
        .filter(|name| !name.is_empty())
        .map(str::to_string)
        .collect();
    let at = |path: &str| shown(&full(path));

    if let Some(exit) = kept(asked, request, &authority, answer) {
        return exit;
    }
    if let Some(node) = &asked.node {
        let folder = at(asked.out.as_deref().unwrap_or("."));
        if asked.authority.is_none() {
            let arguments = json!({ "node": node, "names": names, "folder": folder });
            return authority_act::requested(&asked_of("request", &arguments), node, answer);
        }
        let cluster = asked.cluster.as_deref().unwrap_or_default();
        return issued(&authority, cluster, node, &names, &folder, answer);
    }
    if let Some(person) = &asked.operator {
        return requested(person, &at(asked.out.as_deref().unwrap_or(".")), answer);
    }
    if let Some(configuration) = &asked.sign {
        let written = asked.out.as_deref().map(full);
        return signed(
            &full(request),
            &full(configuration),
            written.as_deref(),
            answer,
        );
    }
    if asked.authority.is_some() && !request.is_empty() {
        let request = at(request);
        let written = asked.out.as_deref().map(at);
        let mut arguments =
            json!({ "authority": shown(&authority), "request": request, "names": names });
        if let Some(written) = &written {
            arguments["out"] = json!(written);
        }
        let asked = asked_of("sign", &arguments);
        return authority_act::signed(&asked, (&request, written.as_deref()), answer);
    }

    let mut arguments = json!({
        "authority": shown(&authority),
        "name": certificate::authority_name(asked.cluster.as_deref().unwrap_or_default()),
    });
    if let Some(publish) = asked
        .publish
        .as_deref()
        .filter(|said| !said.trim().is_empty())
    {
        arguments["publish"] = json!(publish);
    }
    authority_act::inspected(&asked_of("set-up", &arguments), answer, "Set up")
}

// An act on what the authority keeps — inspected, revoked, exported, backed
// up or imported — where the line names one.
fn kept(
    asked: &CertificateAsk,
    request: &str,
    authority: &Path,
    answer: &mut Answer<'_>,
) -> Option<i32> {
    let at = |path: &str| shown(&full(path));

    if let Some(inspected) = &asked.inspect {
        let path = full(inspected);
        return Some(if path.is_file() {
            authority_act::read(
                &asked_of("read", &json!({ "certificate": shown(&path) })),
                answer,
            )
        } else {
            let state = asked_of("inspect", &json!({ "authority": shown(&path) }));
            authority_act::inspected(&state, answer, "Inspected")
        });
    }
    let authority = shown(authority);
    if let Some(serial) = &asked.revoke {
        let reason = asked.reason.as_deref().unwrap_or("unspecified");
        let arguments = json!({ "authority": authority, "serial": serial, "reason": reason });
        return Some(authority_act::revoked(
            &asked_of("revoke", &arguments),
            answer,
        ));
    }
    if let Some(path) = &asked.export {
        let format = asked.format.as_deref().unwrap_or("pem");
        let path = at(path);
        let arguments = json!({ "authority": authority, "path": path, "format": format });
        let asked = asked_of("export", &arguments);
        return Some(authority_act::written(&asked, (&path, format), answer));
    }
    if let Some(folder) = &asked.backup {
        let folder = at(folder);
        let arguments = json!({ "authority": authority, "folder": folder });
        let asked = asked_of("back-up", &arguments);
        return Some(authority_act::written(&asked, (&folder, "backup"), answer));
    }
    asked.import.as_ref().map(|folder| {
        let arguments = json!({ "certificate": at(request), "folder": at(folder) });
        authority_act::imported(&asked_of("import", &arguments), answer)
    })
}

// One act on the authority asked: the answer, or the refusal in words.
fn asked_of(act: &str, arguments: &Value) -> Result<Value, String> {
    authority::act(act, arguments).map_err(|refused| format!("REFUSED: {refused}"))
}

// A node's fresh key and certificate from the cluster's authority, kept in
// `authority` and made there where none is, for its own name and `names`.
fn issued(
    authority: &Path,
    cluster: &str,
    node: &str,
    names: &[String],
    folder: &str,
    answer: &mut Answer<'_>,
) -> i32 {
    let issued = provision::node::issue_into(
        authority,
        &certificate::authority_name(cluster),
        node,
        names,
        Path::new(folder),
        provision::now(),
    );
    let (certificate, key, anchors) = issued.as_ref().map_or_else(
        |_| (String::new(), String::new(), String::new()),
        |written| {
            (
                shown(&written.certificate),
                shown(&written.key),
                shown(&written.anchor),
            )
        },
    );
    let refused = issued
        .as_ref()
        .err()
        .map(|refused| format!("REFUSED: {}", refused.message));

    answer.said(
        refused.as_deref(),
        |json| {
            json.string("node", node);
            json.string("certificate", &certificate);
            json.string("key", &key);
            json.string("anchors", &anchors);
        },
        &format!(
            "Issued {node} {certificate}, {key} and {anchors}. Give the node the three as its \
             [operate] names them; the key is its alone."
        ),
        |_: &mut dyn Write| {},
    )
}

// A person's fresh key and request, made into `folder`.
fn requested(person: &str, folder: &str, answer: &mut Answer<'_>) -> i32 {
    let made = provision::operator::request_into(person, Path::new(folder));
    let (key, request) = made.as_ref().map_or_else(
        |_| (String::new(), String::new()),
        |written| (shown(&written.key), shown(&written.request)),
    );
    let refused = made
        .as_ref()
        .err()
        .map(|refused| format!("REFUSED: {}", refused.message));

    answer.said(
        refused.as_deref(),
        |json| {
            json.string("person", person);
            json.string("key", &key);
            json.string("request", &request);
        },
        &format!(
            "Made {key} and {request} for {person}. Keep the key; send the request to whoever \
             signs for the cluster's authority."
        ),
        |_: &mut dyn Write| {},
    )
}

// A person's request signed by the authority `configuration` names, its
// chain to stdout where no file is named.
fn signed(
    request: &Path,
    configuration: &Path,
    written: Option<&Path>,
    answer: &mut Answer<'_>,
) -> i32 {
    let signed = certificate::sign_request(request, configuration, written);

    if let (Ok(chain), None, false) = (&signed, &written, answer.json) {
        let _ = answer.output.write_all(chain.as_bytes());
        return 0;
    }

    let request = shown(request);
    let written = written.map(shown);
    let chain = signed.as_ref().map_or("", String::as_str);
    let sentence = format!(
        "Signed {request}: the chain is {}.",
        written.as_deref().unwrap_or_default()
    );

    answer.said(
        signed.as_ref().err().map(String::as_str),
        |json| {
            json.string("request", &request);
            json.string_or_null("written", written.as_deref());
            json.string("chain", chain);
        },
        &sentence,
        |_: &mut dyn Write| {},
    )
}

fn full(path: &str) -> PathBuf {
    std::path::absolute(Path::new(path)).unwrap_or_else(|_| PathBuf::from(path))
}

fn shown(path: &Path) -> String {
    path.display().to_string()
}
