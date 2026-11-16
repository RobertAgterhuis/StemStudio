/*
 * StemStudio website, every page: theme switch (cross-fade where View Transitions exist), copy
 * buttons, redirects for anchors that moved to the developer page, scroll reveals and the shared
 * beat. The pages work without this script (dark theme, no copy buttons, everything visible).
 */
(function () {
  "use strict";
  var KEY = "stemstudio-site-theme";
  var root = document.documentElement;
  var reduced = root.getAttribute("data-motion") === "reduced";

  /* ---- Old links such as index.html#status now live on developers.html ---- */
  function redirectMovedAnchor() {
    var moved = (root.getAttribute("data-moved") || "").split(" ").filter(Boolean);
    var id = window.location.hash.slice(1);
    if (id && moved.indexOf(id) >= 0 && !document.getElementById(id)) {
      window.location.replace("developers.html#" + id);
    }
  }

  /* ---- Theme ---- */
  function current() {
    return root.getAttribute("data-theme") === "light" ? "light" : "dark";
  }

  function label(button) {
    var target = button.querySelector("[data-theme-label]");
    var next = current() === "light" ? "dark" : "light";
    target.textContent = (button.getAttribute("data-label-" + next) || "Switch to " + next + " theme");
  }

  function applyTheme(next) {
    root.setAttribute("data-theme", next);
    try {
      window.localStorage.setItem(KEY, next);
    } catch (e) {
      /* storage blocked: the choice lasts for this visit only */
    }
    document.dispatchEvent(new CustomEvent("site:theme"));
  }

  function setupTheme() {
    var button = document.querySelector("[data-theme-toggle]");
    if (!button) return;
    label(button);
    button.hidden = false;
    button.addEventListener("click", function () {
      var next = current() === "light" ? "dark" : "light";
      if (document.startViewTransition && !reduced) {
        document.startViewTransition(function () {
          applyTheme(next);
        });
      } else {
        applyTheme(next);
      }
      label(button);
    });
  }

  /* ---- Copy buttons ---- */
  function announce(text) {
    var status = document.querySelector("[data-copy-status]");
    if (status) status.textContent = text;
  }

  function copy(button) {
    var code = button.parentElement.querySelector("code").textContent;
    navigator.clipboard.writeText(code).then(
      function () {
        button.textContent = "Copied";
        announce("Command copied to the clipboard.");
        window.setTimeout(function () {
          button.textContent = "Copy";
        }, 2000);
      },
      function () {
        announce("Copying failed; select the text and copy it yourself.");
      },
    );
  }

  function setupCopy() {
    if (!navigator.clipboard) return;
    document.querySelectorAll("[data-copy]").forEach(function (button) {
      button.hidden = false;
      button.setAttribute("aria-label", "Copy: " + button.parentElement.querySelector("code").textContent.split("\n")[0]);
      button.addEventListener("click", function () {
        copy(button);
      });
    });
  }

  /* ---- The beat: every looping CSS animation restarts on the same grid (document time 0) ---- */
  function syncBeat(scope) {
    if (!scope.getAnimations) return;
    scope.getAnimations({ subtree: true }).forEach(function (animation) {
      if (animation.timeline === document.timeline && animation.effect && animation.effect.getTiming().iterations === Infinity) {
        animation.startTime = 0;
      }
    });
  }

  /* Pauses the looping animations of an element while it is off screen. */
  function pauseOffscreen(elements) {
    if (!("IntersectionObserver" in window) || reduced) return;
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        entry.target.classList.toggle("is-paused", !entry.isIntersecting);
        if (entry.isIntersecting) syncBeat(entry.target);
      });
    });
    elements.forEach(function (element) {
      observer.observe(element);
    });
  }

  /* ---- Scroll reveals (once) ---- */
  function setupReveal() {
    var items = document.querySelectorAll(".reveal");
    if (reduced || !("IntersectionObserver" in window)) {
      items.forEach(function (item) {
        item.classList.add("is-in");
      });
      return;
    }
    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          entry.target.classList.add("is-in");
          observer.unobserve(entry.target);
        });
      },
      { rootMargin: "0px 0px -12% 0px", threshold: 0.12 },
    );
    items.forEach(function (item) {
      observer.observe(item);
    });
  }

  window.StemSite = { syncBeat: syncBeat, pauseOffscreen: pauseOffscreen, reduced: reduced };

  redirectMovedAnchor();
  setupTheme();
  setupCopy();
  setupReveal();
  pauseOffscreen(document.querySelectorAll("[data-beat]"));
  syncBeat(document.body);
})();
