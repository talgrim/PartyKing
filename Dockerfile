# One image for Fly.io: nginx serves the UI and proxies /api to Kestrel (see docker/)

FROM node:22-alpine AS ui-build
WORKDIR /ui
COPY src/ui/PartyKing.UI/package.json src/ui/PartyKing.UI/package-lock.json ./
RUN npm ci
COPY src/ui/PartyKing.UI/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api-build
WORKDIR /src
COPY src/api/ ./
RUN dotnet publish PartyKing.API/PartyKing.API.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends nginx \
    && rm -rf /var/lib/apt/lists/* /etc/nginx/sites-enabled /etc/nginx/conf.d

WORKDIR /app
COPY --from=api-build /out ./
COPY --from=ui-build /ui/dist /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/nginx.conf
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Everything that has to survive a redeploy lives on the /data volume
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://127.0.0.1:5000 \
    ASPNETCORE_WEBROOT=/data/wwwroot \
    PARTYKING_DATABASE_PATH=/data/PartyKing.db \
    DataProtection__KeysDirectory=/data/keys

VOLUME /data
EXPOSE 8181
ENTRYPOINT ["/entrypoint.sh"]
