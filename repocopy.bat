@echo off
rem This is for copying changes from the directory we're working with to the actual git repo
rem Only older files will be replaced per the /d option
set modfolder=%1
xcopy /e /i /y /d .\%modfolder% C:\Users\%USERNAME%\Documents\GitHub\dfu-mods\%modfolder%
