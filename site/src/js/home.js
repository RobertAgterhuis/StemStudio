/*
 * StemStudio home page: the hero "split" (one waveform into six stems, canvas), the living mini
 * mixer (mute/solo, meters and faders on the beat), the GPU race and pausing of the story
 * visuals off screen. Colours come from the CSS tokens; with reduced motion every part shows its
 * end state and nothing loops. Needs site.js (window.StemSite) loaded first.
 */
(function () {
  "use strict";
  var site = window.StemSite || { reduced: true, syncBeat: function () {}, pauseOffscreen: function () {} };
  var reduced = site.reduced;
  var root = document.documentElement;
  var STEMS = ["vocals", "drums", "bass", "guitar", "piano", "other"];
  var BEAT_MS = parseFloat(getComputedStyle(root).getPropertyValue("--site-beat")) || 625;
  var silent = {};
  var listeners = [];

  function now() {
    return window.performance.now();
  }

  /* ---- Musical envelopes per stem, in beats (b), 0..1 ---- */
  function frac(x) {
    return x - Math.floor(x);
  }

  function hash(n) {
    var x = Math.sin(n * 127.1) * 43758.5453;
    return x - Math.floor(x);
  }

  var ENVELOPE = {
    vocals: function (b) {
      var phrase = Math.max(0, Math.sin((b * Math.PI) / 4));
      return 0.12 + 0.75 * phrase * (0.7 + 0.3 * Math.sin(b * 5.1));
    },
    drums: function (b) {
      var hit = Math.exp(-frac(b) * 7) * (frac(b / 4) < 0.25 ? 1 : 0.75);
      return 0.1 + 0.85 * Math.max(hit, 0.45 * Math.exp(-frac(b + 0.5) * 9));
    },
    bass: function (b) {
      return 0.25 + 0.6 * Math.exp(-frac(b / 2) * 1.8);
    },
    guitar: function (b) {
      return 0.15 + 0.7 * Math.exp(-frac(b + 0.5) * 3.5);
    },
    piano: function (b) {
      return 0.12 + 0.7 * Math.exp(-frac(b / 2) * 2.4);
    },
    other: function (b) {
      return 0.35 + 0.2 * Math.sin(b * 0.8);
    },
  };

  function beatsAt(time) {
    return time / BEAT_MS;
  }

  /* ---- Theme colours from the tokens ---- */
  var colours = {};

  function readColours() {
    var style = getComputedStyle(root);
    STEMS.forEach(function (stem) {
      colours[stem] = style.getPropertyValue("--ss-stem-" + stem).trim();
    });
    colours.strand = style.getPropertyValue("--site-strand").trim();
  }

  /* ---- The split ---- */
  function Split(figure) {
    var canvas = figure.querySelector("[data-split]");
    var ctx = canvas.getContext && canvas.getContext("2d");
    if (!ctx) return null;
    figure.classList.add("has-canvas");
    var width = 0;
    var height = 0;
    var ratio = 1;
    var start = now();
    var introMs = reduced ? 0 : 1800;

    function resize() {
      var rect = canvas.getBoundingClientRect();
      ratio = Math.min(window.devicePixelRatio || 1, rect.width < 700 ? 1.5 : 2);
      width = rect.width;
      height = rect.height;
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(height * ratio);
      ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    }

    function smooth(x) {
      var t = Math.min(1, Math.max(0, x));
      return t * t * (3 - 2 * t);
    }

    function draw(time) {
      var intro = introMs ? Math.min(1, (time - start) / introMs) : 1;
      var wipe = smooth(intro / 0.45);
      var spread = smooth((intro - 0.3) / 0.7);
      var step = width < 700 ? 4 : 3;
      var beatPx = Math.max(48, width / 14);
      var beats = reduced ? 6.3 : beatsAt(time);
      var cy = height / 2;
      var xs = width * (width < 700 ? 0.26 : 0.32);
      var blend = width * 0.2;
      var laneAmp = (height / 6) * 0.42;
      var mixAmp = height * 0.2;
      var limit = width * wipe;
      ctx.clearRect(0, 0, width, height);
      ctx.lineWidth = Math.max(1.5, step - 1.5);
      ctx.lineCap = "round";

      ctx.strokeStyle = colours.strand;
      ctx.beginPath();
      for (var x = 0; x < Math.min(xs, limit); x += step) {
        var b = (x - xs) / beatPx - beats;
        var sum = 0;
        STEMS.forEach(function (stem) {
          sum += silent[stem] ? 0 : ENVELOPE[stem](b);
        });
        var a = (sum / STEMS.length) * mixAmp * (0.55 + 0.45 * hash(Math.round(b * 16)));
        ctx.moveTo(x, cy - a - 0.5);
        ctx.lineTo(x, cy + a + 0.5);
      }
      ctx.stroke();

      STEMS.forEach(function (stem, i) {
        var laneY = (i + 0.5) * (height / 6);
        ctx.strokeStyle = colours[stem];
        ctx.globalAlpha = silent[stem] ? 0.18 : 0.95;
        ctx.beginPath();
        for (var x2 = xs; x2 < limit; x2 += step) {
          var k = smooth((x2 - xs) / blend) * spread;
          var y = cy + (laneY - cy) * k;
          var b2 = (x2 - xs) / beatPx - beats;
          var level = silent[stem] ? 0.08 : ENVELOPE[stem](b2);
          var a2 = level * laneAmp * (0.5 + 0.5 * hash(Math.round(b2 * 16) + i * 97)) * (0.35 + 0.65 * k);
          ctx.moveTo(x2, y - a2 - 0.5);
          ctx.lineTo(x2, y + a2 + 0.5);
        }
        ctx.stroke();
      });
      ctx.globalAlpha = 1;
      if (intro >= 0.75) figure.classList.add("is-split");
    }

    resize();
    window.addEventListener("resize", function () {
      resize();
      if (reduced) draw(now());
    });
    if (!reduced) figure.classList.add("is-armed");
    return { draw: draw, element: figure };
  }

  /* ---- The mini mixer ---- */
  function Mixer(desk, status) {
    var strips = Array.prototype.slice.call(desk.querySelectorAll(".ch"));
    var chips = {};
    document.querySelectorAll(".split-labels .chip").forEach(function (chip) {
      STEMS.forEach(function (stem) {
        if (chip.classList.contains("s-" + stem)) chips[stem] = chip;
      });
    });
    var levels = {};
    var travel = 0;

    function measure() {
      var fader = strips[0].querySelector(".ch-fader");
      travel = fader.clientHeight - strips[0].querySelector(".ch-cap").offsetHeight;
    }
    var words = { playing: desk.getAttribute("data-playing"), muted: desk.getAttribute("data-muted"), nothing: desk.getAttribute("data-nothing") };

    function state(strip, kind) {
      return strip.querySelector(kind === "mute" ? "[data-mute]" : "[data-solo]").getAttribute("aria-pressed") === "true";
    }

    function update() {
      var soloed = strips.filter(function (s) {
        return state(s, "solo");
      });
      var playing = [];
      var muted = [];
      strips.forEach(function (strip) {
        var stem = strip.getAttribute("data-stem");
        var isMuted = state(strip, "mute");
        var off = isMuted || (soloed.length > 0 && !state(strip, "solo"));
        silent[stem] = off;
        strip.classList.toggle("is-silent", off);
        if (chips[stem]) chips[stem].classList.toggle("is-silent", off);
        if (isMuted) muted.push(stem);
        if (!off) playing.push(stem);
      });
      var text = playing.length ? words.playing + " " + playing.join(", ") + "." : words.nothing;
      if (muted.length) text += " " + words.muted + " " + muted.join(", ") + ".";
      status.textContent = text;
      listeners.forEach(function (fn) {
        fn();
      });
    }

    desk.addEventListener("click", function (event) {
      var button = event.target.closest("button");
      if (!button) return;
      if (button.hasAttribute("data-mix-reset")) {
        desk.querySelectorAll("[aria-pressed]").forEach(function (b) {
          b.setAttribute("aria-pressed", "false");
        });
      } else {
        button.setAttribute("aria-pressed", button.getAttribute("aria-pressed") === "true" ? "false" : "true");
      }
      update();
    });

    function draw(time) {
      var beats = reduced ? 0.15 : beatsAt(time);
      strips.forEach(function (strip, i) {
        var stem = strip.getAttribute("data-stem");
        var target = silent[stem] ? 0 : ENVELOPE[stem](beats) * (0.85 + 0.15 * hash(Math.floor(beats * 4) + i * 13));
        var prev = levels[stem] || 0;
        var level = reduced ? target : Math.max(target, prev * 0.9);
        levels[stem] = level;
        strip.querySelector(".ch-level").style.transform = "scaleY(" + Math.min(1, level).toFixed(3) + ")";
        var drift = reduced ? 0 : Math.sin(beats / 8 + i * 1.3) * 0.12;
        strip.querySelector(".ch-cap").style.transform = "translateY(" + (travel * (0.22 + drift)).toFixed(1) + "px)";
      });
    }

    measure();
    window.addEventListener("resize", measure);
    update();
    return { draw: draw, element: desk };
  }

  /* ---- One render loop for the hero, running only while the hero is visible ---- */
  function startHero(parts) {
    var visible = true;
    var frame = 0;
    var last = 0;
    var small = window.matchMedia("(max-width: 40rem)").matches;

    function tick(time) {
      frame = 0;
      if (!visible) return;
      if (!small || time - last > 30) {
        last = time;
        parts.forEach(function (part) {
          part.draw(time);
        });
      }
      frame = window.requestAnimationFrame(tick);
    }

    function render() {
      parts.forEach(function (part) {
        part.draw(now());
      });
    }

    readColours();
    document.addEventListener("site:theme", function () {
      readColours();
      render();
    });
    listeners.push(function () {
      if (reduced) render();
    });
    if (reduced) {
      render();
      return;
    }
    if ("IntersectionObserver" in window) {
      new IntersectionObserver(function (entries) {
        visible = entries.some(function (e) {
          return e.isIntersecting;
        });
        if (visible && !frame) frame = window.requestAnimationFrame(tick);
      }).observe(document.querySelector(".hero"));
    }
    frame = window.requestAnimationFrame(tick);
  }

  /* ---- GPU race: both bars grow at the same speed; the GPU bar stops after 3.7 of 31.5 ---- */
  function setupRace(race) {
    var again = race.querySelector("[data-race-again]");
    var rows = Array.prototype.slice.call(race.querySelectorAll(".race-row"));
    var TOTAL = 6500;
    if (reduced || !race.animate) return;
    race.classList.add("is-armed");

    var running = [];

    function run() {
      again.hidden = true;
      running.forEach(function (animation) {
        animation.cancel();
      });
      running = [];
      var finished = 0;
      rows.forEach(function (row) {
        var fill = row.querySelector("[data-race-fill]");
        var share = parseFloat(fill.getAttribute("data-race-fill")) || 1;
        row.classList.remove("is-done");
        var animation = fill.animate([{ transform: "scaleX(0)" }, { transform: "scaleX(1)" }], { duration: TOTAL * share, easing: "linear", fill: "both" });
        running.push(animation);
        animation.onfinish = function () {
          row.classList.add("is-done");
          finished += 1;
          if (finished === rows.length) again.hidden = false;
        };
      });
    }

    rows.forEach(function (row) {
      running.push(row.querySelector("[data-race-fill]").animate([{ transform: "scaleX(0)" }, { transform: "scaleX(0)" }], { duration: 1, fill: "forwards" }));
    });
    again.addEventListener("click", run);
    if (!("IntersectionObserver" in window)) {
      run();
      return;
    }
    var observer = new IntersectionObserver(
      function (entries) {
        if (!entries[0].isIntersecting) return;
        observer.disconnect();
        run();
      },
      { threshold: 0.6 },
    );
    observer.observe(race);
  }

  var parts = [];
  var figure = document.querySelector(".split");
  if (figure) {
    var split = Split(figure);
    if (split) parts.push(split);
  }
  var desk = document.querySelector("[data-mixer]");
  if (desk) parts.push(Mixer(desk, document.querySelector("[data-mix-status]")));
  if (parts.length) startHero(parts);
  var race = document.querySelector("[data-race]");
  if (race) setupRace(race);
  site.pauseOffscreen(document.querySelectorAll(".story-visual, .flow"));
})();
