# PrusaPrinterApiForObs

This is essentially a reverse proxy for the API on the printer.  Note that it has not been tested against the cloud Prusa dashboard service and probably won't work, but if you do, I'd love to hear it.  This API logs into the printer using the username "maker" and password, executes the API and then returns the data.  It has only two endpoints:
- /api/status - This is a perfect reflection of the printer's status API and is mainly for testing.
- /api/statusImage - This endpoint generates a PNG image of the current status of the print.  It uses the Job api for this.

You configure it by setting environment variables:
- LocalPrusaBaseUrl - This is the IP address of your printer, probably something like 192.168.1.232, depending on your network.
- LocalPrusaApiKey - This is the API Key for your printer.  You can find this key on the settings page in Prusa Connect.  This key identifies your printer.

Generally you would compile this program and publish it to a directory under your web server.  I'm using IIS, so I created a directory in inetpub and I pointed the publish to that directory.  I configured IIS to know that there is an application at this location.  Note that this is .net 7, so you configure your application pool with No Managed Code (since it is self-contained).  Make sure you set the environment variables for this web app.  There are lots of ways to do this, but what I suggest is using the Configuration Editor, then choose "system.webServer/aspNetCore" From ApplicationHost.config.  This way the values won't change when you do a publish.

This has only been tested against the Prusa MK4, but from the information I've seen on the Internet, it should work against other models as well.  If you get it working against another model, please let me know and I'll update this readme.

This code is free to do whatever you want to do with it, is not warranteed to be useful in any way, and there is no explicit support, though if you submit a ticket I'll probably read it.  And, in advance, no I won't post a compiled version, it's a tiny program and Visual Studio (or Code) has a free community version that will work just fine.
