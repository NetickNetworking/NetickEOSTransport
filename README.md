# Netick EOS Transport

An Epic Online Services (EOS) integration for Netick, featuring support for relays and lobbies. Built on [EOS Plugin for Unity](https://github.com/EOS-Contrib/eos_plugin_for_unity_upm) - refer to its documentation to integrate additional EOS services.

## Features

- EOS P2P relay networking (free cross-platform relay)
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

## Usage Example

The `EOSGameStarter` script in the Examples folder demonstrates how to:

- Initialize and login to EOS
- Create lobbies
- Search for and join existing lobbies
- Handle connection events and errors

### Basic Flow

1. **Login**: Authenticate with EOS using your chosen method
2. **Create/Find Lobby**: Either create a new lobby or search for existing ones
3. **Start Netick**: Initialize Netick with the EOS transport
4. **Connect**: Join the lobby and connect to host

### How Connection Works

When using the EOS transport with Netick, the connection process works as follows:

**Host:**
```csharp
// Create a lobby through EOS
lobbyManager.CreateLobby(newLobby, (result) =>
{
    if (result == Result.Success)
    {
        // Start Netick as host
        Network.StartAsHost(EOSTransportProvider, default, SandboxPrefab);
    }
});
```

**Note**: In this example, the host starts immediately after lobby creation. However, this is not required - you can create a lobby and start the host later based on your game's needs. For example, you might want to wait for a certain number of players to join, or allow the lobby owner to manually start the game session. The lobby and Netick host are separate systems that you control independently.

**Client:**
```csharp
// Join the lobby through EOS
lobbyManager.JoinLobby(lobbyId, lobbyDetails, true, (result) =>
{
    if (result == Result.Success)
    {
        // Start Netick as client
        var client = Network.StartAsClient(EOSTransportProvider, default, SandboxPrefab);
        
        // Connect using the lobby owner's ProductUserId as the connection string
        string hostId = lobbyManager.GetCurrentLobby().LobbyOwner.ToString();
        client.Connect(default, hostId);
    }
});
```

**Important**: The connection string passed to `client.Connect()` must be the **lobby owner's ProductUserId** (converted to string). This is how the EOS transport identifies which peer to establish a P2P connection with. The lobby owner's ID is automatically available through `lobby.LobbyOwner` after successfully joining a lobby.

## Lobby Management

### Creating Lobbies

To create a lobby, configure a `Lobby` object with your desired settings and use the `EOSLobbyManager`:

```csharp
Lobby newLobby = new Lobby
{
    MaxNumLobbyMembers = 4,
    LobbyPermissionLevel = LobbyPermissionLevel.Publicadvertised,
    BucketId = "DefaultBucket", // Used for filtering lobbies
    PresenceEnabled = true,
    RTCRoomEnabled = false,
    AllowInvites = true
};

// Add custom attributes for filtering and display
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

**Key Settings:**
- **MaxNumLobbyMembers**: Maximum number of players allowed
- **LobbyPermissionLevel**: Controls who can see and join the lobby
  - `Publicadvertised`: Visible to everyone and joinable
  - `Joinviapresence`: Joinable through friend presence
  - `Inviteonly`: Only joinable via invite
- **BucketId**: A filter string for grouping lobbies (useful for different game modes or versions)
- **Attributes**: Custom key-value pairs for additional information and filtering

### Listing and Searching Lobbies

EOS provides a powerful lobby search system. Here's how to list and filter lobbies:

#### Basic Lobby Listing (All Lobbies in Bucket)

```csharp
var lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();

// Create a search handle
var searchOptions = new CreateLobbySearchOptions { MaxResults = 20 };
lobbyInterface.CreateLobbySearch(ref searchOptions, out LobbySearch searchHandle);

// Filter by bucket ID (recommended to separate game versions/modes)
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

// Execute the search
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

#### Advanced Filtering (Search by Attributes)

You can add multiple filters to narrow down results:

```csharp
// Search by lobby name
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

// Search by custom game mode attribute
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

// Filter by available slots
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

#### Processing Search Results

After a successful search, process the results:

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
            // Create a Lobby object from the details
            Lobby lobby = new Lobby();
            lobby.InitFromLobbyDetails(details);
            
            // Access lobby information
            string lobbyId = lobby.Id;
            uint maxPlayers = lobby.MaxNumLobbyMembers;
            uint currentPlayers = (uint)lobby.Members.Count;
            
            // Access custom attributes
            foreach (var attr in lobby.Attributes)
            {
                if (attr.Key == "LOBBYNAME")
                {
                    string lobbyName = attr.AsString;
                    Debug.Log($"Found lobby: {lobbyName} ({currentPlayers}/{maxPlayers})");
                }
            }
            
            // Store the LobbyDetails for joining later
            // You need to keep this reference to join the lobby
        }
    }
}
```

**Important**: Keep the `LobbyDetails` object returned from the search - you need it to join the lobby later. Don't forget to release the search handle when done:

```csharp
searchHandle.Release();
```

### Joining Lobbies

Once you have a lobby from search results:

```csharp
lobbyManager.JoinLobby(lobbyId, lobbyDetails, true, (result) =>
{
    if (result == Result.Success)
    {
        // Successfully joined lobby
        var client = Network.StartAsClient(EOSTransportProvider, default, SandboxPrefab);
        string hostId = lobbyManager.GetCurrentLobby().LobbyOwner.ToString();
        client.Connect(default, hostId);
    }
});
```

### Best Practices for Lobby Search

1. **Always use BucketId**: Use your game version or a mode identifier as the bucket ID to prevent version mismatches
   ```csharp
   BucketId = Application.version // or Network.GameVersion.ToString()
   ```
2. **Limit search results**: Don't retrieve more than you can display. Start with 10-20 results.
3. **Release search handles**: Always call `searchHandle.Release()` when done to free memory.
4. **Store LobbyDetails**: Keep the `LobbyDetails` reference from search results - you need it to join the lobby.
5. **Add meaningful attributes**: Include searchable attributes like game mode, map, skill level, region, etc.
6. **Update lobby attributes**: If lobby state changes (like available slots), update attributes so searches remain accurate.
7. **Handle empty results gracefully**: Always check if search returned zero results and provide feedback to the user.

## Authentication

EOS provides two main authentication flows:

### 1. Device ID Authentication

Device ID creates a hardware-tied persistent identity without requiring an Epic Games account. This is the simplest method for testing and works well for mobile or standalone applications.

**Implementation Example:**
```csharp
EOSManager.Instance.StartConnectLoginWithDeviceToken("User", (connectInfo) =>
{
    if (connectInfo.ResultCode == Result.Success)
    {
        // Successfully authenticated
    }
});
```

### 2. Epic Account Authentication

Epic Account authentication uses Epic Games accounts and supports multiple credential types:

**Available Credential Types:**
- **Developer (DevAuthTool)**: For local development and testing. Requires the DevAuthTool running locally.
- **AccountPortal**: Opens a browser for user login with their Epic Games account.
- **ExchangeCode**: Uses a one-time exchange code (typically from Epic Games Launcher).
- **PersistentAuth**: Uses cached credentials from a previous login.
- **Password**: Direct username/password login (requires special permissions).
- **ExternalAuth**: Login via external platforms (Steam, PlayStation, Xbox, etc.).

**Implementation Example (Developer/DevAuthTool):**
```csharp
var credentials = new Epic.OnlineServices.Auth.Credentials 
{ 
    Type = Epic.OnlineServices.Auth.LoginCredentialType.Developer,
    Id = "localhost:8888",  // DevAuthTool address
    Token = "YourDevUsername"  // Username from DevAuthTool
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
        // After Epic Account auth, connect to EOS
        EOSManager.Instance.StartConnectLoginWithEpicAccount(authCallback.LocalUserId, (connectInfo) =>
        {
            if (connectInfo.ResultCode == Result.Success)
            {
                // Successfully authenticated and connected
            }
        });
    }
});
```

**Note**: Epic Account authentication is a two-step process: first authenticate with Epic Account Services, then connect to EOS with the resulting credentials.

### Local Testing with Multiple Unity Instances

**Important**: EOS does not allow multiple logins using the same account or Device ID. To test locally with multiple Unity instances, you must use different authentication methods for each instance:

**Recommended Setup for Local Testing:**
- **First Instance**: Use Device ID authentication
- **Second Instance**: Use Epic Account authentication with DevAuthTool

**Setting Up DevAuthTool:**
1. Download the DevAuthTool from the [Epic Online Services documentation](https://dev.epicgames.com/docs/epic-account-services/developer-authentication-tool)
2. Run the tool and login with your Epic Games account
3. The tool will display credentials (e.g., `localhost:8888` with a username)
4. In your second game instance, select Developer login type and enter:
   - **ID/Address**: The address shown by DevAuthTool (e.g., `localhost:8888`)
   - **Token**: Your chosen username from DevAuthTool

This allows you to run two instances on the same machine with different EOS identities, enabling proper local multiplayer testing.

**Production Considerations:**
- For production builds, use **AccountPortal** or **ExternalAuth** for the best user experience
- Device ID is suitable for mobile games or applications where Epic accounts aren't required
- Developer authentication should only be used during development

## Important Notes

- Ensure your EOS application is properly configured before testing
- Lobby attributes can be customized for matchmaking and filtering
- Connection quality depends on relay server locations and player proximity
- Always release search handles after use to prevent memory leaks
- Use BucketId to version-separate lobbies and avoid compatibility issues

## Troubleshooting

### Common Issues

**"Failed to initialize EOS"**
- Verify your Product Name, Product ID, Sandbox ID, Client ID, and Client Secret are correct
- Ensure the EOS Plugin is properly installed
- Check that your client policy includes the necessary permissions

**"Cannot create/join lobby"**
- Confirm you're logged into EOS successfully
- Verify your sandbox is correctly configured
- Check that lobby permissions are enabled in your client policy

**"Connection timeout"**
- Ensure both clients are authenticated with EOS
- Verify P2P networking is enabled in your client policy
- Check firewall settings aren't blocking EOS connections

**"No lobbies found in search"**
- Verify you're searching the correct BucketId
- Check that lobbies exist with `LobbyPermissionLevel.Publicadvertised`
- Ensure you're authenticated before searching
- Try searching without filters to see all available lobbies

## Help

For issues related to the EOS C# SDK or Unity plugin itself, please open an issue on the [EOS Plugin for Unity repository](https://github.com/EOS-Contrib/eos_plugin_for_unity).

For Netick-related questions, join our [discord server](https://discord.com/invite/uV6bfG66Fx).

For EOS-specific issues or information, read the [Epic Online Services documentation](https://dev.epicgames.com/docs/epic-online-services).