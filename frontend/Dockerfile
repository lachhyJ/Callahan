FROM node:22-alpine AS build
WORKDIR /src
COPY package*.json .
RUN npm ci
COPY . .
ARG VITE_API_BASE=http://localhost:8080
ARG VITE_VAPID_PUBLIC_KEY=""
ENV VITE_API_BASE=$VITE_API_BASE
ENV VITE_VAPID_PUBLIC_KEY=$VITE_VAPID_PUBLIC_KEY
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
