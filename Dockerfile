FROM node:20-slim

WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install --omit=dev

COPY src ./src
RUN mkdir -p data

ENV NODE_ENV=production
ENV PORT=8081
EXPOSE 8081

CMD ["npm", "start"]
