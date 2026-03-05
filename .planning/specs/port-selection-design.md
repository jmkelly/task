# Port Selection Design (API Server)

Job: job-20260305-001
Tags: job-20260305-001,phase1,port

## Summary
The API server should **prefer port 8080** when it is available. If 8080 cannot be bound, the server **auto-assigns an available port** and reports the selected endpoint. This document defines the detection, fallback, and error behavior.

## Defaults and Precedence
1. **Explicit configuration wins** (for example, `ASPNETCORE_URLS`, `--urls`, or configured endpoints).
2. If no explicit configuration is provided, the server uses the **preferred default port: 8080**.
3. If 8080 is unavailable, the server falls back to **auto-assigned port (port 0)**.

## Availability Check (Prefer 8080)
- The system should attempt to bind the API server to `http://localhost:8080`.
- Availability is determined by the **binding result** at startup (i.e., the OS accepts the socket bind).
- If the bind succeeds, the server remains on **8080**.
- If the bind fails with an address-in-use or permission error, fallback is triggered.

## Auto-Assign Strategy (Fallback)
- On failure to bind 8080, the server should configure Kestrel to **bind to port 0** on loopback.
- Port 0 delegates selection to the OS, which chooses the next available port.
- After startup, the bound address must be discovered via `IServerAddressesFeature` and logged.

## Discovery and Reporting
- After startup, retrieve the final bound address from `IServerAddressesFeature`.
- Log the chosen endpoint in a structured, single-line message:
  - Example: `Server.Started port=53217 url=http://localhost:53217 reason=auto-assigned`
- When 8080 is used, log with `reason=preferred`.

## Error Handling
- **If binding 8080 fails**:
  - Log a warning with the failure cause (address in use, permission denied, etc.).
  - Proceed with auto-assign (port 0).
- **If auto-assigned binding fails**:
  - Log an error with the exception details and configuration context.
  - Exit with a non-zero code and a clear error message stating that the server could not bind to any port.

## Notes
- This behavior is limited to the default case when no explicit port configuration is provided.
- The auto-assigned port must be discoverable and surfaced to users and logs.
