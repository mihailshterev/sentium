// Applies the stored theme before first paint, so the page never flashes the wrong one. A plain script file
// rather than inline, because the Content-Security-Policy allows scripts from the portal's own origin only.
(function () {
  try {
    var stored = JSON.parse(localStorage.getItem("sentium-theme") || "null");

    var pref = (stored && stored.state && stored.state.preference) || "system";

    var dark = pref === "dark" || (pref === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);

    document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
  } catch (e) {
    document.documentElement.setAttribute("data-theme", "dark");
  }
})();
