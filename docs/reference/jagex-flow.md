# Jagex Account OAuth Flow — Reference Constants

Transcribed from two public reference Rust launchers and cross-checked against the RuneLite
client source. All literal values are quoted exactly as they appear in source. Items that
could not be confirmed from static analysis are marked
**UNVERIFIED — confirm in Task 8 (live login)**.

Sources used:
- **[melxin]** `https://github.com/melxin/native-linux-jagex-launcher` (cloned at `/tmp/ref-melxin`, depth 1)
- **[aitoiaita]** `https://github.com/aitoiaita/linux-jagex-launcher` (cloned at `/tmp/ref-aitoiaita`, depth 1)
- **[runelite]** `https://github.com/runelite/runelite` (fetched individual source files via raw.githubusercontent.com)

---

## 1. Authorization

### Authorize endpoint

```
https://account.jagex.com/oauth2/auth
```

Source: [melxin] `src/daemon/launcher_client.rs` line 13 (`LAUNCHER_AUTH_URL`);
[aitoiaita] same file same line. **Both repos agree.**

### Token endpoint

```
https://account.jagex.com/oauth2/token
```

Source: [melxin] `src/daemon/launcher_client.rs` line 14 (`LAUNCHER_TOKEN_URL`);
[aitoiaita] same file same line. **Both repos agree.**

### `client_id`

#### Launcher (Step 1 — initial auth code request)

```
com_jagex_auth_desktop_launcher
```

Source: [melxin] `src/daemon/launcher_client.rs` line 11 (`LAUNCHER_CLIENT_ID`);
[aitoiaita] same file same line. **Both repos agree.**

#### Consent client (Step 2 — after identifying as a Jagex account)

```
1fddee4e-b100-4f4e-b2b0-097f9088f9d2
```

Source: [melxin] `src/daemon/consent_client.rs` line 12 (`CONSENT_CLIENT_ID`);
[aitoiaita] same file same line. **Both repos agree.**

### Scope

```
openid offline gamesso.token.create user.profile.read
```

(space-delimited; added as four separate scopes in the oauth2 crate)

Source: [melxin] `src/daemon/launcher_client.rs` lines 215–218 (`register_auth_url`);
[aitoiaita] same file lines 314–317. **Both repos agree.**

### Redirect URI

[melxin] does **not** set a `redirect_uri` explicitly — the oauth2 library omits it.

[aitoiaita] **adds**:

```
https://secure.runescape.com/m=weblogin/launcher-redirect
```

Source: [aitoiaita] `src/daemon/launcher_client.rs` line 26 (`LAUNCHER_REDIRECT_URI`),
used at lines 293–295 and 318–320.

> **Discrepancy:** melxin omits the redirect_uri; aitoiaita sets it to
> `https://secure.runescape.com/m=weblogin/launcher-redirect`. The aitoiaita version
> also uses PKCE (see below), which is the more complete implementation.
> **UNVERIFIED — confirm in Task 8 (live login)** which the Jagex server actually requires.

### PKCE

[melxin] does **not** use PKCE.

[aitoiaita] **uses PKCE with SHA-256**:
- Challenge method: `S256`
- Verifier length: 64 bytes (`PkceCodeChallenge::new_random_sha256_len(64)`)
- The verifier is stored on the `LauncherClient` struct and submitted with the token exchange.

Source: [aitoiaita] `src/daemon/launcher_client.rs` lines 6–7 (imports `PkceCodeChallenge`,
`PkceCodeVerifier`), line 150 (struct field), lines 285–296 (`authorize` method),
line 306 (`register_auth_url`).

> **Discrepancy:** melxin skips PKCE entirely; aitoiaita uses `S256` with a 64-byte
> verifier. aitoiaita appears to be the newer/more correct implementation.
> The existing repo's `AuthorizeUrlBuilder.cs` hard-codes `"code_challenge_method" = "S256"`,
> which aligns with aitoiaita.

### Response type

For the initial launcher auth: `code` (standard authorization code flow).

For the consent client (Jagex-account second step): `id_token code`
(implicit-hybrid; the consent client uses `use_implicit_flow()` with
`set_response_type("id_token code")`).

