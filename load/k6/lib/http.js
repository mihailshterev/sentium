// Thin wrappers over k6/http: every request carries a `name` tag (so URLs with ids aggregate into one series) and a
// `type` tag the SLO thresholds select on (read, write, search, chat, stream, auth, setup).
import http from "k6/http";
import { check } from "k6";
import { BASE_URL } from "./config.js";

const JSON_HEADERS = { "Content-Type": "application/json", Accept: "application/json" };

function params(name, type, extra = {}) {
  const result = {
    headers: { ...JSON_HEADERS, ...(extra.headers || {}) },
    tags: { name, type, ...(extra.tags || {}) },
    timeout: extra.timeout || "60s",
  };
  // Only when given: an explicit undefined/null callback turns http_req_failed off for the request.
  if (extra.responseCallback) {
    result.responseCallback = extra.responseCallback;
  }
  return result;
}

export const url = (path) => `${BASE_URL}${path}`;

export function get(path, name, type = "read", extra) {
  return http.get(url(path), params(name, type, extra));
}

export function post(path, body, name, type = "write", extra) {
  return http.post(url(path), body === undefined ? null : JSON.stringify(body), params(name, type, extra));
}

export function put(path, body, name, type = "write", extra) {
  return http.put(url(path), JSON.stringify(body), params(name, type, extra));
}

export function del(path, name, type = "write", extra) {
  return http.del(url(path), null, params(name, type, extra));
}

/** Parallel GETs, the way the portal loads a page; `requests` is a list of [path, name]. */
export function getPage(requests, type = "read") {
  return http.batch(requests.map(([path, name]) => ["GET", url(path), null, params(name, type)]));
}

export function expectStatus(res, status, label = res.request && res.request.url) {
  const statuses = Array.isArray(status) ? status : [status];
  return check(res, { [`${label} -> ${statuses.join("/")}`]: (r) => statuses.includes(r.status) });
}

export function json(res, fallback = null) {
  try {
    return res.json();
  } catch {
    return fallback;
  }
}
