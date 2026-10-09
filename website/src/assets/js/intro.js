// Homepage launch video: opens over the page on a visitor's first visit, and any time from
// Watch the Video. Plays muted (sound on request), and doesn't start by itself when the visitor
// prefers reduced motion. X, Esc, the backdrop and Continue to the Site close it; closing it is
// remembered so it doesn't open by itself again.
(function () {
  var box = document.getElementById("intro");
  if (!box || !box.showModal) return;
  var key = "kc-intro-seen";
  var id = box.getAttribute("data-intro-id");
  var video = box.querySelector("video");
  var sound = box.querySelector("[data-intro-sound]");
  var still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function play() {
    var p = video.play();
    if (p && p.catch) p.catch(function () {});
  }
  function close() { if (box.open) box.close(); }
  function showSound() { sound.hidden = !video.muted; }
  function open(fromButton) {
    if (box.open) return;
    video.preload = "auto";
    box.showModal();
    showSound();
    // A click on Watch the Video is a request to play, even with reduced motion.
    if (fromButton || !still) play();
  }

  box.querySelectorAll("[data-intro-close]").forEach(function (b) { b.addEventListener("click", close); });
  box.addEventListener("click", function (e) { if (e.target === box) close(); });
  box.addEventListener("close", function () {
    video.pause();
    try { localStorage.setItem(key, id); } catch (e) {}
  });
  sound.addEventListener("click", function () { video.muted = false; play(); video.focus(); });
  video.addEventListener("volumechange", showSound);
  video.addEventListener("ended", function () { box.querySelector(".btn.primary").focus(); });
  document.querySelectorAll("[data-intro-open]").forEach(function (b) {
    b.addEventListener("click", function () { video.currentTime = 0; open(true); });
  });

  var seen = false;
  try { seen = localStorage.getItem(key) === id; } catch (e) {}
  if (!seen) open(false);
})();
