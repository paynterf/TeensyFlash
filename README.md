# TeensyFlash
 C# command-line program for Teensy OTA
 
 This is the companion program for TeensyOTADemo.  When launched via a post-build
 command, it sends a trigger character (currently 'U' for Update) to the Teensy's
 Serial1 port, waits for the appropriate response, and then sends the .HEX file
 associated with the calling Teensy sketch.  If the number of lines sent match 
 the reported number of lines received, then this program confirms that by sending 
 the number of lines received to the Teensy sketch, which allows the update to 
 finish.
 
 Note that the Teensy sketch must include all the required 'FlasherX' code created
 by Joe Pasquariello. See the Teensy OTA threads on the Teensy forum for more
 information.  
 
 
