# Epic Online Services (EOS) Transport for Netick

An Epic Online Services (EOS) integration for Netick, featuring support for relays and lobbies. Built on [EOS Plugin for Unity](https://github.com/EOS-Contrib/eos_plugin_for_unity_upm) - refer to its documentation to integrate additional EOS services.

## Features

- EOS relay networking (free cross-platform relay)
- Lobby creation and discovery through EOS
- NAT punchthrough and relay fallback

## Installation

This package and its dependencies can be installed via Unity Package Manager using Git URLs.

**Dependencies:**
- **[EOS Plugin for Unity](https://github.com/EOS-Contrib/eos_plugin_for_unity_upm)**
- **[Netick for Unity](https://github.com/NetickNetworking/NetickForUnity)**

### Installing from Git

All dependencies can be installed via Unity Package Manager using Git URLs. For detailed instructions on installing packages from Git repositories, see the [Unity documentation on installing from a Git URL](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-ui-giturl.html).

**Quick Installation Steps:**

1. Open Unity Package Manager (Window > Package Manager)
2. Click the **+** button in the top-left corner
3. Select **Add package from git URL...**
4. Add the following packages in order:
   - First, add EOS Plugin: `https://github.com/EOS-Contrib/eos_plugin_for_unity_upm.git`
   - Then, add Netick for Unity: `https://github.com/NetickNetworking/NetickForUnity.git`
   - Finally, add this package: `https://github.com/NetickNetworking/NetickEOSTransport.git`
5. Click **Add** for each package

## Epic Online Services Setup

Before using this transport, you need to configure EOS through the Epic Developer Portal:

### 1. Create an EOS Application

- Visit the [Epic Games Developer Portal](https://dev.epicgames.com/portal/)
- Create a new application or use an existing one
- Note your **Product Name**, **Product ID**, **Sandbox ID**, and **Deployment ID**

### 2. Configure Client Policy

In your EOS application settings:

- Navigate to **Product Settings > Clients**
- Create a new client or configure an existing one
- Note your **Client ID** and **Client Secret**
- Set the **Client Policy** to include:
  - P2P networking permissions
  - Lobby permissions

### 3. Set Up Sandbox

- Configure your sandbox environment in the Developer Portal
- Ensure the sandbox is set to the correct deployment

### 4. Configure EOS Plugin in Unity

Once the EOS Plugin for Unity is installed:

- Open the EOS configuration window in Unity
- Enter your credentials:
  - **Product Name**
  - **Product ID**
  - **Sandbox ID**
  - **Deployment ID**
  - **Client ID**
  - **Client Secret**
- Save the configuration

> [!NOTE]
> Please note that 'Sandbox' refers here to an EOS-specific concept and is distinct from the sandbox terminology used in Netick

## Transport Setup

### 1. Create an EOSTransportProvider instance

1. In Unity, right-click in the Project window
2. Navigate to **Create > Netick > Transport > EOSTransportProvider**
3. Name the asset (e.g., "EOSTransportProvider")
4. Configure any transport-specific settings in the Inspector

### 2. Setup EOS in Your Scene

Create a GameObject to manage EOS:

1. Create a new GameObject in your scene (e.g., name it "EOS")
2. Add the **EOSManager** component to it
   - This component handles EOS initialization and must be present
3. Add the **EOSGameStarter** component to the same GameObject (optional, for testing)
   - This is an example script that demonstrates login, lobby creation, and joining
   - Assign the `EOSTransportProvider` asset you created to the transport field

**Important**: The `EOSManager` component must be in your scene for EOS to initialize properly. The `EOSGameStarter` is optional and serves as a reference implementation for how to use the transport.

## Getting Started with EOSGameStarter

The `EOSGameStarter` component provides a complete reference implementation demonstrating the typical workflow for using this transport. It handles the entire process from authentication through lobby management to establishing Netick connections.

### What EOSGameStarter Demonstrates

The example script shows the complete integration flow:

1. **Authentication**: How to log in using either Device ID or Epic Account credentials
2. **Lobby Creation**: Creating and configuring lobbies with custom attributes
3. **Lobby Discovery**: Searching for and listing available lobbies
4. **Joining Lobbies**: Connecting to existing lobbies found through search
5. **Netick Integration**: Starting Netick as host or client and establishing connections

### Using the Example

To test the transport using `EOSGameStarter`:

1. Add the component to your EOS GameObject (alongside `EOSManager`)
2. Assign your `EOSTransportProvider` asset to the Transport field
3. Configure your preferred authentication method in the Inspector
4. Enter Play mode and use the provided UI to:
   - Log in with your chosen authentication method
   - Create a new lobby or search for existing lobbies
   - Join a lobby and establish a connection

The script provides a functional starting point that you can reference when implementing your own lobby and matchmaking systems.

## Authentication

EOS provides two main authentication flows:

### Device ID Authentication

Device ID authentication creates a hardware-tied persistent identity without requiring an Epic Games account. This method is ideal for testing and works well for mobile or standalone applications where Epic account integration is not required.

```csharp
EOSManager.Instance.StartConnectLoginWithDeviceToken("User", (connectInfo) =>
{
    if (connectInfo.ResultCode == Result.Success)
    {
        // Successfully authenticated
    }
});
```

### Epic Account Authentication

Epic Account authentication integrates with Epic Games accounts and supports multiple credential types for different deployment scenarios:

**Credential Types:**
- **Developer**: Uses the DevAuthTool for local development and testing
- **AccountPortal**: Opens a browser for user login with Epic Games credentials
- **ExchangeCode**: Authenticates using a one-time exchange code
- **PersistentAuth**: Uses cached credentials from previous sessions
- **Password**: Direct username/password authentication (requires special permissions)
- **ExternalAuth**: Integrates with external platforms (Steam, PlayStation, Xbox, etc.)

**Implementation:**
```csharp
var credentials = new Epic.OnlineServices.Auth.Credentials 
{ 
    Type = Epic.OnlineServices.Auth.LoginCredentialType.Developer,
    Id = "localhost:8888",
    Token = "YourDevUsername"
};

var loginOptions = new Epic.OnlineServices.Auth.LoginOptions()
{
    Credentials = credentials,
    ScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile
};

EOSManager.Instance.GetEOSAuthInterface().Login(ref loginOptions, null, (authCallback) =>
{
    if (authCallback.ResultCode == Result.Success)
    {
        EOSManager.Instance.StartConnectLoginWithEpicAccount(authCallback.LocalUserId, (connectInfo) =>
        {
            if (connectInfo.ResultCode == Result.Success)
            {
                // Successfully authenticated
            }
        });
    }
});
```

**Note**: Epic Account authentication requires two steps: first authenticate with Epic Account Services, then connect to EOS using the resulting credentials.

### Local Testing with Multiple Unity Instances

For proper local testing of the lobby system, you need two distinct authenticated users. This is because a user who creates a lobby cannot join that same lobby as a client - you need separate identities to test the host-client relationship.

**Recommended Configuration for Local Testing:**
- **Instance 1 (Host)**: Device ID authentication
- **Instance 2 (Client)**: Epic Account authentication with DevAuthTool

**DevAuthTool Setup:**
1. Download DevAuthTool from the [Epic Online Services documentation](https://dev.epicgames.com/docs/epic-account-services/developer-authentication-tool)
2. Launch the tool and authenticate with your Epic Games account
3. Note the displayed credentials (typically `localhost:8888` with a username)
4. Configure your second instance with:
   - **Credential Type**: Developer
   - **ID**: Address from DevAuthTool (e.g., `localhost:8888`)
   - **Token**: Username from DevAuthTool

This configuration provides two distinct EOS identities on a single machine, enabling you to properly test lobby creation (with one user) and lobby joining (with another user).

### Authentication for Production

For production deployments:
- Use **AccountPortal** or **ExternalAuth** for optimal user experience
- Reserve **Device ID** for mobile applications or games not requiring Epic accounts
- Restrict **Developer** authentication to development environments only

## Lobby Management

Lobbies provide matchmaking functionality, allowing players to create, find, and join game sessions. All lobby operations require prior authentication.

### Creating Lobbies

Configure and create lobbies using the `EOSLobbyManager`:

```csharp
Lobby newLobby = new Lobby
{
    MaxNumLobbyMembers = 4,
    LobbyPermissionLevel = LobbyPermissionLevel.Publicadvertised,
    BucketId = "DefaultBucket",
    PresenceEnabled = true,
    RTCRoomEnabled = false,
    AllowInvites = true
};

newLobby.Attributes.Add(new LobbyAttribute 
{ 
    Key = "LOBBYNAME", 
    AsString = "My Game Lobby", 
    Visibility = LobbyAttributeVisibility.Public 
});

lobbyManager.CreateLobby(newLobby, (result) =>
{
    if (result == Result.Success)
    {
        // Lobby created successfully
    }
});
```

**Configuration Parameters:**
- **MaxNumLobbyMembers**: Maximum player capacity
- **LobbyPermissionLevel**: Access control settings
  - `Publicadvertised`: Publicly visible and joinable
  - `Joinviapresence`: Accessible through friend presence
  - `Inviteonly`: Requires explicit invitation
- **BucketId**: Organizational identifier for grouping lobbies (e.g., game version, mode)
- **Attributes**: Custom metadata for filtering and display purposes

### Searching for Lobbies

The lobby search system provides flexible filtering capabilities:

**Basic Search:**
```csharp
var lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();

var searchOptions = new CreateLobbySearchOptions { MaxResults = 20 };
lobbyInterface.CreateLobbySearch(ref searchOptions, out LobbySearch searchHandle);

var bucketParam = new LobbySearchSetParameterOptions
{
    Parameter = new AttributeData 
    { 
        Key = "bucket", 
        Value = new AttributeDataValue { AsUtf8 = "DefaultBucket" } 
    },
    ComparisonOp = ComparisonOp.Equal
};
searchHandle.SetParameter(ref bucketParam);

var findOptions = new LobbySearchFindOptions 
{ 
    LocalUserId = EOSManager.Instance.GetProductUserId() 
};

searchHandle.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo data) =>
{
    if (data.ResultCode == Result.Success)
    {
        ProcessSearchResults(searchHandle);
    }
});
```

**Advanced Filtering:**
```csharp
// Filter by lobby name
var nameParam = new LobbySearchSetParameterOptions
{
    Parameter = new AttributeData 
    { 
        Key = "LOBBYNAME", 
        Value = new AttributeDataValue { AsUtf8 = "Specific Lobby Name" } 
    },
    ComparisonOp = ComparisonOp.Equal
};
searchHandle.SetParameter(ref nameParam);

// Filter by custom attributes
var modeParam = new LobbySearchSetParameterOptions
{
    Parameter = new AttributeData 
    { 
        Key = "GAMEMODE", 
        Value = new AttributeDataValue { AsUtf8 = "DeathMatch" } 
    },
    ComparisonOp = ComparisonOp.Equal
};
searchHandle.SetParameter(ref modeParam);

// Filter by available capacity
var slotsParam = new LobbySearchSetParameterOptions
{
    Parameter = new AttributeData 
    { 
        Key = "AVAILABLESLOTS", 
        Value = new AttributeDataValue { AsInt64 = 1 } 
    },
    ComparisonOp = ComparisonOp.Greaterthanorequal
};
searchHandle.SetParameter(ref slotsParam);
```

**Processing Results:**
```csharp
private void ProcessSearchResults(LobbySearch search)
{
    var countOptions = new LobbySearchGetSearchResultCountOptions();
    uint count = search.GetSearchResultCount(ref countOptions);
    
    var indexOptions = new LobbySearchCopySearchResultByIndexOptions();
    
    for (uint i = 0; i < count; i++)
    {
        indexOptions.LobbyIndex = i;
        
        if (search.CopySearchResultByIndex(ref indexOptions, out LobbyDetails details) == Result.Success)
        {
            Lobby lobby = new Lobby();
            lobby.InitFromLobbyDetails(details);
            
            string lobbyId = lobby.Id;
            uint maxPlayers = lobby.MaxNumLobbyMembers;
            uint currentPlayers = (uint)lobby.Members.Count;
            
            foreach (var attr in lobby.Attributes)
            {
                if (attr.Key == "LOBBYNAME")
                {
                    string lobbyName = attr.AsString;
                    // Display or store lobby information
                }
            }
            
            // Retain LobbyDetails reference for joining
        }
    }
}
```

**Important**: Preserve the `LobbyDetails` object returned from search results - it is required for joining operations. Always release search handles after use:

```csharp
searchHandle.Release();
```

### Joining Lobbies

Join discovered lobbies using the stored `LobbyDetails` reference:

```csharp
lobbyManager.JoinLobby(lobbyId, lobbyDetails, true, (result) =>
{
    if (result == Result.Success)
    {
        // Successfully joined lobby
    }
});
```

### Lobby Management Best Practices

1. **Version Control**: Use BucketId to separate incompatible game versions
   ```csharp
   BucketId = Application.version
   ```
2. **Result Limiting**: Constrain search results to manageable quantities (10-20 initially)
3. **Resource Management**: Always release search handles to prevent memory leaks
4. **Reference Retention**: Store `LobbyDetails` from search results for joining operations
5. **Meaningful Metadata**: Include relevant attributes (game mode, map, region, skill level)
6. **Attribute Maintenance**: Update lobby attributes when state changes
7. **Empty Result Handling**: Provide appropriate user feedback when searches return no results

## Netick Integration

After authentication and lobby setup, establish Netick connections using the EOS transport.

### Connection Workflow

**Host Setup:**
```csharp
lobbyManager.CreateLobby(newLobby, (result) =>
{
    if (result == Result.Success)
    {
        Network.StartAsHost(EOSTransportProvider, default, SandboxPrefab);
    }
});
```

The host may be started immediately after lobby creation or deferred based on game requirements (e.g., minimum player count, manual start trigger). Lobby creation and Netick host initialization are independent operations.

**Client Setup:**
```csharp
lobbyManager.JoinLobby(lobbyId, lobbyDetails, true, (result) =>
{
    if (result == Result.Success)
    {
        var client = Network.StartAsClient(EOSTransportProvider, default, SandboxPrefab);
        
        string hostId = lobbyManager.GetCurrentLobby().LobbyOwner.ToString();
        client.Connect(default, hostId);
    }
});
```

**Critical**: The connection string must be the lobby owner's `ProductUserId` (converted to string). This identifier enables the EOS transport to establish connections with the correct peer. The lobby owner's ID is accessible via `lobby.LobbyOwner` after successful lobby join operations.

## Important Considerations

- Ensure EOS application configuration is correct before testing
- Authentication is required before accessing lobby functionality
- Lobby attributes enable custom matchmaking and filtering logic
- Connection quality depends on relay server proximity and network conditions
- BucketId should be used to prevent version incompatibility issues

## Troubleshooting

### EOS Initialization Failures
- Verify your Product Name, Product ID, Sandbox ID, Client ID, and Client Secret are correct
- Ensure the EOS Plugin is properly installed
- Check that your client policy includes the necessary permissions

### Authentication Issues
- **Device ID**: Ensure unique credentials per instance for proper testing
- **Epic Account**: Verify DevAuthTool is running (for Developer credential type)
- Confirm credential accuracy
- Validate client policy authentication permissions

### Lobby Issues
- Confirm you're logged into EOS successfully
- Verify your sandbox is correctly configured
- Check that lobby permissions are enabled in your client policy

### Connection Problems
- Ensure both peers are authenticated
- Verify P2P networking permissions in client policy
- Check firewall configuration
- Confirm correct ProductUserId usage in connection string

### Lobby Search Issues
- Verify you're searching the correct BucketId
- Check that lobbies exist with `LobbyPermissionLevel.Publicadvertised`
- Ensure you're authenticated before searching
- Try searching without filters to see all available lobbies


## Support Resources

For EOS Plugin or Unity-specific issues: [EOS Plugin for Unity Repository](https://github.com/EOS-Contrib/eos_plugin_for_unity)

For Netick-related questions: [Discord Server](https://discord.com/invite/uV6bfG66Fx)

For EOS platform documentation: [Epic Online Services Documentation](https://dev.epicgames.com/docs/epic-online-services)