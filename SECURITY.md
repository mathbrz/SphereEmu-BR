# Security policy

## Reporting a problem

If you find a security problem (account takeover, GM access without permission, item or money duplication, code
execution, data leak), **do not open a public issue**. Report it privately to the maintainers:

- Use GitHub's "Report a vulnerability" button on the repository's Security tab (private advisory), or
- contact a maintainer directly and share details only there.

Say what you found, how to reproduce it, and what it affects. We will answer as soon as we can. This is a volunteer
project, so there is no fixed response time.

## What counts

Authentication and password storage, GM and debug access, anything that lets a player act as another player or
create items and money, remote code execution, reading `sph.db` or logs from outside, and unsafe use of the
network.

## What is out of scope

The original game client and the official servers. Problems there belong to their owners.

## For contributors

Never commit passwords, tokens, captures, databases or logs. See
[docs/PR_REVIEW_CHECKLIST.md](docs/PR_REVIEW_CHECKLIST.md).
