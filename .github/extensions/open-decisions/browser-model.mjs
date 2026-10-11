export function filterDecisions(cards, query = "", group = "All") {
    const needle = query.trim().toLowerCase();
    return cards.filter(card => (group === "All" || card.group === group)
        && `${card.issueNumber} ${card.issueTitle} ${card.heading} ${card.body}`.toLowerCase().includes(needle));
}
export function safeLink(value) {
    try {
        const url = new URL(value);
        return url.protocol === "https:" && url.hostname === "github.com" && !url.username && !url.password && !url.port ? url.href : null;
    } catch { return null; }
}
