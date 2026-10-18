# Builds the YouthGroups API server into a container image.
# Not needed for local development (use `dotnet run`); the database runs
# from docker-compose.yml.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so the restore layer is cached between builds
COPY YouthGroups.sln Directory.Build.props ./
COPY YouthGroups.Core/YouthGroups.Core.csproj YouthGroups.Core/
COPY YouthGroups.Data/YouthGroups.Data.csproj YouthGroups.Data/
COPY YouthGroups.Service/YouthGroups.Service.csproj YouthGroups.Service/
COPY YouthGroups.API/YouthGroups.API.csproj YouthGroups.API/
RUN dotnet restore YouthGroups.API/YouthGroups.API.csproj

COPY . .
RUN dotnet publish YouthGroups.API/YouthGroups.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "YouthGroups.API.dll"]
