# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SharingBridge.UserService.sln ./
COPY src/SharingBridge.UserService/SharingBridge.UserService.csproj src/SharingBridge.UserService/
RUN dotnet restore src/SharingBridge.UserService/SharingBridge.UserService.csproj
COPY src/SharingBridge.UserService/ src/SharingBridge.UserService/
RUN dotnet publish src/SharingBridge.UserService/SharingBridge.UserService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8081
ENV PORT=8081
EXPOSE 8081
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SharingBridge.UserService.dll"]
