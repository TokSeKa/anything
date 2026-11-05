@echo off
rem Memasang jadwal harian di Windows Task Scheduler (default jam 18:30).
rem Kalau laptop mati/tidur pada jamnya, tugas dijalankan begitu laptop menyala lagi.
setlocal
set "JAM=%~1"
if "%JAM%"=="" set "JAM=18:30"
set "BAT=%~dp0jalankan-harian.bat"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$a = New-ScheduledTaskAction -Execute $env:BAT -WorkingDirectory (Split-Path $env:BAT);" ^
  "$t = New-ScheduledTaskTrigger -Daily -At $env:JAM;" ^
  "$s = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 6);" ^
  "Register-ScheduledTask -TaskName 'PengumpulData' -Action $a -Trigger $t -Settings $s -Description 'Pengumpul data pasar harian' -Force | Out-Null"
if errorlevel 1 (
  echo Gagal memasang jadwal.
) else (
  echo Jadwal "PengumpulData" terpasang: setiap hari jam %JAM%.
  echo Cek/ubah di aplikasi Task Scheduler. Hapus dengan scripts\hapus-jadwal-windows.bat
)
pause
