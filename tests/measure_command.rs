//! `xmip measure` over a fake surface: the six figures in order, a figure
//! nothing published a dash in text and null in a document, and nothing
//! published at all a complaint.

#[path = "support/fake.rs"]
mod fake;

use fake::FakeSurface;
use observe::Counted;
use operate::surface::scope_tree::ROOT;
use xmip_core_cli::command::measure;

fn run(surface: &FakeSurface, json: bool) -> (i32, String, String) {
    let (mut output, mut error) = (Vec::new(), Vec::new());
    let exit = measure::run(surface, ROOT, json, &mut output, &mut error);
    (
        exit,
        String::from_utf8(output).expect("UTF-8"),
        String::from_utf8(error).expect("UTF-8"),
    )
}

#[test]
fn text_says_the_six_figures_in_order_and_an_unpublished_one_as_a_dash() {
    let mut surface = FakeSurface::new(Vec::new());
    surface.measurements.push((Counted::Streams, 12));
    surface.measurements.push((Counted::Messages, 9));
    surface.measurements.push((Counted::Journeys, 10));
    surface.measurements.push((Counted::Retrying, 1));

    let (exit, output, _) = run(&surface, false);

    assert_eq!(exit, 0);
    assert_eq!(
        output.trim(),
        "Streams 12  Messages 9  Journeys 10  Bytes –  Retrying 1  Failed –"
    );
}

#[test]
fn json_keeps_an_unpublished_figure_null() {
    let mut surface = FakeSurface::new(Vec::new());
    surface.measurements.push((Counted::Failed, 3));

    let (_, output, _) = run(&surface, true);

    assert!(output.contains("\"streams\":null"), "{output}");
    assert!(output.contains("\"failed\":3"), "{output}");
}

#[test]
fn nothing_published_is_a_complaint_and_exit_one() {
    let surface = FakeSurface::new(Vec::new());

    let (exit, _, error) = run(&surface, false);

    assert_eq!(exit, 1);
    assert!(error.contains("Nothing measured"), "{error}");
}
