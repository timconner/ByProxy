# ByProxy - Blazorized YARP Proxy

A reverse proxy built on top of **Microsoft YARP**.

ByProxy extends YARP’s core capabilities with certificate automation, advanced routing, and a full administrative web UI.

---

## Getting Started

The easiest way to run ByProxy is via Docker Compose.
The admin UI is exposed on **https://\<host\>:8081**

```yaml
services:
  byproxy:
    container_name: byproxy
    image: timconner/byproxy
    ports:
      - 80:80
      - 443:443
      - 8081:8081
    volumes:
      - ./byproxy:/app/data
    restart: unless-stopped
```

---

## Features

### Enhanced Routing & Control
- **Port-based filtering/routing**
- **Route outcomes:**
  - Forward to a cluster
  - Return an HTTP status
  - Issue an HTTP redirect
  - Tarpit when required
- **Optional HTTP → HTTPS redirection** (per-route)
- **Tarpit support** when no routes match

### Certificate Management
- **ACME support** using Let’s Encrypt (production & staging)
- **HTTP-01 and DNS-01 challenges**, including wildcard support
- **DNS-01 fulfilled through a custom C# script** executed from the Web UI
  - Script samples available in the [timconner/byproxy-dns-samples](https://github.com/timconner/ByProxy-DNS-Samples/) repository
- **Import existing certificates** or **issue certificates from internal CA(s)**

### Administration & Configuration
- **Full admin Web UI** built with hosted Blazor Server
- **Commit/confirm model for configuration changes**
- **Internal user management**
- **Supports most YARP request transforms**

---

## Architecture

ByProxy uses YARP as the underlying proxy engine while layering on:

- A revision-based configuration system
- Certificate automation and management
- UI-driven administration workflow
- A flexible routing engine that supports advanced behaviors (redirects, tarpit, static responses, cluster forwarding)

### Technologies Used
- **YARP** – Microsoft’s reverse proxy framework
- **ASP.NET Core** – Kestrel hosting, routing, SSL termination, background services
- **Entity Framework Core** - Database object-relational mapper
- **Blazor Server** – Interactive administrative web UI
- **UI Components**
  - Custom-built components for forms, configuration editors, tables, dialogs, and more
  - SVG icons from **tabler.io**
  - **SortableJS** for mobile-friendly drag-and-drop interactions
  - **Monaco Editor** for configuration diffing and editing embedded C# automation scripts

---

## Ideas for Future Enhancements

### Near-Term
- Response Transforms

### Mid-Term
- Rate Limiting
- Statistics / Status Pages
- Output Caching
- Route-based pre-forwarding authentication / authorization

### Long-Term
- C# Script Transforms
- Clustering support

---

## Running Without Docker

You can also run ByProxy directly on Windows or Linux. Install the
[ASP.NET Core Runtime for .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
and extract the published application files.

#### Windows

Run `ByProxy.exe` from the extracted application folder. You can double-click
it in File Explorer or launch it from a terminal:

```text
ByProxy.exe
```

If using PowerShell, use `.\ByProxy.exe`.

#### Linux

Open a terminal in the application folder and run:

```bash
dotnet ByProxy.dll
```

Binding to ports 80 and 443 may require additional permissions on Linux.
If running as a systemd service, use `AmbientCapabilities=CAP_NET_BIND_SERVICE`
to allow a non-root service account to bind to these ports.

### Data Storage

ByProxy stores its persistent data in a single folder:

```text
data/
├── proxy.db          # SQLite database
├── certs/            # Certificates and their private keys (.pfx)
└── acme/
    └── accounts/     # ACME account keys
```

By default, ByProxy creates a `data` folder **one level above the application
folder**. For example:

| Application location | Default data folder |
| --- | --- |
| `C:\ByProxy\app\ByProxy.exe` | `C:\ByProxy\data` |
| `/opt/byproxy/app/ByProxy.dll` | `/opt/byproxy/data` |

This location is based on where the application files are stored, not the
terminal's current directory.

#### Using a Different Data Folder

Set the `Proxy__DataDir` environment variable before starting ByProxy.
The folder must already exist, and the account running ByProxy must have
read and write access to it. Use an absolute path to avoid ambiguity.

**Windows (PowerShell):**

```powershell
New-Item -ItemType Directory -Force -Path "C:\ByProxy\Data" | Out-Null
$env:Proxy__DataDir = "C:\ByProxy\Data"
.\ByProxy.exe
```

**Linux:**

```bash
mkdir -p "/opt/byproxy/data"
export Proxy__DataDir="/opt/byproxy/data"
dotnet ByProxy.dll
```

These examples set the variable for the current terminal session.
For a service, set it in the service configuration instead.

**Ensure access to the folder is restricted. It contains
certificate private keys and ACME account keys.**

---

## Localization

The UI is currently available in **English**, but the application is structured for straightforward expansion into additional languages.
All translatable text is organized into language files located in `AdminApp\Languages`.

---

## Screenshots

![Intro Screen](./Screenshots/intro.png)

![Clusters](./Screenshots/clusters.png)

![Routes](./Screenshots/routes.png)

![SNI Maps](./Screenshots/sni-maps.png)

![Settings](./Screenshots/settings.png)