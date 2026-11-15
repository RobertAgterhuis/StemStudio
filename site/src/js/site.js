/*
 * StemStudio website: the theme switch and the copy buttons of the code blocks. The page works
 * without this script (dark theme, no copy buttons); both controls stay hidden until it runs.
 */
(function () {
  "use strict";
  var KEY = "stemstudio-site-theme";
  var root = document.documentElement;

  function current() {
    return root.getAttribute("data-theme") === "light" ? "light" : "dark";
  }

  function label(button) {
    var next = current() === "light" ? "dark" : "light";
    button.querySelector("[data-theme-label]").textContent = "Switch to " + next + " theme";
  }

  function setupTheme() {
    var button = document.querySelector("[data-theme-toggle]");
    if (!button) return;
    label(button);
    button.hidden = false;
    button.addEventListener("click", function () {
      var next = current() === "light" ? "dark" : "light";
      root.setAttribute("data-theme", next);
      try {
        window.localStorage.setItem(KEY, next);
      } catch (e) {
        /* storage blocked: the choice lasts for this visit only */
      }
      label(button);
    });
  }

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

  setupTheme();
  setupCopy();
})();
