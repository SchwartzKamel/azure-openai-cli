# Third-Party Notices

`az-ai` (the Azure OpenAI CLI) incorporates components from the following
third-party open source projects. Each component retains its own copyright
and is distributed under the license noted below. The full text of each
unique license appears once at the bottom of this file.

This file covers the **v2.x** dependency graph. For the authoritative audit
and reaffirmation cadence, see [`docs/licensing-audit.md`](docs/licensing-audit.md).

> **Version-vs-holder note.** The versions shown below are the resolved
> versions observed at the most recent manifest refresh. Transitive versions
> drift with each direct-pin bump; that drift is a supply-chain concern, not
> an attribution concern. MIT, Apache-2.0 §4(d), and BSD-3-Clause all bind
> attribution to the **copyright holder**, not to a specific version number,
> and the holders listed here are stable across the minor/patch churn typical
> of NuGet resolution. The **authoritative resolved graph for any given
> release** lives in the CycloneDX SBOM (see
> [`docs/security/sbom.md`](docs/security/sbom.md) and the `sbom.cdx.json`
> asset on each GitHub Release); this document is the human-readable
> attribution copy derived from that inventory.

---

## Manifest

### MIT-licensed components

Copyright holders as indicated. Distributed under the [MIT License](#mit-license).

| Package | Version | Copyright |
|---|---|---|
| Azure.AI.OpenAI | 2.1.0 | © Microsoft Corporation |
| Azure.Core | 1.44.1 | © Microsoft Corporation |
| dotenv.net | 3.1.2 | © 2017 Bolorunduro Winner-Timothy B |
| Microsoft.Agents.AI | 1.1.0 | © Microsoft Corporation |
| Microsoft.Agents.AI.Abstractions | 1.1.0 | © Microsoft Corporation |
| Microsoft.Agents.AI.OpenAI | 1.1.0 | © Microsoft Corporation |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.AI | 10.4.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.AI.Abstractions | 10.4.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.AI.OpenAI | 10.4.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Caching.Abstractions | 10.0.4 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Compliance.Abstractions | 10.4.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Configuration | 10.0.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.3 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Configuration.Binder | 10.0.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.4 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.3 | © .NET Foundation and Contributors |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.3 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.3 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Logging | 10.0.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Logging.Abstractions | 10.0.4 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Logging.Configuration | 10.0.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.ObjectPool | 10.0.4 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Options | 10.0.3 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.0 | © .NET Foundation and Contributors |
| Microsoft.Extensions.Primitives | 10.0.4 | © .NET Foundation and Contributors |
| Microsoft.Extensions.VectorData.Abstractions | 9.7.0 | © .NET Foundation and Contributors |
| Microsoft.ML.Tokenizers | 2.0.0 | © .NET Foundation and Contributors |
| Microsoft.NET.ILLink.Tasks † | 10.0.6 | © .NET Foundation and Contributors |
| OpenAI | 2.9.1 | © OpenAI |
| System.ClientModel | 1.10.0 | © .NET Foundation and Contributors |
| System.Memory.Data | 10.0.3 | © .NET Foundation and Contributors |
| System.Numerics.Tensors | 10.0.4 | © .NET Foundation and Contributors |

† `Microsoft.NET.ILLink.Tasks` is a build-time MSBuild tasks package used by
the AOT trimmer. It is **not** redistributed in `az-ai` binaries. Listed here
for completeness.

### Apache-2.0-licensed components

Distributed under the [Apache License, Version 2.0](#apache-license-version-20).

| Package | Version | Copyright |
|---|---|---|
| OpenTelemetry | 1.15.2 | © The OpenTelemetry Authors |
| OpenTelemetry.Api | 1.15.2 | © The OpenTelemetry Authors |
| OpenTelemetry.Api.ProviderBuilderExtensions | 1.15.2 | © The OpenTelemetry Authors |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.15.2 | © The OpenTelemetry Authors |

The upstream `opentelemetry-dotnet` repository carries no `NOTICE` file as
of the 1.15.2 release (verified 2026-04-10), so no additional attribution
text is required beyond this manifest and the license text below.

### BSD-3-Clause-licensed components

Distributed under the [BSD 3-Clause License](#bsd-3-clause-license).

| Package | Version | Copyright |
|---|---|---|
| Google.Protobuf | 3.30.2 | © 2008 Google Inc. |

---

## License texts

### MIT License

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

Applies to every package in the "MIT-licensed components" table above,
each holding the copyright indicated in its row.

### Apache License, Version 2.0

```
                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
```

Full license text: <https://www.apache.org/licenses/LICENSE-2.0.txt>

Applies to every package in the "Apache-2.0-licensed components" table above,
each holding copyright as indicated.

### BSD 3-Clause License

```
Copyright 2008 Google Inc.  All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

    * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
    * Neither the name of Google Inc. nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

Code generated by the Protocol Buffer compiler is owned by the owner
of the input file used when generating it.  This code is not
standalone and requires a support library to be linked with it.  This
support library is itself covered by the above license.
```

---

## Trademark notice

"Microsoft", "Azure", ".NET", "OpenAI", "Google", "Protocol Buffers", and
"OpenTelemetry" are trademarks of their respective owners. Use in this
project is nominative and descriptive only. See `NOTICE` and
`docs/legal/trademark-policy.md` for the full trademark posture.
