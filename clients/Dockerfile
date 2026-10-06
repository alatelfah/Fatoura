# syntax=docker/dockerfile:1
# Fatoura web app — build: docker build -t fatoura-web clients/
FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json .npmrc ./
COPY shared/package.json shared/
COPY web/package.json web/
RUN npm ci --no-audit --no-fund
COPY shared/ shared/
COPY web/ web/
RUN npm run build -w @fatoura/web

FROM nginx:1.29-alpine
COPY web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/web/dist /usr/share/nginx/html
EXPOSE 80
