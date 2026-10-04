// Shows bim-open-data's NOTICE.md for the sample buildings, copied to data/ by the
// tests that write data/buildings.json, so the page carries the notice unchanged.
const target = document.getElementById("notice-text");
try {
  const response = await fetch("data/NOTICE.md");
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  target.textContent = await response.text();
} catch (err) {
  target.textContent = `data/NOTICE.md did not load (${err.message}). The notice is in bim-open-data: samples/public/NOTICE.md.`;
}
