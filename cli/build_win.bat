@echo off
cd /d "%~dp0\.."

echo Building bridge-win...
pyinstaller --onefile --name bridge-win .claude\skills\unity-bridge\bridge.py --distpath .claude\skills\unity-bridge\bin\

echo Building mcp-server-win...
pyinstaller --onefile --name mcp-server-win .claude\skills\unity-bridge\mcp_server.py --distpath .claude\skills\unity-bridge\bin\

echo Done. Binaries in .claude\skills\unity-bridge\bin\
