/**
 * Golden-recording dialog fixture (#139): loaded into the recorded pi via
 * `--extension`, it raises the three round-tripping dialog kinds from the
 * first agent_start — i.e. during a prompt turn, the way guard-style
 * extensions actually raise dialogs. Deliberately NOT session_start: a
 * dialog awaited there is unanswerable in RPC mode (the stdin reader only
 * attaches after session_start hooks complete) and pi exits 0 — that trap
 * is pinned separately by session-start-dialog.ts. The driving scenario
 * answers confirm with confirmed:true, input with a value, and select with
 * cancelled:true (Esc-equivalent), in that order. Deliberately untyped:
 * the fixture must load standalone from a temp HOME with no package
 * resolution.
 */
let raised = false;

export default function dialogFixture(pi) {
	pi.on("agent_start", async (_event, ctx) => {
		if (raised) {
			return;
		}
		raised = true;
		await ctx.ui.confirm("golden-confirm", "Confirm the golden dialog round-trip?");
		await ctx.ui.input("golden-input", "type golden");
		await ctx.ui.select("golden-select", ["alpha", "beta"]);
	});
}
