// Homepage launch video: shown over the page once per visitor. Plays muted (sound on request),
// and doesn't start by itself when the visitor prefers reduced motion. X, Esc, the backdrop and
// Continue to the Site all close it, and closing it is remembered.
(function () {
  var box = document.getElementById("intro");
  if (!box || !box.showModal) return;
  var key = "kc-intro-seen";
  var id = box.getAttribute("data-intro-id");
  try { if (localStorage.getItem(key) === id) return; } catch (e) {}

  var video = box.querySelector("video");
  var sound = box.querySelector("[data-intro-sound]");
  var still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function close() { if (box.open) box.close(); }
  function showSound() { sound.hidden = !video.muted; }

  box.querySelectorAll("[data-intro-close]").forEach(function (b) { b.addEventListener("click", close); });
  box.addEventListener("click", function (e) { if (e.target === box) close(); });
  box.addEventListener("close", function () {
    video.pause();
    try { localStorage.setItem(key, id); } catch (e) {}
  });
  sound.addEventListener("click", function () {
    video.muted = false;
    var p = video.play();
    if (p && p.catch) p.catch(function () {});
    video.focus();
  });
  video.addEventListener("volumechange", showSound);
  video.addEventListener("ended", function () { box.querySelector(".btn.primary").focus(); });

  video.preload = "auto";
  box.showModal();
  showSound();
  if (!still) {
    var p = video.play();
    if (p && p.catch) p.catch(function () {});
  }
})();
