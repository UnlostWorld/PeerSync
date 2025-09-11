
## Character Fingerprints

**What:** Character Fingerprints are unique id's generated from a character name, world, and a password.

**Scope:** Sent to _servers_ and _paired clients_ when establishing a connection.

**Risk:** As Character Fingerprints are used to identify characters, the primary risk factor is bad actors obtaining a Character Fingerprint and reversing it into a character name that could be used for harassment.

**Mitigation:** They are cryptographically hashed with a password to prevent reversing, and are only stored on the server while the user is online.


## IP Addresses

**What:** IP Addresses are an internet-facing address that allows clients to connect directly to each other.

**Scope:**
- Local (LAN) address is sent to _servers_.
- Internet (WAN) address is captured by _servers_.
- Local (LAN) and Internet (WAN) address is obtained by clients who already have the corresponding _Character Fingerprint_.

**Risk:** IP Addresses could be used by bad actors to:
- Identify the approximate physical location (city) of a client and their internet provider.
- DDOS attack a client's internet connection directly.

**Mitigation:** IP addresses of residential users are typically dynamic, and can be changed by simply rebooting the modem.