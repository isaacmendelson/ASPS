# ============================================================
# Stage 1: Build Angular SPA
# ============================================================
FROM node:22-alpine AS build
WORKDIR /app

# Copy package files first for layer caching
COPY package.json package-lock.json ./
RUN npm ci --no-audit

# Copy source and build
COPY . .
RUN npx ng build --configuration=production

# ============================================================
# Stage 2: Serve with nginx
# ============================================================
FROM nginx:1.27-alpine AS runtime

# Remove default nginx content
RUN rm -rf /usr/share/nginx/html/*

# Copy built SPA (Angular 18 application builder outputs to dist/<project>/browser)
COPY --from=build /app/dist/angular/browser /usr/share/nginx/html

# Copy nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy runtime config injection script
COPY docker-entrypoint.sh /docker-entrypoint.d/40-inject-config.sh
RUN chmod +x /docker-entrypoint.d/40-inject-config.sh

EXPOSE 80

# nginx base image already provides: CMD ["nginx", "-g", "daemon off;"]
