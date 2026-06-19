@echo off
echo Mazani zakazanych vystupu: bin, obj, dist, .vs
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist dist rmdir /s /q dist
if exist .vs rmdir /s /q .vs
echo Hotovo. Ted commitni a pushni.
pause
