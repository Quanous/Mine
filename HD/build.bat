@echo off
REM HD.exe 만들기 (Python 설치 필요). 더블클릭하면 dist\HD.exe 생성됨
pip install pyinstaller
pyinstaller --noconsole --onefile --name HD --icon icon.ico --add-data "icon.png;." --add-data "icon.ico;." HD.pyw
echo.
echo 완료! dist\HD.exe 를 바탕화면으로 옮기세요.
pause
