/*
 * StemStudio website: picks the theme before the first paint (loaded synchronously in <head>).
 * A stored choice wins, otherwise the system preference. tokens.css (from the app) keys its
 * colours on html[data-theme] and its motion durations on html[data-motion].
 */
(function () {
  "use strict";
  var root = document.documentElement;
  var stored = null;
  try {
    stored = window.localStorage.getItem("stemstudio-site-theme");
  } catch (e) {
    stored = null;
  }
  var prefersLight = window.matchMedia && window.matchMedia("(prefers-color-scheme: light)").matches;
  var reduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  root.setAttribute("data-theme", stored === "light" || stored === "dark" ? stored : prefersLight ? "light" : "dark");
  if (reduced) root.setAttribute("data-motion", "reduced");
})();
