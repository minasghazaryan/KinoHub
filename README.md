# KinoHub

ASP.NET Core Razor Pages app for movies and series. Kinopoisk API for metadata, Vibix for video player. Dark theme (slate/amber).

## Run with Docker

### Build and run the app only

```bash
docker build -t kinohub .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal;Database=KinoHubDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True;" \
  -e KinopoiskApiKey="your-kinopoisk-api-key" \
  kinohub
```

Use `host.docker.internal` when SQL Server runs on the host. For Linux, add `--add-host=host.docker.internal:host-gateway` if needed.

### Run app + SQL Server with Docker Compose

```bash
docker compose up -d
```

Then open http://localhost:8080. The app runs migrations on startup. SQL Server can take 10–20 seconds to be ready; if the app fails to connect, run `docker compose restart app` after a few seconds. Default SQL password is in `docker-compose.yml`—change it for production.

See [docker-compose.yml](docker-compose.yml) for environment variables (KinopoiskApiKey, Vibix, etc.). Pass them in the compose file or via a `.env` file.
