# Etapa 1 — Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia o csproj e restaura dependências
COPY ["Certificado.csproj", "."]
RUN dotnet restore "Certificado.csproj"

# Copia o restante do código e publica
COPY . .
RUN dotnet publish "Certificado.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2 — Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copia o resultado do build
COPY --from=build /app/publish .

# Configura variáveis obrigatórias para Render
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Certificado.dll"]
