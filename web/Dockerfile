FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Dev/demo only: baked into the bundle. Production would use MSAL sign-in instead.
ARG VITE_DEV_TOKEN
ENV VITE_DEV_TOKEN=$VITE_DEV_TOKEN
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
