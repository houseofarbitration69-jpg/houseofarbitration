using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum Role
{
    Client,
    Server
}

public interface IConnectivityService
{
    event EventHandler<string> MessageReceived;
    event EventHandler<string> StatusChanged;
    event EventHandler<IEnumerable<string>> PeersFound;

    Task Start(Role role, string peerToConnect = null);
    Task Stop();
    Task ScanForPeers();
    Task SendMessage(string message);
}