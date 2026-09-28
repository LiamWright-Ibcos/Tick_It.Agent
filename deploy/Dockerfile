# TickIt.Agent.Host container image
#
# Bundles TickIt.Agent.Host (the ASP.NET Core host) together with a published copy of
# TickIt.TeamSupport.Mcp. This is deliberate, not an oversight: at startup the Host spawns
# TickIt.TeamSupport.Mcp as a genuine MCP subprocess over stdio (see Program.cs /
# McpTicketSource.ConnectAsync) rather than calling it over a network transport - so the two
# have to land in the same container/filesystem, not as separate sidecar containers, unless
# that transport is changed first. McpServer__DllPath below points at where this image puts
# the published MCP server so the container doesn't rely on the dev-time sibling-directory
# auto-discovery in Program.cs (which assumes the source tree layout).
#
# Build from the repo root so the `src/` COPY paths resolve:
#   docker build -f deploy/Dockerfile -t tickit-agent-host:local .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first so `dotnet restore` is cached independently of source edits.
COPY src/TickIt.Core/TickIt.Core.csproj src/TickIt.Core/
COPY src/TickIt.TeamSupport.Mcp/TickIt.TeamSupport.Mcp.csproj src/TickIt.TeamSupport.Mcp/
COPY src/TickIt.Atlassian/TickIt.Atlassian.csproj src/TickIt.Atlassian/
COPY src/TickIt.Agent/TickIt.Agent.csproj src/TickIt.Agent/
COPY src/TickIt.Agent.Host/TickIt.Agent.Host.csproj src/TickIt.Agent.Host/

RUN dotnet restore src/TickIt.Agent.Host/TickIt.Agent.Host.csproj \
 && dotnet restore src/TickIt.TeamSupport.Mcp/TickIt.TeamSupport.Mcp.csproj

COPY src/ src/

RUN dotnet publish src/TickIt.Agent.Host/TickIt.Agent.Host.csproj -c Release -o /app/publish/host \
 && dotnet publish src/TickIt.TeamSupport.Mcp/TickIt.TeamSupport.Mcp.csproj -c Release -o /app/publish/mcp

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN useradd --system --create-home --shell /usr/sbin/nologin tickitagent \
 && mkdir -p /data \
 && chown -R tickitagent:tickitagent /app /data

COPY --from=build /app/publish/host ./
COPY --from=build /app/publish/mcp ./mcp/

# Container-time settings - override anything else (credentials, live-executor flags, the
# Teams webhook, polling interval) via environment variables / Key Vault secrets from
# Terraform. See appsettings.json for the full key list and TickIt.Agent's README for what
# each one does; none of these three need to change for a normal deploy.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    McpServer__DllPath=/app/mcp/TickIt.TeamSupport.Mcp.dll \
    Audit__LogFilePath=/data/audit-log.jsonl

EXPOSE 8080

USER tickitagent

ENTRYPOINT ["dotnet", "TickIt.Agent.Host.dll"]
