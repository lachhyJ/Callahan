FROM node:22-alpine AS build
WORKDIR /src
COPY package*.json .
RUN npm ci
COPY . .
ARG VITE_API_BASE=http://localhost:8080
ARG VITE_VAPID_PUBLIC_KEY=""
ENV VITE_API_BASE=$VITE_API_BASE
ENV VITE_VAPID_PUBLIC_KEY=$VITE_VAPID_PUBLIC_KEY
# vite.config.js's readBuildInfo() can't shell out to git in here — this
# build context is just this directory, with no .git, and this base image
# has no git binary either. deploy.sh computes these on the NAS instead.
ARG CALLAHAN_GIT_COMMIT=""
ARG CALLAHAN_GIT_BRANCH=""
ENV CALLAHAN_GIT_COMMIT=$CALLAHAN_GIT_COMMIT
ENV CALLAHAN_GIT_BRANCH=$CALLAHAN_GIT_BRANCH
RUN npm run build

FROM nginx:alpine AS runtime
# Same-origin in production (the app is served through this nginx, which proxies
# /api/). The local docker-dev stack overrides it because there the app is on
# :8081 and the API on :8080 — a cross-origin call the CSP has to allow.
ARG CSP_CONNECT_SRC="'self'"
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/security-headers.conf
RUN sed -i "s|__CSP_CONNECT_SRC__|${CSP_CONNECT_SRC}|" /etc/nginx/security-headers.conf \
    && grep -q "__CSP_CONNECT_SRC__" /etc/nginx/security-headers.conf && exit 1 || true
EXPOSE 80