Source: [melxin] `src/daemon/consent_client.rs` lines 89–91;
[aitoiaita] same file lines 107–112. **Both repos agree.**

---

## 2. Token Exchange

### Request

`POST https://account.jagex.com/oauth2/token`  
Content-Type: `application/x-www-form-urlencoded`

Fields:
| Field | Value |
|---|---|
| `grant_type` | `authorization_code` |
| `code` | authorization code from redirect |
| `client_id` | `com_jagex_auth_desktop_launcher` |
| `redirect_uri` | `https://secure.runescape.com/m=weblogin/launcher-redirect` (aitoiaita only; UNVERIFIED) |
| `code_verifier` | PKCE verifier (aitoiaita only; UNVERIFIED) |

The existing repo's `OAuthClient.cs` (`ExchangeCodeAsync`) sends exactly these fields
including `redirect_uri` and `code_verifier`, consistent with the aitoiaita implementation.

### Token Response JSON shape

```json
{
  "access_token": "<string>",
  "token_type": "<string>",
  "expires_in": <number>,
  "refresh_token": "<string>",
  "id_token": "<JWT string>",
  "scope": "<space-delimited string>"
}
```

Both `refresh_token` and `id_token` are declared `Option<...>` (may be absent) but
the reference launchers treat both as required — a missing `id_token` or `refresh_token`
is a fatal error.

Source: [melxin] `src/daemon/jagex_oauth.rs` struct `TokenResponseWithJWT` (lines 153–180),
error variants `TokenResponseMissingIDToken` / `TokenResponseMissingRefreshToken` /
`TokenResponseMissingExpiration` in `launcher_client.rs`;
[aitoiaita] identical struct. **Both repos agree.**

> **Alignment with existing repo:** `OAuthTokens.cs` declares
> `AccessToken`, `RefreshToken?`, `IdToken?`, `ExpiresIn` — matches exactly.
> There is no `token_type` or `scope` field in the C# record; those are consumed
> by the Rust oauth2 library internally and are not needed at the application layer.

### Refresh grant

`POST https://account.jagex.com/oauth2/token`  
Fields:
| Field | Value |
|---|---|
| `grant_type` | `refresh_token` |
| `refresh_token` | stored refresh token |
| `client_id` | `com_jagex_auth_desktop_launcher` |

Source: [melxin] `src/daemon/launcher_client.rs` `refreshed()` method (line 144);
[aitoiaita] same. The existing repo's `OAuthClient.cs` `RefreshAsync` matches.

### `id_token` JWT claims of interest

The Rust launchers parse the `id_token` JWT and inspect:

```
claims.extra["login_provider"]
```

If this claim equals `"runescape"`, the account is a legacy RS account (uses
`JX_ACCESS_TOKEN` / `JX_REFRESH_TOKEN` path); otherwise it is a Jagex account
(uses `JX_SESSION_ID` / `JX_CHARACTER_ID` path).

Source: [melxin] `src/daemon/launcher_client.rs` lines 124–129;
[aitoiaita] same lines 174–179.

---

## 3. Game Session

### Session creation endpoint

```
POST https://auth.jagex.com/game-session/v1/sessions
```

Source: [melxin] `src/daemon/game_session.rs` line 9 (`GAMESESSION_SESSION_ENDPOINT`);
[aitoiaita] same. **Both repos agree.**

### Session request body

```json
{
  "idToken": "<id_token JWT string>"
}
```

Source: [melxin] `src/daemon/game_session.rs` struct `GameSessionRequest` (lines 99–103,
`serde(rename = "idToken")`);
[aitoiaita] same.

### Session response JSON shape

```json
{
  "sessionId": "<string>"
}
```

Source: [melxin] `src/daemon/game_session.rs` struct `GameSessionID` (lines 87–92,
`serde(rename = "sessionId")`);
[aitoiaita] same.

> **Alignment with existing repo:** The task spec assumed `{sessionId}`.
> Both reference implementations confirm `sessionId` (camelCase). **Match.**

---

## 4. Character / Accounts List

