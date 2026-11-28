// One signed-in household member per VU. The cookie jar survives iterations (noCookiesReset in main.js), so a VU
// signs in once and then behaves like a browser tab that stays logged in.
import http from "k6/http";
import exec from "k6/execution";
import { fail } from "k6";
import { BASE_URL, LOAD_USER_PASSWORD, LOAD_USER_PREFIX } from "./config.js";
import { expectStatus, post } from "./http.js";

let signedInAs = null;

export function loadUserName(index) {
  return `${LOAD_USER_PREFIX}-${String(index + 1).padStart(4, "0")}`;
}

/** The load user a VU acts as: VU ids are unique across scenarios, so concurrent VUs never share a user. */
export function currentUser(userCount) {
  return loadUserName((exec.vu.idInTest - 1) % userCount);
}

export function login(userName, password, type = "auth") {
  return post("/api/identity/account/login", { userName, password }, "POST /account/login", type);
}

export function ensureSignedIn(userCount) {
  const userName = currentUser(userCount);
  if (signedInAs === userName) {
    return userName;
  }

  const res = login(userName, LOAD_USER_PASSWORD);
  if (!expectStatus(res, 200, "sign in")) {
    fail(`VU ${exec.vu.idInTest} could not sign in as ${userName}: ${res.status} ${res.body}`);
  }
  signedInAs = userName;
  return userName;
}

/** Forgets the session (after a logout), so the next ensureSignedIn signs in again. */
export function forgetSession() {
  signedInAs = null;
  http.cookieJar().clear(BASE_URL);
}
