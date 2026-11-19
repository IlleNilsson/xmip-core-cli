//! `xmip probe <library>`: a module library opened and judged by the
//! runtime's one loader, the rule a node opens its modules by
//! (`operate::module_probe`; ADR-0012, amendment 2026-11-17), and what it
//! says it is reported: the first of the seven conformance rules in
//! section 11 of the ABI's specification, driven from outside the module.
//! What the module logged goes to stderr; whether it loaded at all, and
//! whether it conforms, are the probe's answers, the ones
//! `Get-XmipModuleDescriptor` reports too.

use std::io::Write;
use std::path::Path;

use abi::ffi::status;
use abi::status_meaning;
use operate::module_probe;

use crate::json::Json;
use crate::say;
use crate::text_table::left;

/// Probe one module library.
pub fn run(library: &str, json: bool, output: &mut dyn Write, error: &mut dyn Write) -> i32 {
    if !Path::new(library).is_file() {
        say(error, &format!("No file at {library}."));
        return 2;
    }

    let full = std::path::absolute(library).unwrap_or_else(|_| library.into());
    let result = module_probe::probe(&full.display().to_string());

    for line in &result.log {
        say(error, &format!("  {line}"));
    }

    // Whether it loaded at all is the probe's answer too, the one the
    // cmdlet refuses on.
    if !result.loaded() {
        say(error, &result.unloadable);
        return 1;
    }

    if result.status != status::OK {
        let named = status_meaning::name(result.status)
            .map_or_else(|| result.status.to_string(), str::to_string);
        say(
            error,
            &format!(
                "{} returned {named} — {}",
                abi::XMIP_ENTRYPOINT,
                result.last_error
            ),
        );
        if result.status == status::UNSUPPORTED {
            say(
                error,
                &format!(
                    "That is the answer a module gives when it cannot speak abi_version {}.",
                    abi::XMIP_ABI_VERSION
                ),
            );
        }
        return 1;
    }

    if json {
        say(
            output,
            &Json::document(|json| {
                json.string("library", library);
                json.string("provider", &result.provider);
                json.string("module", &result.module);
                json.string("standard", &result.standard);
                json.number("abi_version", result.abi_version);
                json.string("trait_version", &result.trait_version);
                json.string("module_version", &result.module_version);
                json.boolean("conforms", result.conforms);
            }),
        );
    } else {
        let rows = [
            ("provider", shown(&result.provider, "(empty)")),
            ("module", shown(&result.module, "(empty)")),
            ("standard", shown(&result.standard, "(none)")),
            ("abi version", result.abi_version.to_string()),
            ("trait version", result.trait_version.clone()),
            ("module version", result.module_version.clone()),
        ];
        for (name, value) in rows {
            say(output, &format!("{}{value}", left(name, 20)));
        }
    }

    if result.conforms {
        return 0;
    }

    say(error, &result.refusal);
    1
}

fn shown(value: &str, when_empty: &str) -> String {
    if value.is_empty() {
        when_empty.to_string()
    } else {
        value.to_string()
    }
}
