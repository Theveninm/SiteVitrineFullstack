# Build multi-stage : le SDK .NET (~800 Mo, compilateur + outils) sert à compiler, mais seul le
# runtime ASP.NET (~110 Mo) part en production. L'image finale ne contient donc ni code source,
# ni compilateur.

# --- Stage 1 : compilation ---
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# On copie le .csproj SEUL avant le reste du code, puis on restaure les paquets NuGet.
# Docker met en cache le résultat de chaque instruction : tant que le .csproj ne change pas,
# le `restore` (lent, il télécharge le réseau) est repris du cache même si le code a changé.
COPY src/SiteVitrine.Api/SiteVitrine.Api.csproj src/SiteVitrine.Api/
RUN dotnet restore src/SiteVitrine.Api/SiteVitrine.Api.csproj

# Maintenant seulement le reste du code : c'est cette couche-là qui sera invalidée à chaque commit.
COPY . .
# UseAppHost=false : pas besoin de générer l'exécutable natif, l'ENTRYPOINT lance `dotnet <dll>`.
RUN dotnet publish src/SiteVitrine.Api/SiteVitrine.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# --- Stage 2 : image finale ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Npgsql tente de charger la bibliothèque Kerberos au premier chargement, même quand
# l'authentification se fait par mot de passe. Absente d'Alpine, elle provoque un
# « Error loading shared library libgssapi_krb5.so.2 » au démarrage — sans conséquence, mais
# qui pollue les logs. ~2 Mo pour s'en débarrasser.
RUN apk add --no-cache krb5-libs

# Dossier des données écrites par l'API (volume `storage` du compose), créé au nom de l'utilisateur de
# l'application. Un volume nommé VIDE recopie, à sa création, le contenu et les droits du dossier de
# l'image sur lequel il est monté : sans cette ligne il appartiendrait à root, et l'API, qui ne tourne
# pas en root, ne pourrait pas y écrire.
RUN mkdir -p /app/storage && chown "$APP_UID" /app/storage

# Les images .NET écoutent sur 8080 par défaut (et non 80) justement pour pouvoir tourner
# sans privilèges root — se lier à un port < 1024 en exigerait.
#
# Deux ports, deux surfaces : 8080 sert le site (lecture seule, c'est lui que Nginx relaie),
# 8081 sert l'administration technique. Le compose ne publie 8081 que sur 127.0.0.1, et le middleware
# UseAdminPortIsolation refuse les routes d'administration arrivées sur 8080.
ENV ASPNETCORE_HTTP_PORTS="8080;8081"
EXPOSE 8080 8081

COPY --from=build /app/publish .

# $APP_UID est défini par l'image de base : utilisateur non-root dédié. Si l'application est
# compromise, l'attaquant n'est pas root dans le conteneur.
USER $APP_UID

ENTRYPOINT ["dotnet", "SiteVitrine.Api.dll"]
