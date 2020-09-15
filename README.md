# SimpleClone
Monitors and mirrors (clones) a directory to another directory.


We use a .NET file watcher to detect changes in a directory and we then mimic these changes to our "destination" directory.
(this is a .NET core application)



Basic usage is:
Usage: ```simpleclone [-cs | -createsource] [-nosync] [-syncinterval N(s,m,h)] <source> <destination>```

 ```-cs``` | ```-createsource```: Creates the ```<source>``` and ```destination``` directory (if it doesn't already exist)
```-nosync```: Skips inital sync
```-syncinterval N(s,m,h)```: Automatically sync (clone) the source to the destination ever N (seconds, minutes, or hours) (For example, ```-syncinterval 1m``` clones the source director ever 1 minute.

