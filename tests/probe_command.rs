//! `xmip probe <library>`: a library that is not there is a line that
//! cannot be obeyed, said on stderr naming it.

use xmip_core_cli::command::probe::run;

#[test]
fn a_missing_library_is_a_usage_error_on_stderr() {
    let folder = file::temporary::Folder::new("cli-probe");
    let missing = folder.join("no-such-module.dll").display().to_string();
    let (mut output, mut error) = (Vec::new(), Vec::new());

    let exit = run(&missing, false, &mut output, &mut error);

    assert_eq!(exit, 2);
    assert!(String::from_utf8(error).expect("UTF-8").contains(&missing));
}
