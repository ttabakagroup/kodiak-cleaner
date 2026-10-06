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
        var j = e.key === "ArrowRight" ? i + 1 : e.key === "ArrowLeft" ? i - 1 : null;
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
