//! `xmip abi`: both boundaries, as aligned text and as one document.

use xmip_core_cli::command::abi;

fn answered(json: bool) -> (i32, String) {
    let mut output = Vec::new();
    let exit = abi::run(json, &mut output);
    (exit, String::from_utf8(output).expect("UTF-8"))
}

#[test]
fn lists_both_boundaries_as_aligned_text() {
    let (exit, said) = answered(false);
    let lines: Vec<&str> = said.split('\n').collect();

    assert_eq!(exit, 0);
    assert_eq!(
        lines[0],
        format!("module version      {}", ::abi::XMIP_ABI_VERSION)
    );
    assert_eq!(
        lines[1],
        format!("module entrypoint   {}", ::abi::XMIP_ENTRYPOINT)
    );
    assert_eq!(
        lines[3],
        format!("operate version     {}", ::abi::XMIP_OPERATE_VERSION)
    );
    assert_eq!(
        lines[4],
        format!("operate entrypoint  {}", ::abi::XMIP_OPERATE_ENTRYPOINT)
    );
}

#[test]
fn lists_both_boundaries_as_one_document() {
    let (exit, said) = answered(true);
    let document: serde_json::Value = serde_json::from_str(&said).expect("one document");

    assert_eq!(exit, 0);
    assert_eq!(document["module"]["entrypoint"], ::abi::XMIP_ENTRYPOINT);
    assert_eq!(document["operate"]["version"], ::abi::XMIP_OPERATE_VERSION);
}
