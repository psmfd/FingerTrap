/**
 * Golden-recording fixture for spawn-time dialogs (#139): awaits a confirm
 * inside session_start. Under pins before v0.84.2-psmfd.1 this was the
 * silent-death trap — RPC mode attached its stdin reader only AFTER
 * session_start hooks completed, so the dialog could never be answered and
 * pi exited 0 (upstream psmfd/pi#57, fixed by psmfd-patch-012). From that
 * pin on, the reader attaches before extensions bind, the dialog is
 * answerable, and the notify below makes the completed round-trip
 * observable on the wire. Deliberately untyped, like dialog-fixture.ts.
 */
export default function sessionStartDialog(pi) {
	pi.on("session_start", async (_event, ctx) => {
		const ok = await ctx.ui.confirm("golden-spawn-confirm", "Answerable before the first command?");
		ctx.ui.notify(`golden-spawn-confirm-resolved:${ok}`, "info");
	});
}
