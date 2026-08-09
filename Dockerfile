# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/SharingBridge.UserService/SharingBridge.UserService.csproj src/SharingBridge.UserService/
RUN dotnet restore src/SharingBridge.UserService/SharingBridge.UserService.csproj
COPY src/SharingBridge.UserService/ src/SharingBridge.UserService/
RUN dotnet publish src/SharingBridge.UserService/SharingBridge.UserService.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Render injects PORT at runtime; Program.cs binds to 0.0.0.0:$PORT
ENV ASPNETCORE_URLS=
EXPOSE 10000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SharingBridge.UserService.dll"]
