#define NETWORK_H_

using System;
using System.Net.Sockets;

#if CONFIG_USE_EPOLL
    // using SocketMgrLinux;
    using ListenSocketLinux;
#endif

public class Network
{
    // Inclure les fichiers nécessaires
    // using Log;
    // using NGLog;
    // using CircularBuffer;
    // using SocketDefines;
    // using SocketOps;
    // using Socket;

#if CONFIG_USE_IOCP
    // using SocketMgrWin32;
    //using ListenSocketWin32;
#endif

#if CONFIG_USE_EPOLL
    // using SocketMgrLinux;
    using ListenSocketLinux;
#endif

#if CONFIG_USE_KQUEUE
    // using SocketMgrFreeBSD;
    // using ListenSocketFreeBSD;
#endif
}
