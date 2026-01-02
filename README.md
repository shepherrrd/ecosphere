# Ecosphere - WebRTC Video Calling Application

A real-time video calling application built with .NET backend and Next.js frontend.

## Prerequisites

Before running the application, make sure you have the following installed:

- **PostgreSQL** (version 12 or higher)
- **Redis** (version 6 or higher)
- **.NET SDK** (version 9.0)
- **Node.js** (version 18 or higher)
- **npm** or **yarn**

## Backend Setup

### 1. Configure Database

Make sure PostgreSQL is running on your machine:

```bash
# Start PostgreSQL (macOS with Homebrew)
brew services start postgresql

# Or on Linux
sudo systemctl start postgresql
```

Create the database:

```bash
psql -U postgres
CREATE DATABASE ecosphere;
\q
```

### 2. Configure Redis

Start Redis:

```bash
# macOS with Homebrew
brew services start redis

# Or on Linux
sudo systemctl start redis
```

### 3. Configure Application Settings

Copy the example configuration file and update it with your settings:

```bash
cd backend/Ecosphere
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json` and update the following:

- **ConnectionStrings.Ecosphere**: Update with your PostgreSQL credentials
- **ConnectionStrings.Redis**: Update if Redis is not on default port
- **JwtSettings.Secret**: Set a strong secret key for JWT tokens
- **StunTurn.SharedSecret**: Set a strong secret for TURN server
- **Metered.Domain** and **Metered.SecretKey**: Add your Metered credentials (optional)

### 4. Run Database Migrations

```bash
cd backend/Ecosphere
dotnet ef database update
```

### 5. Start the Backend

```bash
cd backend/Ecosphere
dotnet run
```

The backend will start on `https://localhost:7156` and `http://localhost:5156`

## Frontend Setup

### 1. Install Dependencies

```bash
cd frontend
npm install
```

### 2. Configure Environment Variables

Create a `.env.local` file in the frontend directory:

```bash
cd frontend
touch .env.local
```

Add the following to `.env.local`:

```
NEXT_PUBLIC_API_URL=http://localhost:5156
NEXT_PUBLIC_SIGNALR_HUB_URL=http://localhost:5156/hubs/call
```

### 3. Start the Frontend

```bash
cd frontend
npm run dev
```

The frontend will start on `http://localhost:3000`

## Running the Complete Application

### Option 1: Run in Separate Terminals

**Terminal 1 - Backend:**
```bash
cd backend/Ecosphere
dotnet run
```

**Terminal 2 - Frontend:**
```bash
cd frontend
npm run dev
```

### Option 2: Quick Start Script

Create a file named `start.sh` in the project root:

```bash
#!/bin/bash

# Start backend in background
cd backend/Ecosphere
dotnet run &
BACKEND_PID=$!

# Start frontend in background
cd ../../frontend
npm run dev &
FRONTEND_PID=$!

echo "Backend running on http://localhost:5156"
echo "Frontend running on http://localhost:3000"
echo "Press Ctrl+C to stop both servers"

# Wait for Ctrl+C
trap "kill $BACKEND_PID $FRONTEND_PID; exit" INT
wait
```

Make it executable and run:

```bash
chmod +x start.sh
./start.sh
```

## Using the Application

1. Open your browser and navigate to `http://localhost:3000`
2. Create an account or log in
3. Start a new meeting or join an existing one
4. Share the meeting code with others to join

## Troubleshooting

### Backend won't start

- Check if PostgreSQL is running: `psql -U postgres -c "SELECT 1"`
- Check if Redis is running: `redis-cli ping`
- Verify database connection string in `appsettings.json`

### Frontend won't connect to backend

- Verify backend is running on `http://localhost:5156`
- Check `.env.local` has correct API URL
- Clear browser cache and restart frontend

### Database migration errors

```bash
# Reset migrations
cd backend/Ecosphere
dotnet ef database drop
dotnet ef database update
```

### Port already in use

If port 5156 or 3000 is already in use, you can change them:

**Backend:** Edit `backend/Ecosphere/Properties/launchSettings.json`

**Frontend:** Run with custom port:
```bash
npm run dev -- -p 3001
```

## Development Notes

- The backend uses Entity Framework Core for database operations
- The frontend uses Next.js 14 with App Router
- Real-time communication is handled via SignalR
- WebRTC is used for peer-to-peer video calling

## Production Build

### Backend
```bash
cd backend/Ecosphere
dotnet publish -c Release -o ./publish
```

### Frontend
```bash
cd frontend
npm run build
npm start
```
