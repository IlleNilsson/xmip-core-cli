//! What the executable needs beside it and in it: `cli.toml`, the document
//! that says which surface a command reads (ADR-0052 clause 3), copied
//! beside the built `xmip` where it is newer than the one there, as the
//! .NET build kept the newest, so a developer's clone answers as shipped;
//! and the
//! Xmip mark linked into it as its Windows icon, the resource the estate's
//! `Sync-XmipEstate -Brand` makes from the mark and copies here.

use std::path::{Path, PathBuf};

const DOCUMENT: &str = "cli.toml";
const RESOURCE: &str = "src/xmip-x.res";

fn main() {
    println!("cargo:rerun-if-changed={DOCUMENT}");
    println!("cargo:rerun-if-changed={RESOURCE}");
    let Ok(root) = std::env::var("CARGO_MANIFEST_DIR") else {
        return;
    };
    let root = PathBuf::from(root);

    if let Some(beside) = executable_folder() {
        let placed = beside.join(DOCUMENT);
        let written = |path: &Path| {
            std::fs::metadata(path)
                .and_then(|file| file.modified())
                .ok()
        };
        if written(&root.join(DOCUMENT)) > written(&placed) {
            let _ = std::fs::copy(root.join(DOCUMENT), placed);
        }
    }

    let windows = std::env::var("CARGO_CFG_TARGET_OS").is_ok_and(|os| os == "windows");
    let msvc = std::env::var("CARGO_CFG_TARGET_ENV").is_ok_and(|env| env == "msvc");
    let resource = root.join(RESOURCE);
    if windows && msvc && resource.is_file() {
        println!("cargo:rustc-link-arg-bins={}", resource.display());
    }
}

// The profile's folder the executable is built into: three above this
// build's own output, `<target>/<profile>/build/<package>/out`.
fn executable_folder() -> Option<PathBuf> {
    let out = std::env::var("OUT_DIR").ok()?;
    Path::new(&out).ancestors().nth(3).map(Path::to_path_buf)
}
