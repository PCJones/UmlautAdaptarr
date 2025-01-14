#!/bin/bash

# Download linux binary from https://github.com/PCJones/UmlautAdaptarr/releases

# script by schumi4 - THX!

#seedbox fix
export DOTNET_GCHeapHardLimit=20000000

# Basic Configuration
export TZ=Europe/Berlin

# Sonarr Configuration
export SONARR__ENABLED=true
export SONARR__HOST=https://name.server.usbx.me/sonarr/
export SONARR__APIKEY=APIKEY

# Radarr Configuration
export RADARR__ENABLED=false
export RADARR__HOST=http://localhost:7878
export RADARR__APIKEY=APIKEY

# Readarr Configuration
export READARR__ENABLED=false
export READARR__HOST=http://localhost:8787
export READARR__APIKEY=APIKEY

# Lidarr Configuration
export LIDARR__ENABLED=false
export LIDARR__HOST=http://localhost:8686
export LIDARR__APIKEY=APIKEY

# Multiple Sonarr Instances (commented out by default)
#export SONARR__0__NAME="NAME 1"
#export SONARR__0__ENABLED=false
#export SONARR__0__HOST=http://localhost:8989
#export SONARR__0__APIKEY=APIKEY
#export SONARR__1__NAME="NAME 2"
#export SONARR__1__ENABLED=false
#export SONARR__1__HOST=http://localhost:8989
#export SONARR__1__APIKEY=APIKEY

# Advanced Options
#export IpLeakTest__Enabled=false
#export SETTINGS__IndexerRequestsCacheDurationInMinutes=12
export SETTINGS__ApiKey="apikey" # Change to something unique! Then in Prowlarr, in the proxy settings set any username and use this ApiKey as password.
export SETTINGS__ProxyPort=1234 # Port for Proxy
export Kestrel__Endpoints__Http__Url="http://[::]:1235" # Port for UmlautAdaptarr API

chmod +x ./publish/UmlautAdaptarr

./publish/UmlautAdaptarr
