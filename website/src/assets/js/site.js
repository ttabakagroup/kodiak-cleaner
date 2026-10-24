// Theme button, mobile menu, theme tabs, screenshot lightbox and scroll reveal.
(function () {
  var root = document.documentElement;

  // Theme: Match Device -> Light -> Dark -> Match Device
  var themeBtn = document.querySelector("[data-theme-toggle]");
  var labels = { system: "Match Device", light: "Light", dark: "Dark" };
  function current() { return root.getAttribute("data-theme") || "system"; }
  function showTheme() {
    if (!themeBtn) return;
    var mode = current();
    themeBtn.setAttribute("aria-label", "Appearance: " + labels[mode] + ". Click to change.");
    themeBtn.title = "Appearance: " + labels[mode];
    themeBtn.querySelectorAll("svg").forEach(function (s) { s.hidden = s.dataset.mode !== mode; });
  }
  if (themeBtn) {
    themeBtn.addEventListener("click", function () {
      var next = { system: "light", light: "dark", dark: "system" }[current()];
      if (next === "system") root.removeAttribute("data-theme"); else root.setAttribute("data-theme", next);
      try { if (next === "system") localStorage.removeItem("kc-theme"); else localStorage.setItem("kc-theme", next); } catch (e) {}
      showTheme();
    });
    showTheme();
  }

  // Mobile menu
  var menuBtn = document.querySelector("[data-menu]");
  var nav = document.getElementById("site-nav");
  if (menuBtn && nav) {
    menuBtn.addEventListener("click", function () {
      var open = nav.classList.toggle("open");
      menuBtn.setAttribute("aria-expanded", open ? "true" : "false");
    });
    // Esc closes the open menu and puts focus back on the Menu button.
    document.addEventListener("keydown", function (e) {
      if (e.key !== "Escape" || !nav.classList.contains("open")) return;
      nav.classList.remove("open");
      menuBtn.setAttribute("aria-expanded", "false");
      menuBtn.focus();
    });
  }

  // Tabs
  document.querySelectorAll("[role=tablist]").forEach(function (list) {
    var tabs = Array.prototype.slice.call(list.querySelectorAll("[role=tab]"));
    function select(tab) {
      tabs.forEach(function (t) {
        var on = t === tab;
        t.setAttribute("aria-selected", on ? "true" : "false");
        t.tabIndex = on ? 0 : -1;
        document.getElementById(t.getAttribute("aria-controls")).hidden = !on;
      });
    }
    tabs.forEach(function (t, i) {
      t.addEventListener("click", function () { select(t); });
      t.addEventListener("keydown", function (e) {
        var j = e.key === "ArrowRight" ? i + 1 : e.key === "ArrowLeft" ? i - 1 : e.key === "Home" ? 0 : e.key === "End" ? tabs.length - 1 : null;
        if (j === null) return;
        var next = tabs[(j + tabs.length) % tabs.length];
        select(next); next.focus(); e.preventDefault();
      });
    });
  });

  // Lightbox for screenshots
  var box = document.getElementById("lightbox");
  if (box && box.showModal) {
    var img = box.querySelector("img"), cap = box.querySelector("p");
    document.querySelectorAll("button.zoom").forEach(function (b) {
      b.addEventListener("click", function () {
        var src = b.querySelector("img");
        img.src = src.currentSrc || src.src;
        img.alt = src.alt;
        cap.textContent = src.alt;
        box.showModal();
      });
    });
    box.addEventListener("click", function (e) { if (e.target === box || e.target.closest(".close")) box.close(); });
  } else {
    document.querySelectorAll("button.zoom").forEach(function (b) { b.style.cursor = "default"; });
  }

  // Reveal on scroll
  var items = document.querySelectorAll(".reveal");
  if ("IntersectionObserver" in window) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) { if (en.isIntersecting) { en.target.classList.add("seen"); io.unobserve(en.target); } });
    }, { rootMargin: "0px 0px -8% 0px" });
    items.forEach(function (el) { io.observe(el); });
  } else {
    items.forEach(function (el) { el.classList.add("seen"); });
  }
})();

// Thank-you pop-up after a download starts. The download itself goes ahead untouched;
// the pop-up only appears over the page and closes with X, Esc, Maybe Later or a click outside.
(function () {
  var box = document.getElementById("download-thanks");
  if (!box || !box.showModal) return;
  var isDownload = /^\/download\/[^/]+\.(msi|zip|dmg)$|\/releases\/download\/[^/]+\/[^/]+\.(msi|zip|dmg|deb|AppImage)$/i;
  document.addEventListener("click", function (e) {
    var a = e.target.closest && e.target.closest("a[href]");
    if (!a || box.contains(a) || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    var url;
    try { url = new URL(a.href, location.href); } catch (err) { return; }
    var path = url.origin === location.origin ? url.pathname : url.href;
    if (!isDownload.test(path)) return;
    setTimeout(function () { if (!box.open) box.showModal(); }, 0);
  });
  box.querySelectorAll("[data-thanks-close]").forEach(function (b) { b.addEventListener("click", function () { box.close(); }); });
  box.addEventListener("click", function (e) { if (e.target === box) box.close(); });
})();
