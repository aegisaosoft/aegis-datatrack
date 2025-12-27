@echo off
echo Starting Vehicle Tracker...
echo.

:: Start backend in new window
echo Starting Backend on http://localhost:5000...
start "Vehicle Tracker Backend" cmd /k "cd /d %~dp0backend\VehicleTracker.Api && dotnet run --urls=http://localhost:5000"

:: Wait a bit for backend to start
timeout /t 3 /nobreak >nul

:: Start frontend in new window
echo Starting Frontend on http://localhost:5173...
start "Vehicle Tracker Frontend" cmd /k "cd /d %~dp0frontend && npm run dev"

echo.
echo Both services are starting...
echo Backend: http://localhost:5000/swagger
echo Frontend: http://localhost:5173
echo.
echo Press any key to exit this window (services will continue running)
pause >nul
