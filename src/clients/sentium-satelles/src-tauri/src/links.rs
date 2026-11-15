//! What the person types as a server address, and what clicking a toast may open.

use std::net::IpAddr;

use url::Url;

pub const INBOX_PATH: &str = "/inbox";

/// Turns typed input into the server origin. HTTPS is required except on this PC itself, where a development server
/// may run plain HTTP.
pub fn parse_server(input: &str) -> Result<Url, String> {
    let text = input.trim();
    if text.is_empty() {
        return Err("Enter the address of your Sentium server.".into());
    }

    let text = if text.contains("://") { text.to_owned() } else { format!("https://{text}") };
    let parsed = Url::parse(&text).map_err(|_| "That is not a valid address.".to_string())?;
    let host = parsed.host_str().ok_or_else(|| "That is not a valid address.".to_string())?;

    let is_https = parsed.scheme() == "https";
    let is_local_http = parsed.scheme() == "http" && is_loopback(host);
    if !is_https && !is_local_http {
        return Err("The address must start with https://.".into());
    }

    Url::parse(&format!("{}/", parsed.origin().ascii_serialization())).map_err(|_| "That is not a valid address.".into())
}

/// Only a path on the paired server is followed; anything else (another host or scheme, a protocol-relative or
/// malformed link) opens the Inbox, so a Notification can never make the PC open an arbitrary URL or program.
pub fn resolve_notification_link(server: &Url, link: Option<&str>) -> Url {
    let inbox = server.join(INBOX_PATH).expect("the inbox path is static");

    let Some(link) = link.map(str::trim).filter(|l| !l.is_empty()) else {
        return inbox;
    };

    let suspicious = !link.starts_with('/')
        || link.starts_with("//")
        || link.contains('\\')
        || link.chars().any(char::is_control);
    if suspicious {
        return inbox;
    }

    match server.join(link) {
        Ok(target) if target.origin() == server.origin() => target,
        _ => inbox,
    }
}

fn is_loopback(host: &str) -> bool {
    host.eq_ignore_ascii_case("localhost")
        || host.trim_matches(['[', ']']).parse::<IpAddr>().is_ok_and(|ip| ip.is_loopback())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn server() -> Url {
        Url::parse("https://sentium.home.arpa/").unwrap()
    }

    #[test]
    fn follows_paths_on_the_paired_server() {
        assert_eq!(resolve_notification_link(&server(), Some("/vigil")).as_str(), "https://sentium.home.arpa/vigil");
        assert_eq!(
            resolve_notification_link(&server(), Some("/orchestration/runs/42?tab=log")).as_str(),
            "https://sentium.home.arpa/orchestration/runs/42?tab=log"
        );
    }

    #[test]
    fn opens_the_inbox_for_anything_else() {
        let inbox = "https://sentium.home.arpa/inbox";
        for link in [
            None,
            Some(""),
            Some("   "),
            Some("https://evil.example/phish"),
            Some("//evil.example/phish"),
            Some("/\\evil.example"),
            Some("javascript:alert(1)"),
            Some("file:///C:/Windows/System32/calc.exe"),
            Some("vigil"),
            Some("/vigil\nSet-Cookie: x"),
        ] {
            assert_eq!(resolve_notification_link(&server(), link).as_str(), inbox, "link {link:?}");
        }
    }

    #[test]
    fn keeps_the_port_of_the_server() {
        let server = Url::parse("https://localhost:7001/").unwrap();
        assert_eq!(resolve_notification_link(&server, Some("/inbox")).as_str(), "https://localhost:7001/inbox");
    }

    #[test]
    fn server_address_defaults_to_https_and_keeps_only_the_origin() {
        assert_eq!(parse_server("sentium.home.arpa").unwrap().as_str(), "https://sentium.home.arpa/");
        assert_eq!(parse_server(" https://sentium.home.arpa/vigil?x=1 ").unwrap().as_str(), "https://sentium.home.arpa/");
        assert_eq!(parse_server("https://sentium.home.arpa:8443").unwrap().as_str(), "https://sentium.home.arpa:8443/");
    }

    #[test]
    fn plain_http_is_allowed_only_on_this_pc() {
        assert!(parse_server("http://localhost:5000").is_ok());
        assert!(parse_server("http://127.0.0.1:5000").is_ok());
        assert!(parse_server("http://[::1]:5000").is_ok());
        assert!(parse_server("http://sentium.home.arpa").is_err());
        assert!(parse_server("ftp://sentium.home.arpa").is_err());
        assert!(parse_server("").is_err());
    }
}
