import { joinSession, createCanvas } from "@github/copilot-sdk/extension";
import { createDecisionCanvas } from "./lifecycle.mjs";

await joinSession({
    canvases: [
        createCanvas(createDecisionCanvas()),
    ],
});
