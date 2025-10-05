# ===== Build stage =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Skopiuj plik projektu i przywróć zależności
COPY OBSAPP1/*.csproj OBSAPP1/
RUN dotnet restore OBSAPP1/OBSAPP1.csproj

# Skopiuj cały kod i zbuduj aplikację
COPY . .
RUN dotnet publish OBSAPP1/OBSAPP1.csproj -c Release -o /app/publish

# ===== Runtime stage =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
CMD ["dotnet", "OBSAPP1.dll"]
