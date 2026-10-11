import { DecisionStore, DraftStore } from "./model.mjs";
import { startServer } from "./server.mjs";

export function createDecisionCanvas(store = new DecisionStore(), drafts = new DraftStore()) {
    const servers = new Map();
    return {
        id: "open-decisions",
        displayName: "Open decisions",
        description: "Live GitHub decision dialogue with focused questions and private, unsubmitted response drafts.",
        inputSchema: { type: "object", properties: {}, additionalProperties: false },
        actions: [{
            name: "refresh",
            description: "Refresh authoritative open GitHub issues; return counts, fetch errors and last successful refresh.",
            inputSchema: { type: "object", properties: {}, additionalProperties: false },
            handler: async () => {
                await store.refresh();
                const { cards, ...summary } = store.snapshot();
                return { ...summary, count: cards.length };
            },
        }],
        open: async (ctx) => {
            let pending = servers.get(ctx.instanceId);
            if (!pending) {
                pending = startServer(store, drafts);
                servers.set(ctx.instanceId, pending);
            }
            let entry;
            try { entry = await pending; }
            catch (error) { servers.delete(ctx.instanceId); throw error; }
            await store.refresh();
            return { title: "Open decisions", url: entry.url, status: store.error ? "Source fetch failed" : "Read-only GitHub source" };
        },
        onClose: async (ctx) => {
            const pending = servers.get(ctx.instanceId);
            if (pending) {
                servers.delete(ctx.instanceId);
                const entry = await pending;
                await new Promise((resolve) => entry.server.close(resolve));
            }
        },
    };
}
