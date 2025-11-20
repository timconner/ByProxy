FROM mcr.microsoft.com/dotnet/aspnet:8.0-azurelinux3.0 AS base
ENV ASPNETCORE_HTTP_PORTS=
COPY ./ByProxy /app/ByProxy
WORKDIR /app/ByProxy
ENTRYPOINT ["dotnet", "/app/ByProxy/ByProxy.dll"]
