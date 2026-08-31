package com.uiusimulator.auth.controller;

import com.uiusimulator.config.ClerkProperties;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.ResponseBody;

@Controller
public class AuthPageController {

    private final ClerkProperties clerkProperties;

    public AuthPageController(ClerkProperties clerkProperties) {
        this.clerkProperties = clerkProperties;
    }

    @GetMapping(value = "/auth/login", produces = MediaType.TEXT_HTML_VALUE)
    @ResponseBody
    public String loginPage() {
        String publishableKey = clerkProperties.publishableKey() == null
                ? ""
                : clerkProperties.publishableKey();

        // Note: %% escapes for String.formatted
        return """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1" />
                  <title>UIU Simulator — Sign in</title>
                  <script
                    async
                    crossorigin="anonymous"
                    data-clerk-publishable-key="%s"
                    src="https://cdn.jsdelivr.net/npm/@clerk/clerk-js@5/dist/clerk.browser.js"
                    type="text/javascript"
                  ></script>
                  <style>
                    :root {
                      --bg: #0f1a14;
                      --panel: #1a2b22;
                      --text: #e8f0ea;
                      --muted: #9bb5a5;
                      --accent: #3d8f6a;
                      --accent-hover: #4eaa7f;
                      --danger: #c45c5c;
                    }
                    * { box-sizing: border-box; }
                    body {
                      margin: 0;
                      min-height: 100vh;
                      font-family: "Segoe UI", system-ui, sans-serif;
                      background:
                        radial-gradient(ellipse at top, #1e3a2c 0%%, transparent 55%%),
                        var(--bg);
                      color: var(--text);
                      display: grid;
                      place-items: center;
                      padding: 1.5rem;
                    }
                    .card {
                      width: min(520px, 100%%);
                      background: var(--panel);
                      border: 1px solid #2a4034;
                      border-radius: 12px;
                      padding: 2rem;
                    }
                    h1 {
                      margin: 0 0 0.35rem;
                      font-size: 1.5rem;
                      font-weight: 650;
                      letter-spacing: -0.02em;
                    }
                    p {
                      margin: 0 0 1.25rem;
                      color: var(--muted);
                      line-height: 1.45;
                      font-size: 0.95rem;
                    }
                    .row {
                      display: flex;
                      flex-wrap: wrap;
                      gap: 0.75rem;
                      margin-bottom: 1rem;
                    }
                    button {
                      appearance: none;
                      border: 0;
                      border-radius: 8px;
                      padding: 0.7rem 1.1rem;
                      font-size: 0.95rem;
                      font-weight: 600;
                      cursor: pointer;
                      background: var(--accent);
                      color: #fff;
                    }
                    button:hover { background: var(--accent-hover); }
                    button.secondary {
                      background: transparent;
                      border: 1px solid #3a5646;
                      color: var(--text);
                    }
                    button.secondary:hover { border-color: var(--accent); }
                    #status {
                      margin-top: 0.75rem;
                      font-size: 0.85rem;
                      color: var(--muted);
                      min-height: 1.25rem;
                    }
                    #status.error { color: var(--danger); }
                    #status.ok { color: #7dcea0; }
                    #user-info, #dev-panel {
                      display: none;
                      margin-top: 0.75rem;
                      padding-top: 1rem;
                      border-top: 1px solid #2a4034;
                      font-size: 0.9rem;
                    }
                    textarea {
                      width: 100%%;
                      min-height: 88px;
                      margin: 0.5rem 0;
                      border-radius: 8px;
                      border: 1px solid #3a5646;
                      background: #122018;
                      color: var(--text);
                      padding: 0.6rem;
                      font-family: ui-monospace, monospace;
                      font-size: 0.75rem;
                    }
                    .hint { color: var(--muted); font-size: 0.8rem; line-height: 1.4; }
                  </style>
                </head>
                <body>
                  <main class="card">
                    <h1>UIU Simulator</h1>
                    <p>Sign in with Clerk. On Linux/Editor, Unity receives the token over HTTP — custom schemes like <code>uiusim://</code> are not registered with the OS.</p>
                    <div id="signed-out" class="row">
                      <button id="sign-in" type="button">Sign in</button>
                      <button id="sign-up" class="secondary" type="button">Sign up</button>
                    </div>
                    <div id="signed-in" style="display:none">
                      <div class="row">
                        <button id="send-unity" type="button">Send to Unity</button>
                        <button id="copy-token" class="secondary" type="button">Copy token</button>
                        <button id="try-deeplink" class="secondary" type="button">Try deep link</button>
                        <button id="sign-out" class="secondary" type="button">Sign out</button>
                      </div>
                      <div id="user-info"></div>
                      <div id="dev-panel">
                        <div class="hint">Development fallback — paste into Unity Editor “Apply JWT” if needed:</div>
                        <textarea id="token-box" readonly></textarea>
                      </div>
                    </div>
                    <div id="status">Loading Clerk…</div>
                  </main>
                  <script>
                    const DEEP_LINK = "uiusim://auth/callback";
                    const params = new URLSearchParams(window.location.search);
                    const sessionId = params.get("session");
                    const statusEl = document.getElementById("status");
                    const signedOut = document.getElementById("signed-out");
                    const signedIn = document.getElementById("signed-in");
                    const userInfo = document.getElementById("user-info");
                    const devPanel = document.getElementById("dev-panel");
                    const tokenBox = document.getElementById("token-box");
                    let cachedToken = "";

                    function setStatus(message, kind) {
                      statusEl.textContent = message;
                      statusEl.classList.toggle("error", kind === "error");
                      statusEl.classList.toggle("ok", kind === "ok");
                    }

                    async function fetchToken() {
                      if (!window.Clerk || !window.Clerk.session) {
                        throw new Error("No active Clerk session");
                      }
                      const token = await window.Clerk.session.getToken();
                      if (!token) {
                        throw new Error("Could not obtain session token");
                      }
                      cachedToken = token;
                      tokenBox.value = token;
                      devPanel.style.display = "block";
                      return token;
                    }

                    async function sendToUnityBridge() {
                      const token = await fetchToken();
                      if (!sessionId) {
                        setStatus("No Unity session id in URL. Use Copy token, or reopen Sign In from Unity.", "error");
                        return;
                      }
                      setStatus("Sending token to Unity…");
                      const response = await fetch("/auth/dev/bridge/" + encodeURIComponent(sessionId), {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ token })
                      });
                      if (!response.ok) {
                        setStatus("Failed to send token to Unity bridge.", "error");
                        return;
                      }
                      setStatus("Sent. Return to the Unity Editor — it will pick this up automatically.", "ok");
                    }

                    async function copyToken() {
                      const token = cachedToken || await fetchToken();
                      await navigator.clipboard.writeText(token);
                      setStatus("Token copied. Paste it in Unity Editor (Apply JWT).", "ok");
                    }

                    async function tryDeepLink() {
                      const token = cachedToken || await fetchToken();
                      const url = DEEP_LINK + "?token=" + encodeURIComponent(token);
                      setStatus("Attempting uiusim:// deep link (usually fails on Linux Editor)…");
                      window.location.href = url;
                    }

                    function renderAuthState() {
                      const user = window.Clerk.user;
                      if (user) {
                        signedOut.style.display = "none";
                        signedIn.style.display = "block";
                        userInfo.style.display = "block";
                        const email = user.primaryEmailAddress && user.primaryEmailAddress.emailAddress;
                        userInfo.textContent = "Signed in as " + (email || user.id);
                        setStatus(sessionId
                          ? "Signed in — sending to Unity…"
                          : "Signed in — copy token or open Unity with a session link.");
                      } else {
                        signedOut.style.display = "flex";
                        signedIn.style.display = "none";
                        userInfo.style.display = "none";
                        devPanel.style.display = "none";
                        setStatus("Choose Sign in or Sign up.");
                      }
                    }

                    window.addEventListener("load", async () => {
                      const clerk = window.Clerk;
                      if (!clerk) {
                        setStatus("Clerk failed to load. Check CLERK_PUBLISHABLE_KEY.", "error");
                        return;
                      }
                      try {
                        await clerk.load();
                        renderAuthState();

                        document.getElementById("sign-in").addEventListener("click", () => {
                          clerk.openSignIn({
                            afterSignInUrl: window.location.href,
                            afterSignUpUrl: window.location.href
                          });
                        });
                        document.getElementById("sign-up").addEventListener("click", () => {
                          clerk.openSignUp({
                            afterSignInUrl: window.location.href,
                            afterSignUpUrl: window.location.href
                          });
                        });
                        document.getElementById("sign-out").addEventListener("click", async () => {
                          await clerk.signOut();
                          cachedToken = "";
                          renderAuthState();
                        });
                        document.getElementById("send-unity").addEventListener("click", () => {
                          sendToUnityBridge().catch((err) => {
                            console.error(err);
                            setStatus(err.message || "Send failed", "error");
                          });
                        });
                        document.getElementById("copy-token").addEventListener("click", () => {
                          copyToken().catch((err) => {
                            console.error(err);
                            setStatus(err.message || "Copy failed", "error");
                          });
                        });
                        document.getElementById("try-deeplink").addEventListener("click", () => {
                          tryDeepLink().catch((err) => {
                            console.error(err);
                            setStatus(err.message || "Deep link failed", "error");
                          });
                        });

                        clerk.addListener(({ user }) => {
                          renderAuthState();
                          if (user && sessionId) {
                            setTimeout(() => {
                              sendToUnityBridge().catch((err) => {
                                console.error(err);
                                setStatus(err.message || "Send failed", "error");
                              });
                            }, 500);
                          } else if (user) {
                            fetchToken().catch(() => {});
                          }
                        });

                        if (clerk.user && sessionId) {
                          setTimeout(() => {
                            sendToUnityBridge().catch((err) => {
                              console.error(err);
                              setStatus(err.message || "Send failed", "error");
                            });
                          }, 500);
                        } else if (clerk.user) {
                          fetchToken().catch(() => {});
                        }
                      } catch (err) {
                        console.error(err);
                        setStatus("Clerk initialization failed.", "error");
                      }
                    });
                  </script>
                </body>
                </html>
                """.formatted(publishableKey);
    }
}
