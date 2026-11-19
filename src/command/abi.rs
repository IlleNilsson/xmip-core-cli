//! `xmip abi`: the two boundaries this build speaks — the module boundary a
//! Module plugs into and the operator boundary a surface drives from
//! (ADR-0027 clause 2, versioned apart). `abi::boundaries::Boundaries` is
//! the answer, the one `Get-XmipAbi` emits too; only the rendering is here.
//! Nothing is loaded.

use std::io::Write;

use abi::boundaries::Boundaries;

use crate::json::Json;
use crate::say;
use crate::text_table::left;

/// Print the boundaries: exit 0.
pub fn run(json: bool, output: &mut dyn Write) -> i32 {
    let abi = Boundaries::current();

    if json {
        say(
            output,
            &Json::document(|json| {
                json.object("module", |json| {
                    json.number("version", abi.module_version);
                    json.string("entrypoint", abi.module_entrypoint);
                    json.string("library_file_name", &abi.module_library_file_name);
                });
                json.object("operate", |json| {
                    json.number("version", abi.operate_version);
                    json.string("entrypoint", abi.operate_entrypoint);
                });
            }),
        );
        return 0;
    }

    let rows = [
        ("module version", abi.module_version.to_string()),
        ("module entrypoint", abi.module_entrypoint.to_string()),
        ("module file name", abi.module_library_file_name.clone()),
        ("operate version", abi.operate_version.to_string()),
        ("operate entrypoint", abi.operate_entrypoint.to_string()),
    ];
    for (name, value) in rows {
        say(output, &format!("{}{value}", left(name, 20)));
    }

    0
}
