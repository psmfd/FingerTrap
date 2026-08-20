/**
 * Golden-recording fixture for the spawn-time-dialog trap (#139): awaits a
 * confirm inside session_start. In the pinned pi, RPC mode attaches its
 * stdin reader only AFTER session_start hooks complete, so this dialog can
 * never be answered — the await deadlocks and pi exits 0 once its event
 * loop drains. The golden pins that silent death; a supervisor must treat
 * a spawn-time extension_ui_request as fatal in this pin (upstream fix
 * tracked for the fork). Deliberately untyped, like dialog-fixture.ts.
 */
export default function sessionStartDialog(pi) {
	pi.on("session_start", async (_event, ctx) => {
		await ctx.ui.confirm("golden-spawn-confirm", "Answerable before the first command?");
	});
}
