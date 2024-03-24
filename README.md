# UwebServerCert
LetsEncrypt bot to automatically create an LetsEncrypt certificate for a Web Server

## Installation
```
dotnet tool install LetsencryptCert --global
```

## Executing the tool for the first time

Copy ```cert.json``` to current directory with following content:

```
{
    "account": "<account@domain.de>",
    "domains": [
        "<domain 1>", "<domain 2>", "<domain 3>"
    ],
    "data": {
        "CountryName": "<my country>",
        "State": "<my state",
        "Locality": "<my locality>",
        "Organization": "<my organization>",
        "OrganizationUnit": "<my organization unit>",
        "CommonName": "<my common name, main domain>"
    }
}

```

```
LetsencryptCert -create
``` 
This lets LetsencryptCert create an account.

Create a file ```letsencrypt-uweb``` in ```/etc``` (Linux) or ```c:\program data\letsencrypt-uweb``` (Windows). This text file should contain a password for the to be created pfx certificate.

## Obtaining certificate (test mode)
After executing the tool for the first time and having created an account, you can obtain a certificate from LetsEncrypt by running the tool:

```
LetsencryptCert
``` 

Your webserver has to serve the token to LetsEncrypt.

## Obtaining certificate 

To get a secure certificate, you have to set the command line argument ```-prod```:

```
LetsencryptCert -create -prod
LetsencryptCert -prod
```

## Executing tool every day (Linux)

```
crontab -e
```

Append

```
PATH=$PATH:/home/pi/.dotnet/tools
DOTNET_ROOT=/home/pi/.dotnet
0 4 * * * LetsencryptCert > /home/pi/logs/LetsencryptCert.log 2>&1
```

This executes dns update every day at 4 AM (universal time). Last log is saved in ```/home/pi/logs/LetsencryptCert.log```
