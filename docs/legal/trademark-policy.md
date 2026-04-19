# Trademark Policy

**Effective:** as of repository adoption date
**Maintainer:** project maintainers, via GitHub issue tracker
**Short name:** "Azure OpenAI CLI" / `az-ai`

---

## 0. Not legal advice

This document is a **policy statement**, not legal advice. It is drafted by
project maintainers — not your counsel. Nothing here creates an attorney-client
relationship, waives any third party's rights, or binds any trademark owner.
If you need a legal opinion about your specific situation — consult your own
lawyer. We disclaim, we decline, we defer to counsel.

This policy governs use of the project's own naming only. It does **not**
grant any right in any third-party mark (see §3).

---

## 1. Scope

This policy covers:

- The project name **"Azure OpenAI CLI"** and the command/binary name **`az-ai`**
- Any logos, wordmarks, or graphical identifiers distributed from this
  repository (collectively, the "Project Marks")

It does **not** cover the code itself — the code is governed by the MIT
`LICENSE`. A copyright license is not a trademark license. Different beasts.

---

## 2. The project's posture

The Project Marks are **unregistered** common-law designators used to identify
this specific open-source project. The maintainers assert only such rights as
arise from use in commerce and community recognition. No ™ or ® is claimed,
no federal registration is pleaded, no state-level protection is invoked
beyond what the law supplies by default.

The project is licensed MIT. The **code** may be freely copied, modified, and
redistributed under that license. The **name** may not — naming a derivative
or downstream work is governed by §5 and §6 below.

---

## 3. Third-party marks — nominative use only

This project references marks owned by third parties. We use them **only
nominatively** — that is, to honestly identify the upstream products this
software interoperates with. Specifically:

- **"Azure"** and **"Microsoft"** are trademarks of Microsoft Corporation.
- **"OpenAI"** is a trademark of OpenAI, L.L.C.
- **".NET"** is a trademark of the .NET Foundation / Microsoft Corporation.

All such marks remain the property of their respective owners. This project
is **not** affiliated with, endorsed by, sponsored by, or otherwise connected
to Microsoft Corporation, the .NET Foundation, or OpenAI, L.L.C. No such
relationship is claimed, implied, or to be inferred.

Nominative use in this repository follows the classical three-factor test:
(a) the referenced product is not reasonably identifiable without the mark;
(b) only so much of the mark is used as is necessary; (c) nothing is done to
suggest sponsorship or endorsement. If you spot a usage that strays from that
posture — file an issue. We'll fix it.

For attribution, see the `NOTICE` file at the repository root.

---

## 4. Permitted uses (of the Project Marks)

You may, without asking:

- **Refer to the project by name** in articles, tutorials, blog posts, talks,
  social media, bug reports, and academic work — that's descriptive, that's
  honest, that's fine.
- State factual compatibility: *"works with Azure OpenAI CLI,"* *"tested
  against `az-ai` v1.x,"* *"a plugin for Azure OpenAI CLI."*
- Link to the repository, releases, or documentation using the project name.
- Quote the name in package manifests, dependency lists, `README` files, and
  bill-of-materials output.
- Use the name in unmodified redistribution — shipping the project as-is,
  under its own name, is exactly what the MIT license contemplates.

---

## 5. Prohibited uses (of the Project Marks)

You may **not**:

- Imply this is an **official Microsoft**, **Azure**, or **OpenAI** product —
  it is not, it has never been, it is not becoming one.
- Imply the project **endorses** your product, service, company, or fork when
  it does not.
- Use the Project Marks — or confusingly similar variants (`az-ai-pro`,
  `AzureOpenAI-CLI-Plus`, `az_ai`, and similar near-misses) — as the **primary
  identifier of a fork or derivative** whose behavior materially diverges
  from upstream (see §6).
- Reproduce any logo, wordmark, or graphical identifier from this repository
  in advertising, merchandise, or promotional material without prior written
  permission from the maintainers.
- Register the Project Marks (or confusingly similar marks) as a trademark,
  domain name, package name, social-media handle, or company name in any
  jurisdiction.
- Use the Project Marks in a manner that is false, misleading, disparaging,
  or that tarnishes the project's reputation.

---

## 6. Forks and derivatives

Forking is encouraged — that's the MIT license working as intended. But:

- If your fork's behavior **materially diverges** from upstream (incompatible
  CLI surface, different auth model, different output formats, different
  security posture) — **rename it.** Pick a name that does not begin with
  `az-ai-`, does not include "Azure OpenAI CLI" as the leading phrase, and
  does not invite confusion with the upstream project.
- Unmodified or near-unmodified redistributions may retain the name,
  provided they clearly identify themselves as redistributions and do not
  imply maintainer endorsement of any bundled modifications.
- If you publish a package (NuGet, Homebrew tap, Docker image) derived from
  this project, your package name must make the fork-vs-upstream distinction
  obvious to a reasonable user at install time.
- Do **not** use the Project Marks to imply maintainer endorsement of your
  fork, your company, or your commercial support offering.

When in doubt, **rename and attribute**. Attribution to upstream is welcome;
appropriation of identity is not.

---

## 7. Enforcement posture

The maintainers reserve the right to request correction of any use of the
Project Marks that violates this policy. Requests will ordinarily be made by
public issue or email to the maintainers before any further action. We prefer
a conversation to a complaint.

Nothing in this policy waives any rights the maintainers or any third-party
mark owner may have under applicable law.

---

## 8. Changes

This policy may be revised. Revisions take effect on merge to the default
branch and are versioned with the repository. Prior versions remain available
in Git history.

---

## 9. Contact

Questions, clarifications, or requests for permission: open an issue on the
GitHub repository and tag it `legal` / `trademark`. For sensitive inquiries,
use the contact path documented in `SECURITY.md` and mark the subject line
accordingly.

---

*Prepared on behalf of the project by its maintainers. Not legal advice. Your
mileage, your jurisdiction, and your counsel may vary.*
