FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/src ./src
COPY frontend/public ./public
COPY frontend/next.config.ts frontend/tsconfig.json frontend/postcss.config.mjs frontend/eslint.config.mjs ./
RUN npm run build

FROM node:20-alpine AS runtime
ENV NODE_ENV=production
ENV HOSTNAME=0.0.0.0
ENV PORT=3000

COPY --from=build /app/.next/standalone /app
COPY --from=build /app/.next/static /app/frontend/.next/static
COPY --from=build /app/public /app/frontend/public

WORKDIR /app/frontend
EXPOSE 3000

CMD ["node", "server.js"]
