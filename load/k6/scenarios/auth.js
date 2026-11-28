// Sign-in churn: members opening the portal on a new device, checking their session and signing out again.
import { LOAD_USER_PASSWORD } from "../lib/config.js";
import { expectStatus, get, post } from "../lib/http.js";
import { currentUser, forgetSession, login } from "../lib/session.js";
import { think } from "../lib/think.js";

export function auth(data) {
  forgetSession();

  if (!expectStatus(login(currentUser(data.userCount), LOAD_USER_PASSWORD), 200, "login")) {
    think(5, 10);
    return;
  }
  expectStatus(get("/api/identity/account/session", "GET /account/session", "auth"), 200, "session after login");

  think(1, 3);
  expectStatus(post("/api/identity/account/logout", undefined, "POST /account/logout", "auth"), [200, 204], "logout");
  forgetSession();

  think(5, 10);
}
