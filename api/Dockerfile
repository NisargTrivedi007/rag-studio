# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RagAndAI.Api/RagAndAI.Api.csproj", "src/RagAndAI.Api/"]
RUN dotnet restore "src/RagAndAI.Api/RagAndAI.Api.csproj"

COPY . .
RUN dotnet build "src/RagAndAI.Api/RagAndAI.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/RagAndAI.Api/RagAndAI.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 5000
ENTRYPOINT ["dotnet", "RagAndAI.Api.dll"]
