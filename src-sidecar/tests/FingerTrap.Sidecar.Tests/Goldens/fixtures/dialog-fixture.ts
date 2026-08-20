/**
 * Golden-recording dialog fixture (#139): loaded into the recorded pi via
 * `--extension`, it raises the three round-tripping dialog kinds from the
 * first agent_start — i.e. during a prompt turn, the way guard-style
 * extensions actually raise dialogs. The session_start variant is pinned
 * separately by session-start-dialog.ts: unanswerable (silent exit 0) under
 * pins before v0.84.2-psmfd.1, a completed round-trip from that pin on
 * (psmfd-patch-012, psmfd/pi#57). The driving scenario
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
