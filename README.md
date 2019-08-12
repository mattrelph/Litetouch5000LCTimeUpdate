# Litetouch5000LCTimeUpdate
Litetouch 5000LC CCU Time Update Utility - Since the internal clocks are prone to losing track of accurate time


 v 0.1
 
 This utility updates the time on a Litetouch 5000LC CCU.
 XXX.XXX.XXX.XXX = First argument is the Target CCU Host Address (IPv4 Address, FQDN, etc)
 -c = Use Computer Clock
 -i = Use Internet Time Service (time.nist.gov) (Default)
 -z = Check CCU Clock only. (No Update)
  
   
Litetouch Com Port is 10001  and uses TCP protocol

5000LC UART Protocol Commands (Carriage return = end of command)

Command:	Get Clock
Format:	R,DGCLK
Return:	R,RQRES,DGCLK,yyyymmddhhmmss
yyyy – year
mm – month
dd – day
hh – hours
mm – minutes
ss - seconds
Description:	Requests the date-time.

 
Command:	Set Clock
Format:	R,DSCLK,yyyymmddhhmmss	see Get Clock for details
Return:	none
Description:	Sets the clock with the specified date-time.
