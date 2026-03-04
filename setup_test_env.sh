#!/bin/bash

# Navigate to the script's directory to ensure commands run in the correct context
cd "$(dirname "$0")"

# Check requirements
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet is not installed."
    exit 1
fi

if ! command -v npm &> /dev/null; then
    echo "Error: npm is not installed."
    exit 1
fi

# Define cleanup function
cleanup() {
    echo ""
    echo "Stopping processes..."
    # Kill all child processes in the same process group
    kill 0
    exit
}

# Trap SIGINT (Ctrl+C) and SIGTERM
trap cleanup SIGINT SIGTERM

echo "Installing npm dependencies..."
npm install

echo "Starting Tailwind CSS watcher..."
# Run tailwind watcher in background
npm run build:css &

echo "Starting ASP.NET Core application..."
# Run dotnet app
dotnet run

# Wait for all background processes (though dotnet run is foreground here, good practice)
wait
