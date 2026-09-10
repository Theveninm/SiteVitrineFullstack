# --- Stage 1 : build de l'application Angular ---
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# --- Stage 2 : image finale, Nginx sert les fichiers statiques ---
FROM nginx:alpine AS serve

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/site-vitrine/browser /usr/share/nginx/html

EXPOSE 80
