"use strict";
const input = document.getElementById("site-search");
const results = document.getElementById("search-results");
const status = document.getElementById("search-status");
const entries = window.siteIndex;
input.addEventListener("input", () => {
  const words = input.value.trim().toLocaleLowerCase("en").split(/\s+/u).filter(Boolean);
  results.replaceChildren();
  if (!words.length) {
    status.textContent = "Search all chapters, or use the chapter navigation.";
    return;
  }
  const matches = entries.filter(entry => words.every(word =>
    `${entry.title} ${entry.text}`.toLocaleLowerCase("en").includes(word)));
  for (const entry of matches) {
    const item = document.createElement("li");
    const link = document.createElement("a");
    link.href = entry.url;
    link.textContent = entry.title;
    item.append(link);
    results.append(item);
  }
  status.textContent = matches.length
    ? `${matches.length} matching chapter${matches.length === 1 ? "" : "s"}. Tab to explore results.`
    : "No matching chapters. Try fewer words or use the chapter navigation.";
});
input.addEventListener("keydown", event => {
  if (event.key === "Escape") {
    input.value = "";
    input.dispatchEvent(new Event("input"));
  }
});
