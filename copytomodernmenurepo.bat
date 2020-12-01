rem This is for copying changes from the directory we're working with to the actual git repo
rem Only older files will be replaced per the /d option
xcopy /e /i /y /d .\ModernMenu C:\Users\rbrtc\Documents\GitHub\dfu-modernmenu\ModernMenu
pause
