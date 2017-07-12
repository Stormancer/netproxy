NetProxy
========

Netproxy is a simple ipv6/ipv4 UDP & TCP proxy based on .NET Core. It should build and run for any .NET Core 1.1 compatible platform, 
tested on *win10-x64* and *ubuntu.16.10-x64*.

Why? 
====
We needed a simple, crossplatform IPV6 compatible UDP forwarder, and couldn't find a satisfying solution. 
Nginx was obviously a great candidate but building it on Windows with UDP forwarding enabled was quite a pain.

The objective is to be able to expose as an ipv6 endpoint a server located in an ipv4 only server provider.
(Yes, as of 07/2017, I'm looking at you Google! Ok, you support ipv4 for TCP, but what about UDP?)

Disclaimer
==========
Error management exist, but is minimalist. IPV6 is not supported on the forwarding side.

Usage
=====
- Compile for your platform following instructions at https://www.microsoft.com/net/core
- Rewrite the config.json file to fit your need
- Run NetProxy

Configuration
=============
Config.json contains a map of named forwarding rules, for instance :

    {
     "http": {
     "localport": 80,
     "localip":"",
     "protocol": "tcp",
     "forwardIp": "xx.xx.xx.xx",
     "forwardPort": 80
     },
    ...
    }
       
- *localport* : The local port the forwarder should listen to.
- *localip* : An optional local binding IP the forwarder should listen to. If empty or missing, it will listen to ANY_ADDRESS.
- *protocol* : The protocol to forward. `tcp` or `udp`.
- *forwardIp* : The ip the traffic will be forwarded to.
- *forwardPort* : The port the traffic will be forwarded to.
   
