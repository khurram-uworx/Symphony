FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Symphony/Symphony.csproj
RUN dotnet publish src/Symphony/Symphony.csproj \
    -c Release -r linux-x64 --no-self-contained -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build --chown=app:app /app .
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl git gh jq nodejs npm openssh-client unzip \
        libkrb5-3 libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/App_Data /home/app/.aspnet/DataProtection-Keys /home/app/workspaces \
    && chown -R app:app /app/App_Data /home/app/.aspnet /home/app/workspaces
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV SYMPHONY_WORKSPACE_ROOT=/home/app/workspaces
ENTRYPOINT ["dotnet", "Symphony.dll"]