### Accounts endpoint

```
GET https://auth.jagex.com/game-session/v1/accounts
```

Source: [melxin] `src/daemon/game_session.rs` line 8 (`GAMESESSION_ACCOUNTS_ENDPOINT`);
[aitoiaita] same. **Both repos agree.**

### Authentication

Bearer token using the **session ID** (not the OAuth access token):

```
Authorization: Bearer <sessionId value>
```

Source: [melxin] `src/daemon/game_session.rs` lines 155–156;
[aitoiaita] lines 178–181. **Both repos agree.**

### Response JSON shape

The response is a JSON array. Each element has:

| JSON field | Type | melxin | aitoiaita |
|---|---|---|---|
| `accountId` | string (required) | present | present |
| `displayName` | string | required | **optional** (`default`, may be absent) |
| `userHash` | string | absent | **present** (required) |

[melxin] `src/daemon/game_session.rs` lines 108–114:
```rust
pub struct GameSessionAccount {
    #[serde(rename = "accountId")]
    account_id: AccountID,
    #[serde(rename = "displayName")]
    display_name: DisplayName
}
```

[aitoiaita] `src/daemon/game_session.rs` lines 113–121:
```rust
pub struct GameSessionAccount {
    #[serde(rename = "accountId")]
    account_id: AccountID,
    #[serde(rename = "displayName", default)]
    display_name: Option<DisplayName>,
    #[serde(rename = "userHash")]
    user_hash: String,
}
```

> **Discrepancy:** aitoiaita treats `displayName` as optional and adds a `userHash`
> field. The aitoiaita version is the later, more complete implementation and should
> be preferred. **UNVERIFIED — confirm exact shape in Task 8 (live login).**

> **Alignment with existing repo:** The task spec assumed `[{accountId, displayName}]`.
> Both repos use `accountId` and `displayName` (camelCase). The `accountId` field name
> matches the assumed shape. `displayName` matches. `userHash` is an extra field not
> in the assumed shape — the C# deserializer should ignore unknown fields by default.

### RS profile endpoint (legacy Runescape accounts only)

```
GET https://secure.jagex.com/rs-profile/v1/profile
Authorization: Bearer <id_token JWT string>
```

Response:
```json
{
  "display_name_set": <bool>,
  "display_name": "<string or null>"
}
```

Source: [melxin] `src/daemon/game_session.rs` lines 8, 11, 64–82;
[aitoiaita] same. **Both repos agree.**

---

## 5. JX_ Environment Variable Contract

The reference launchers call `runelite` as a child process and pass credentials via
environment variables. RuneLite's `TelemetryClient.java` confirms it reads these
variables directly from the process environment.

### For Jagex accounts (Jagex-account login path)

| Variable | Value passed | Source |
|---|---|---|
| `JX_SESSION_ID` | session ID string from `sessionId` response field | [melxin/aitoiaita] `src/daemon/daemon.rs` `run_runelite_with_jagex_account` |
| `JX_CHARACTER_ID` | account ID string from `accountId` accounts array field | same |
| `JX_DISPLAY_NAME` | display name string from `displayName` accounts array field | same |

### For legacy RuneScape accounts (RS login path)

| Variable | Value passed | Source |
|---|---|---|
| `JX_ACCESS_TOKEN` | OAuth access token | [melxin/aitoiaita] `src/daemon/daemon.rs` `run_runelite_with_rs_account` |
| `JX_REFRESH_TOKEN` | OAuth refresh token | same |
| `JX_DISPLAY_NAME` | RS display name (from profile endpoint, may be empty string) | same |

### RuneLite confirmation

`TelemetryClient.java` line 176:
```java
telemetry.setJxAccount(System.getenv("JX_SESSION_ID") != null && System.getenv("JX_CHARACTER_ID") != null);
```
This independently confirms `JX_SESSION_ID` and `JX_CHARACTER_ID` are the env vars
RuneLite checks to detect a Jagex account login.

Source: [runelite] `runelite-client/src/main/java/net/runelite/client/TelemetryClient.java`

