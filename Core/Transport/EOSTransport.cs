/*
* Copyright (c) 2021 PlayEveryWare
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

//#define EOS_TRANSPORT_DEBUG
namespace NetickEOSTransport
{
  using Epic.OnlineServices;
  using Netick;
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using Collections = Unity.Collections;
  using PlayEveryWare.EpicOnlineServices;
  using Epic.OnlineServices.P2P;

  public class EOSTransport : NetworkTransport
  {
    /// <summary>
    /// EOS Connection wrapper for Netick
    /// </summary>
    public class EOSConnection : TransportConnection
    {
      public ProductUserId UserId;

      private EOSTransport _transport;
      public EOSConnection(EOSTransport transport)
      {
        _transport = transport;
      }

      public override IEndPoint EndPoint => new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0).ToNetickEndPoint();
      public override int       Mtu      => EOSTransportManager.MaxPacketSize;

      public override unsafe void Send(IntPtr ptr, int length)
      {
        SendEOS((byte*)ptr.ToPointer(), length, Epic.OnlineServices.P2P.PacketReliability.UnreliableUnordered);
      }

      public override unsafe void SendUserData(IntPtr ptr, int length, TransportDeliveryMethod method)
      {
        SendEOS((byte*)ptr.ToPointer(), length, method == TransportDeliveryMethod.Unreliable ? Epic.OnlineServices.P2P.PacketReliability.UnreliableUnordered : Epic.OnlineServices.P2P.PacketReliability.ReliableOrdered);
      }

      private unsafe void SendEOS(byte* ptr, int length, Epic.OnlineServices.P2P.PacketReliability reliability)
      {
        if (_transport._bytesBuffer.Length < length)
          _transport._bytesBuffer = new byte[length];
        fixed (byte* bytesBuffer = _transport._bytesBuffer)
          Collections.LowLevel.Unsafe.UnsafeUtility.MemCpy(bytesBuffer, ptr, length);

        _transport.P2PManager.SendPacket(UserId, P2PSocketName, new ArraySegment<byte>(_transport._bytesBuffer, 0, length), 0, false, reliability);
      }
    }

    private EOSTransportManager                      P2PManager;
    public const string                              P2PSocketName                = "EOSP2PTransport";
    
    private ProductUserId                            OurUserId                    { get => LocalUserIdOverride ?? EOSManager.Instance?.GetProductUserId(); }  // Our local EOS UserId (should always be valid after successful initialization)
    private ProductUserId                            ServerUserId                 = null;  // Locked in after calling Connect to avoid changing unexpectedly
    public ProductUserId                             LocalUserIdOverride          = null;  // Override local user id for testing multiple clients at once

    private bool                                     _isInitialized               = false;
    private bool                                     _isServer                    = false;
    private BitBuffer                                _buffer;
    private byte[]                                   _bytesBuffer                 = new byte[1024];

    private ulong                                    _nextTransportConnectiontId  = 1; // (ServerClientId is 0, so we start at 1)
    private Dictionary<ulong, ProductUserId>         _connectionIdToUserId        = null;
    private Dictionary<ProductUserId, ulong>         _userIdToConnectionId        = null;
    private Dictionary<ProductUserId, EOSConnection> _connections;
    private Queue<EOSConnection>                     _freeConnections;
    private RelayControl                             _relayControl;

    public EOSTransport(EOSTransportProvider provider) 
    {
      _relayControl = provider.RelayControl;
    }

    public override void Init()
    {
      // EOSManager should already be initialized and exist by this point
      if (EOSManager.Instance == null)
      {
        LogError("EOSTransport.Init: Unable to initialize - EOSManager singleton is null (has the EOSManager component been added to an object in your initial scene?)");
        return;
      }

      _buffer                                      = new BitBuffer(createChunks: false);
      _nextTransportConnectiontId                  = 1; // Reset local client ID assignment counter
      _connectionIdToUserId                        = new(32);
      _userIdToConnectionId                        = new(32);
      _connections                                 = new(32);
      _freeConnections                             = new(32);

      int connCount                                = Engine.IsClient ? 1 : Engine.MaxClients;
      for (int i = 0; i < connCount; i++)
        _freeConnections.Enqueue(new EOSConnection(this));

      // Initialize EOS Peer-2-Peer Manager
      if (LocalUserIdOverride?.IsValid() == true)
        P2PManager                                 = new EOSTransportManager(LocalUserIdOverride);
      else
        P2PManager                                 = EOSManager.Instance.GetOrCreateManager<EOSTransportManager>();

      P2PManager.OnIncomingConnectionRequestedCb   = OnIncomingConnectionRequestedCallback;
      P2PManager.OnConnectionOpenedCb              = OnConnectionOpenedCallback;
      P2PManager.OnConnectionClosedCb              = OnConnectionClosedCallback;

      if (P2PManager.Initialize() == false)
      {
        LogError("EOSTransport.Init: Unable to initialize - EOSP2PManager failed to initialize.");
        P2PManager.OnIncomingConnectionRequestedCb = null;
        P2PManager.OnConnectionOpenedCb            = null;
        P2PManager.OnConnectionClosedCb            = null;
        P2PManager                                 = null;
        return;
      }

      var setRelayControlOptions                   = new SetRelayControlOptions();
      setRelayControlOptions.RelayControl          = _relayControl;
      EOSManager.Instance.GetEOSP2PInterface().SetRelayControl(ref setRelayControlOptions);
      _isInitialized                               = true;
    }

    public override void Run(RunMode mode, int port)
    {
      _isServer = mode == RunMode.Server;
      switch (mode)
      {
        case RunMode.Server:
          _isServer = true;
          break;

        case RunMode.Client:
          _isServer = false;
          break;
      }
    }

    public override void Connect(string address, int port, byte[] connectionData, int connectionDataLen)
    {
      var serverUserIdToConnectTo = ProductUserId.FromString(address);

      if (serverUserIdToConnectTo == null)
      {
        Log("EOSTransport.Connect: No ServerUserIDToConnectTo set!");
        NetworkPeer.OnConnectFailed(ConnectionFailedReason.Refused);
        return;
      }

      // User provided a valid ServerUserIdToConnectTo?
      if (serverUserIdToConnectTo != null && serverUserIdToConnectTo.IsValid())
      {
        // Store it in ServerUserId so it can't be changed after start up
        ServerUserId = serverUserIdToConnectTo;

        // Attempt to connect to the server hosted by ServerUserId - was the request successfully initiated?
        if (!P2PManager.OpenConnection(ServerUserId, P2PSocketName))
        {
          LogError($"EOSTransport.Connect: Failed Client start up - Unable to initiate a connect request with Server UserId='{ServerUserId}'.");
          NetworkPeer.OnConnectFailed(ConnectionFailedReason.Refused);
        }
      }
      else
      {
        LogError("EOSTransport.Connect: Failed Client start up - 'ServerUserIdToConnectTo' is null or invalid."
            + " Please set a valid EOS ProductUserId of the Server host this Client should try connecting to in the 'ServerUserIdToConnectTo' property before calling Connect"
            + $" (ServerUserIdToConnectTo='{serverUserIdToConnectTo}').");
        NetworkPeer.OnConnectFailed(ConnectionFailedReason.Refused);
      }
    }

    public override void Shutdown()
    {
      if (!_isInitialized)
        return;

      _isInitialized = false;

      // Shutdown EOS Peer-2-Peer Manager
      if (P2PManager != null)
      {
        P2PManager.Shutdown();
        P2PManager.OnIncomingConnectionRequestedCb = null;
        P2PManager.OnConnectionOpenedCb = null;
        P2PManager.OnConnectionClosedCb = null;
        P2PManager = null;
      }

      _connectionIdToUserId?.Clear();
      _userIdToConnectionId?.Clear();
      _connections?.Clear();
      ServerUserId = null;
    }

    public override void PollEvents()
    {
      if (!_isInitialized)
        return;

      // Poll for incoming packets
      while (P2PManager.TryReceivePacket(out ProductUserId userId, /*out string socketName,*/ out byte channel, out ArraySegment<byte> packet))
      {
        //if (socketName != P2PSocketName)
        //  continue;

        if (!_connections.TryGetValue(userId, out EOSConnection connection))
          continue;

        unsafe
        {
          fixed (byte* ptr = packet.Array)
          {
            _buffer.SetFrom(ptr + packet.Offset, packet.Count, packet.Count);
            NetworkPeer.Receive(connection, _buffer);
          }
        }
      }
    }

    public override void Disconnect(TransportConnection connection)
    {
      if (connection is EOSConnection eosConnection)
      {
        if (_isServer)
          P2PManager.CloseConnection(eosConnection.UserId, P2PSocketName, true);
        else
          P2PManager.CloseConnection(ServerUserId, P2PSocketName, true);
      }
    }

    /// <summary>
    /// Called when a connection is originally requested by a remote peer (opened remotely but not yet locally).
    /// </summary>
    public void OnIncomingConnectionRequestedCallback(ProductUserId userId, string socketName)
    {
      // For now if we're a server we'll just accept all incoming requests trying to establish an "EOSP2PTransport" socket connection.
      if (_isServer && socketName == P2PSocketName)
      {
        // Check if we have space
        if (_freeConnections.Count == 0)
        {
          P2PManager.CloseConnection(userId, socketName);
          return;
        }

        P2PManager.OpenConnection(userId, socketName);  // Accept connection request
      }
      else
      {
        P2PManager.CloseConnection(userId, socketName); // Reject connection request
      }
    }

    /// <summary>
    /// Called immediately after a remote peer connection becomes fully opened (opened both locally and remotely).
    /// </summary>
    public void OnConnectionOpenedCallback(ProductUserId userId, string socketName)
    {
      if (socketName != P2PSocketName)
        return;

      EOSConnection connection;

      if (_isServer)
      {
        // We don't have this client in our map yet? (ie. We haven't seen them before)
        if (!_userIdToConnectionId.ContainsKey(userId))
        {
          // Add client ID mapping
          ulong newClientId = _nextTransportConnectiontId++; // Generate new client ID (locally unique, incremental)
          _connectionIdToUserId.Add(newClientId, userId);
          _userIdToConnectionId.Add(userId, newClientId);
        }

        // Get or create connection
        if (!_connections.TryGetValue(userId, out connection))
        {
          if (_freeConnections.Count == 0)
          {
            P2PManager.CloseConnection(userId, socketName, true);
            return;
          }

          connection        = _freeConnections.Dequeue();
          connection.UserId = userId;
          _connections.Add(userId, connection);
        }
      }
      else
      {
        // Client connecting to server
        if (!_connections.TryGetValue(userId, out connection))
        {
          connection        = _freeConnections.Dequeue();
          connection.UserId = userId;
          _connections.Add(userId, connection);
        }
      }

      // Notify Netick of the connection
      NetworkPeer.OnConnected(connection);
    }

    /// <summary>
    /// Called immediately before a fully opened remote peer connection is closed (closed either locally or remotely).
    /// </summary>
    public void OnConnectionClosedCallback(ProductUserId userId, string socketName)
    {
      if (socketName != P2PSocketName)
        return;

      if (_connections.TryGetValue(userId, out EOSConnection connection))
      {
        NetworkPeer.OnDisconnected(connection, TransportDisconnectReason.Shutdown); // Notify Netick of the disconnection
        // Return connection to pool
        _connections.Remove(userId);
        _freeConnections.Enqueue(connection);
      }
    }

    /// <summary>
    /// Gets the ProductUserId for an associated transportId.
    /// </summary>
    public ProductUserId GetUserId(ulong transportConnectionId)
    {
      if (!_isServer)
        return ServerUserId;
      return _connectionIdToUserId.TryGetValue(transportConnectionId, out var userId) ? userId : null;
    }

    public ulong GetTransportConnectionId(ProductUserId userId)
    {
      if (!_isServer)
        return 0; // Server is always 0 for clients
      return _userIdToConnectionId.TryGetValue(userId, out var transportId) ? transportId : 0;
    }

    [System.Diagnostics.Conditional("EOS_TRANSPORT_DEBUG")] private void Log(string msg)        => Debug.Log(msg);
    [System.Diagnostics.Conditional("EOS_TRANSPORT_DEBUG")] private void LogWarning(string msg) => Debug.LogWarning(msg);
    [System.Diagnostics.Conditional("EOS_TRANSPORT_DEBUG")] private void LogError(string msg)   => Debug.LogError(msg);
  }
}