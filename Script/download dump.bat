@ECHO OFF
REM ********************** START **********************
REM *** Author 	: ABO 			***
REM *** Date 	: 09/11/2016	***
REM *** Date 	: 18/11/2016	***
REM *** Version	: 1.3			***

REM clear screen
CLS

SET CURRENTDIR=
SET LOGFILENAME=%date:~-4%%date:~3,2%%date:~0,2%

REM set current directory to FtpConnect application
D:
cd D:\ASTEK\FtpConnect

CALL:LOGSCREENFILE ""
CALL:LOGSCREENFILE "*** START : Connecting to VPN ***"

REM launch the connection program and auto-connect to VPN_Neocles_TPAS_ABO
START "launcher title" "C:\Program Files\Fortinet\SslvpnClient\FortiSSLVPNclient.exe" connect -s "VPN_Neocles_TPAS_ABO" -i

:LOOP
PING spiddev.tpas.astek.fr -n 1 -w 1000
IF ERRORLEVEL 1 (
	CALL:LOGSCREENFILE "*** VPNSSL is not connected yet ; sleeping for 10 seconds ***"
	CALL:SLEEP10SEC
	GOTO LOOP
)

CALL:LOGSCREENFILE "*** VPN connected ***"
CALL:LOGSCREENFILE ""

REM winscp.com /script="script\download dump.ftp" /log=log\%LOGFILENAME%.log
D:\ASTEK\FtpConnect\FtpConnect.exe

CALL:LOGSCREENFILE ""
SET WINSCP_RESULT=%ERRORLEVEL%
IF %WINSCP_RESULT% EQU 0 (
  CALL:LOGSCREENFILE "*** Download successful ***"
  
  REM copy dump to merle
  COPY /Y "D:\ASPIN-SPID\97-BDD Dump\*.*" "\\172.22.40.63\ws\Projects\VIVOP\97-BDD Dump\*.*"
) ELSE (
  CALL:LOGSCREENFILE "*** Download error ***"
)

CALL:LOGSCREENFILE "*** END : Disconnecting from VPN ***"

REM close the VPN connection to VPN_Neocles_TPAS_ABO
START "launcher title" "C:\Program Files\Fortinet\SslvpnClient\FortiSSLVPNclient.exe" disconnect 

EXIT /b %WINSCP_RESULT%

@ECHO ON
@GOTO :EOF

::--------------------------------------------------------
:: Function section
::--------------------------------------------------------
:LOGSCREENFILE
IF NOT "%~1"=="" (
	ECHO %~1
	ECHO >>log\%LOGFILENAME%.log %date% %time% : %~1
)
IF "%~1"=="" (
	ECHO.
	ECHO. >>log\%LOGFILENAME%.log
)
GOTO :EOF
:SLEEP10SEC
PING -n 11 127.0.0.1 >NUL
GOTO :EOF
REM ********************** END **********************