> **Alignment with existing repo:** The assumed contract listed in the task spec was
> `JX_SESSION_ID`, `JX_CHARACTER_ID`, `JX_DISPLAY_NAME`, `JX_ACCESS_TOKEN`,
> `JX_REFRESH_TOKEN`. **All five names are confirmed exactly.** No additional JX_
> variables were found in either reference repo.

> **✅ VERIFIED LIVE (2026-05-24):** Setting EXACTLY these three vars — `JX_SESSION_ID`,
> `JX_CHARACTER_ID`, `JX_DISPLAY_NAME` — logs into the OSRS game world successfully.
> **Critical gotcha:** ALSO setting `JX_ACCESS_TOKEN`/`JX_REFRESH_TOKEN` (the legacy-RS
> path vars) alongside the session makes the client reject the login with the generic
> *"Failed to login. Please try again."* — RuneLite takes the wrong login branch. For a
> Jagex account, emit ONLY the three session vars.

---

## 6. Summary of UNVERIFIED Items

**✅ All resolved by a successful live login on 2026-05-24 (Task 8):**
- **redirect_uri** `https://secure.runescape.com/m=weblogin/launcher-redirect` is **required** and works (the leg-1 `code` is captured at it).
- **PKCE (S256)** is **used and accepted** by the server.
- **displayName** is **optional** (one account returned none; default to empty string).
- **userHash** is not needed (ignored harmlessly by the C# deserializer).
- **Consent leg** (`http://localhost`, `response_type=id_token code`, scope `openid offline`, no `id_token_hint`) works; the `id_token` arrives in the URL **fragment** and is used directly to create the game session.

The original (now-resolved) notes follow for reference:

1. **redirect_uri** — Whether `https://secure.runescape.com/m=weblogin/launcher-redirect`
   is required by the Jagex authorization server, or whether it can be omitted.
   melxin omits it; aitoiaita requires it.

2. **PKCE requirement** — Whether `code_challenge` / `code_verifier` (S256) is
   enforced by the server. melxin skips it; aitoiaita uses it.

3. **`displayName` optionality in accounts array** — aitoiaita treats it as
   `Option<String>` (may be absent); melxin treats it as required. The live
   response will confirm.

4. **`userHash` field** — Present in aitoiaita's `GameSessionAccount` struct but
   absent in melxin's. Whether the API always returns it, and whether it is needed
   by the launcher, is unconfirmed.

5. **Consent client redirect_uri** — Both repos set:
   `http://localhost` (plain HTTP, not HTTPS).
   Source: [melxin/aitoiaita] `src/daemon/consent_client.rs` `register_auth_url`.
   The consent step uses `response_type=id_token code` with implicit flow to
   `http://localhost`. **UNVERIFIED** whether the port matters.

---

## 7. Repo Agreement Summary

| Constant | melxin | aitoiaita | Agree? |
|---|---|---|---|
| Auth URL | `https://account.jagex.com/oauth2/auth` | same | Yes |
| Token URL | `https://account.jagex.com/oauth2/token` | same | Yes |
| Launcher client_id | `com_jagex_auth_desktop_launcher` | same | Yes |
| Consent client_id | `1fddee4e-b100-4f4e-b2b0-097f9088f9d2` | same | Yes |
| Scope string | `openid offline gamesso.token.create user.profile.read` | same | Yes |
| redirect_uri | (omitted) | `https://secure.runescape.com/m=weblogin/launcher-redirect` | **No** |
| PKCE | Not used | S256, 64-byte verifier | **No** |
| Session endpoint | `https://auth.jagex.com/game-session/v1/sessions` | same | Yes |
| Session request body field | `idToken` | same | Yes |
| Session response field | `sessionId` | same | Yes |
| Accounts endpoint | `https://auth.jagex.com/game-session/v1/accounts` | same | Yes |
| Accounts auth header | `Bearer <sessionId>` | same | Yes |
| Account ID field | `accountId` | same | Yes |
| Display name field | `displayName` (required) | `displayName` (optional) | **Partial** |
| `userHash` field | absent | present (required) | **No** |
| JX_ variable names (all 5) | confirmed | confirmed | Yes |
