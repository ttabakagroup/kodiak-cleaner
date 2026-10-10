// Contact Us form: loads the spam check only when the form is set up, then posts to /api/contact.
(function () {
  var form = document.getElementById("contact-form");
  if (!form) return;
  var status = document.getElementById("form-status");
  var sendBtn = form.querySelector("button[type=submit]");
  var msg = form.querySelector("#message");
  var counter = document.getElementById("message-count");
  var widgetId = null;

  function say(text, kind) {
    status.textContent = text;
    status.className = "form-status show " + (kind === "ok" ? "ok" : "error");
    status.focus();
  }

  // Footer links can preselect a topic, for example /contact/?topic=Security
  var wanted = new URLSearchParams(location.search).get("topic");
  form.querySelectorAll("input[name=topic]").forEach(function (r) { if (r.value === wanted) r.checked = true; });

  msg.addEventListener("input", function () {
    counter.textContent = msg.value.length.toLocaleString() + " / 5,000";
  });

  function unavailable() {
    sendBtn.disabled = true;
    document.getElementById("turnstile").hidden = true;
    say("The form isn't switched on yet. Please try again later.", "error");
  }

  fetch("/api/contact", { headers: { accept: "application/json" } })
    .then(function (r) { return r.ok ? r.json() : null; })
    .then(function (cfg) {
      if (!cfg || !cfg.ready) return unavailable();
      window.kcTurnstileReady = function () {
        widgetId = window.turnstile.render("#turnstile", { sitekey: cfg.siteKey, theme: "auto" });
      };
      var s = document.createElement("script");
      s.src = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit&onload=kcTurnstileReady";
      s.async = true;
      document.head.appendChild(s);
    })
    .catch(unavailable);

  form.addEventListener("submit", function (e) {
    e.preventDefault();
    if (!form.reportValidity()) return;
    var data = Object.fromEntries(new FormData(form));
    if (!data["cf-turnstile-response"]) { say("Please finish the spam check above the Send Message button.", "error"); return; }
    sendBtn.disabled = true;
    sendBtn.textContent = "Sending…";
    fetch("/api/contact", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(data) })
      .then(function (r) {
        return r.json().catch(function () {
          // Not our reply: usually the form code wasn't uploaded, or a Cloudflare security rule stepped in.
          return { ok: false, error: "Your message couldn't be sent (the website replied with error " + r.status + "). Please try again in a few minutes." };
        });
      })
      .then(function (out) {
        if (out.ok) {
          form.reset();
          counter.textContent = "0 / 5,000";
          say("Thank you. Your message is on its way to Ted, and replies come to the email address you gave.", "ok");
        } else {
          say(out.error || "Your message couldn't be sent. Please try again in a few minutes.", "error");
        }
      })
      .catch(function () { say("Your message couldn't be sent. Please check your connection and try again.", "error"); })
      .finally(function () {
        sendBtn.disabled = false;
        sendBtn.textContent = "Send Message";
        if (window.turnstile && widgetId !== null) window.turnstile.reset(widgetId);
      });
  });
})();
