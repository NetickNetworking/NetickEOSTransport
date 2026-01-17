# Netick EOS Transport

A Netick transport for Epic Online Services (EOS) featuring support for relays and lobbies. Built on [EOS Plugin for Unity](https://github.com/EOS-Contrib/eos_plugin_for_unity_upm) - refer to its documentation to integrate additional EOS services.

## Features

- EOS P2P relay networking (free cross-platform relay)
- Lobby creation and discovery through EOS
- NAT punchthrough and relay fallback

## Dependencies

This package requires the following to be installed manually:

1. **[EOS Plugin for Unity](https://github.com/EOS-Contrib/eos_plugin_for_unity_upm)**
2. **[Netick for Unity](https://github.com/NetickNetworking/NetickForUnity)**

### Installation Order

1. Install the EOS Plugin for Unity first
2. Install Netick for Unity
3. Install this transport package

## Epic Online Services Setup

Before using this transport, you need to configure EOS through the Epic Developer Portal:

### 1. Create an EOS Application

- Visit the [Epic Games Developer Portal](https://dev.epicgames.com/portal/)
- Create a new application or use an existing one
- Note your **Product ID**, **Sandbox ID**, and **Deployment ID**

### 2. Configure Client Policy

In your EOS application settings:

- Navigate to **Product Settings > Clients**
- Create a new client or configure an existing one
- Set the **Client Policy** to include:
  - P2P networking permissions
  - Lobby permissions

### 3. Set Up Sandbox

- Configure your sandbox environment in the Developer Portal
- Ensure the sandbox is set to the correct deployment

### 4. Configure EOS Plugin in Unity

Once the EOS Plugin for Unity is installed:

- Open the EOS configuration window in Unity
- Enter your **Product ID**, **Sandbox ID**, **Deployment ID**, and **Client ID**
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

## Important Notes

- Ensure your EOS application is properly configured before testing
- Lobby attributes can be customized for matchmaking and filtering
- Connection quality depends on relay server locations and player proximity

## Troubleshooting

### Common Issues

**"Failed to initialize EOS"**
- Verify your Product ID, Sandbox ID, and Client ID are correct
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

## Support

For issues related to the EOS C# SDK or Unity plugin itself, please open an issue on the [EOS Plugin for Unity repository](https://github.com/EOS-Contrib/eos_plugin_for_unity).

For Netick-related questions, join our [discord server](https://discord.com/invite/uV6bfG66Fx).

For EOS-specific issues or information, read the [Epic Online Services documentation](https://dev.epicgames.com/docs/epic-online-services).