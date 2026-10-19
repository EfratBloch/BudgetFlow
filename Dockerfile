# Builds the BudgetFlow API server into a container image.
# Not needed for local development (use `dotnet run`); the database runs
# from docker-compose.yml.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so the restore layer is cached between builds
COPY BudgetFlow.sln Directory.Build.props ./
COPY BudgetFlow.Core/BudgetFlow.Core.csproj BudgetFlow.Core/
COPY BudgetFlow.Data/BudgetFlow.Data.csproj BudgetFlow.Data/
COPY BudgetFlow.Service/BudgetFlow.Service.csproj BudgetFlow.Service/
COPY BudgetFlow.API/BudgetFlow.API.csproj BudgetFlow.API/
RUN dotnet restore BudgetFlow.API/BudgetFlow.API.csproj

COPY . .
RUN dotnet publish BudgetFlow.API/BudgetFlow.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "BudgetFlow.API.dll"]
