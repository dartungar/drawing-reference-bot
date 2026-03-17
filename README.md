# Drawing Bot

Standalone Telegram bot for drawing references.

## Features
- `/draw <subject>` command
- `/random` command and `random topic` keyboard button for random topic suggestion
- `/randomref` command and `random reference` keyboard button for a fully random reference image
- Subject translation to English (optional, via OpenAI Responses API)
- Random topic suggestion when no subject is provided
- Inline buttons:
  - Confirm random topic
  - Suggest another topic
  - Different image
  - Try other source (Unsplash/Pexels)
  - Different subject

## Environment Variables
- `TELEGRAM_BOT_TOKEN` (required)
- `TELEGRAM_ALLOWED_USER_ID` (required)
- `AI_BASE_URL` (optional, default `https://api.openai.com/v1`)
- `AI_MODEL` (optional)
- `AI_API_KEY` (optional)
- `UNSPLASH_ACCESS_KEY` (optional but recommended)
- `PEXELS_API_KEY` (optional but recommended)

At least one of `UNSPLASH_ACCESS_KEY` or `PEXELS_API_KEY` must be configured.

## Random reference behavior
- Unsplash uses `GET /photos/random`
- Pexels uses `GET /v1/curated`, then picks a random curated photo from the returned page

## Run (local)
```bash
dotnet run --project DrawingBot.csproj
```

## Run (Docker Compose)
1. Create env file:
  ```bash
  cp .env.example .env
  ```
2. Fill in required values in `.env`:
  - `TELEGRAM_BOT_TOKEN`
  - `TELEGRAM_ALLOWED_USER_ID`
  - plus at least one image provider key (`UNSPLASH_ACCESS_KEY` or `PEXELS_API_KEY`)
3. Start:
  ```bash
  docker compose up --build -d
  ```
4. View logs:
  ```bash
  docker compose logs -f
  ```
