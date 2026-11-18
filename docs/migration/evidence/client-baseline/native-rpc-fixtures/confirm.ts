// CF1-06 guest-only extension, based on the golden confirmation fixture.
import { appendFileSync } from "node:fs";

export default function confirmFixture(pi) {
  pi.on("before_agent_start", async (event, ctx) => {
    if (event.prompt !== "cf106 dialog") return;
    const receipt = "/Users/admin/.ft-cf106/dialog-events.jsonl";
    appendFileSync(receipt, JSON.stringify({state: "requested", epoch: Date.now() / 1000}) + "\n");
    const confirmed = await ctx.ui.confirm("CF106 confirmation", "Continue the local fixture turn?");
    appendFileSync(receipt, JSON.stringify({state: "answered", confirmed, epoch: Date.now() / 1000}) + "\n");
    ctx.ui.notify("CF106 confirmation received: " + confirmed, "info");
  });
}
