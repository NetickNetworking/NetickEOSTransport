using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Netick;
using Netick.Unity;
using PlayEveryWare.EpicOnlineServices;
using System.Collections.Generic;
using UnityEngine;

namespace NetickEOSTransport.Samples
{
  /// <summary>
  /// This is a helper script for quick prototyping, used to login with EOS and create/join lobbies.
  /// </summary>
  [AddComponentMenu("Netick/EOS Game Starter")]
  public class EOSGameStarter : MonoBehaviour
  {
    private enum AuthFlow { EpicAccount, DeviceID }

    [Header("Netick Configuration")]
    public GameObject SandboxPrefab;
    public EOSTransportProvider EOSTransportProvider;

    [Header("Lobby Settings")]
    public bool RTCRoomEnabled;

    [Header("Login Settings")]
    [SerializeField] private AuthFlow currentFlow = AuthFlow.DeviceID;
    [SerializeField] private Epic.OnlineServices.Auth.LoginCredentialType loginType = Epic.OnlineServices.Auth.LoginCredentialType.Developer;
    [SerializeField] private Epic.OnlineServices.Auth.AuthScopeFlags scope = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile;

    [Header("Credential Inputs")]
    [SerializeField] private string devAuthAddress = "localhost:8888";
    [SerializeField] private string credentialName = "DevUser";

    private EOSLobbyManager lobbyManager;

    // UI State
    private enum UIState { Login, LobbyBrowser, InGame }
    private UIState _currentState = UIState.Login;

    private string _statusMessage = "Idle";
    private bool _isProcessing = false;
    private Vector2 _scrollPos;
    private Rect _windowRect = new Rect(20, 20, 500, 450);

    // Lobby Data
    private string _lobbyName = "My Netick Lobby";
    private string _maxPlayersStr = "4";
    private string _bucketId = "DefaultBucket";
    private int _selectedTab = 0; // 0=Search, 1=Create

    // Search Data
    private string _searchValue = "";

    void Start()
    {
      lobbyManager = EOSManager.Instance.GetOrCreateManager<EOSLobbyManager>();
      _windowRect.x = (Screen.width - _windowRect.width) / 2;
      _windowRect.y = (Screen.height - _windowRect.height) / 2;
      _bucketId = Netick.Unity.Network.GameVersion.ToString();

      if (EOSTransportProvider == null)
        Debug.LogError("EOSGameStarter: EOSTransportProvider is null!");
    }

    // --- AUTHENTICATION LOGIC ---

    private void StartAuthentication()
    {
      if (currentFlow == AuthFlow.EpicAccount) StartEpicAccountLogin();
      else StartDeviceIdLogin();
    }

    private void StartEpicAccountLogin()
    {
      _isProcessing = true;
      _statusMessage = $"Attempting {loginType} Login...";

      var credentials = new Epic.OnlineServices.Auth.Credentials { Type = loginType };

      // Dynamically handle Credential Types based on selected login method
      switch (loginType)
      {
        case Epic.OnlineServices.Auth.LoginCredentialType.Developer:
        case Epic.OnlineServices.Auth.LoginCredentialType.Password:
          credentials.Id = devAuthAddress;
          credentials.Token = credentialName;
          break;

        case Epic.OnlineServices.Auth.LoginCredentialType.ExchangeCode:
        case Epic.OnlineServices.Auth.LoginCredentialType.ExternalAuth:
        case Epic.OnlineServices.Auth.LoginCredentialType.PersistentAuth:
          credentials.Token = credentialName;
          break;

        case Epic.OnlineServices.Auth.LoginCredentialType.AccountPortal:
          // Browser-based login does not require manual ID/Token entry
          break;
      }

      var loginOptions = new Epic.OnlineServices.Auth.LoginOptions()
      {
        Credentials = credentials,
        ScopeFlags = scope
      };

      EOSManager.Instance.GetEOSAuthInterface().Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo authInfo) =>
      {
        if (authInfo.ResultCode == Result.Success)
        {
          _statusMessage = "Auth Success! Connecting...";
          EOSManager.Instance.StartConnectLoginWithEpicAccount(authInfo.LocalUserId, OnConnectCallback);
        }
        else
        {
          _isProcessing = false;
          _statusMessage = $"Auth Error: {authInfo.ResultCode}";
        }
      });
    }

    private void StartDeviceIdLogin()
    {
      _isProcessing = true;
      _statusMessage = "Checking Device ID...";

      EOSManager.Instance.StartConnectLoginWithDeviceToken("User", (Epic.OnlineServices.Connect.LoginCallbackInfo connectInfo) =>
      {
        if (connectInfo.ResultCode == Result.NotFound || connectInfo.ResultCode == Result.InvalidParameters)
        {
          _statusMessage = "Creating new Device ID...";
          var createOptions = new Epic.OnlineServices.Connect.CreateDeviceIdOptions() { DeviceModel = SystemInfo.deviceModel };

          EOSManager.Instance.GetEOSConnectInterface().CreateDeviceId(ref createOptions, null, (ref Epic.OnlineServices.Connect.CreateDeviceIdCallbackInfo createIdInfo) =>
          {
            if (createIdInfo.ResultCode == Result.Success || createIdInfo.ResultCode == Result.DuplicateNotAllowed)
            {
              _statusMessage = "ID Created. Logging in...";
              EOSManager.Instance.StartConnectLoginWithDeviceToken("User", OnConnectCallback);
            }
            else
            {
              _isProcessing = false;
              _statusMessage = $"ID Creation Failed: {createIdInfo.ResultCode}";
            }
          });
        }
        else
        {
          OnConnectCallback(connectInfo);
        }
      });
    }

    private void OnConnectCallback(Epic.OnlineServices.Connect.LoginCallbackInfo connectInfo)
    {
      if (connectInfo.ResultCode == Result.Success)
      {
        _isProcessing = false;
        _statusMessage = "Online";
        _currentState = UIState.LobbyBrowser;
        BrowseLobbies(false);
      }
      else if (connectInfo.ResultCode == Result.InvalidUser)
      {
        _statusMessage = "Creating PUID for User...";
        EOSManager.Instance.CreateConnectUserWithContinuanceToken(connectInfo.ContinuanceToken, (Epic.OnlineServices.Connect.CreateUserCallbackInfo createRes) =>
        {
          if (createRes.ResultCode == Result.Success) StartAuthentication();
          else
          {
            _isProcessing = false;
            _statusMessage = $"PUID Creation Failed: {createRes.ResultCode}";
          }
        });
      }
      else
      {
        _isProcessing = false;
        _statusMessage = $"Connect Error: {connectInfo.ResultCode}";
      }
    }

    // --- LOBBY LOGIC ---

    void CreateNewLobby()
    {
      if (!uint.TryParse(_maxPlayersStr, out uint max)) return;

      Lobby newLobby = new Lobby
      {
        MaxNumLobbyMembers = max,
        LobbyPermissionLevel = LobbyPermissionLevel.Publicadvertised,
        BucketId = _bucketId,
        PresenceEnabled = true,
        RTCRoomEnabled = RTCRoomEnabled,
        AllowInvites = true
      };

      newLobby.Attributes.Add(new LobbyAttribute { Key = "LOBBYNAME", AsString = _lobbyName, Visibility = LobbyAttributeVisibility.Public });

      lobbyManager.CreateLobby(newLobby, (res) =>
      {
        if (res == Result.Success)
        {
          Netick.Unity.Network.StartAsHost(EOSTransportProvider, default, SandboxPrefab);
          _currentState = UIState.InGame;
        }
      });
    }

    void JoinLobby(string id, LobbyDetails details)
    {
      lobbyManager.JoinLobby(id, details, true, (res) =>
      {
        if (res == Result.Success)
        {
          var client = Netick.Unity.Network.StartAsClient(EOSTransportProvider, default, SandboxPrefab);
          client.Connect(default, lobbyManager.GetCurrentLobby().LobbyOwner.ToString());
          _currentState = UIState.InGame;
        }
      });
    }

    // --- UI RENDERING ---

    private void OnGUI()
    {
      DrawStatusHUD();

      if (_currentState == UIState.InGame)
      {
        if (GUI.Button(new Rect(20, Screen.height - 50, 120, 30), "Leave Game"))
        {
          Lobby current = lobbyManager.GetCurrentLobby();
          if (current != null && current.IsValid())
          {
            lobbyManager.LeaveLobby((r) => { Netick.Unity.Network.Shutdown(); });
          }

          _currentState = UIState.LobbyBrowser;
        }
        return;
      }

      if (!IsLoggedIn())
      {
        _windowRect = GUILayout.Window(0, _windowRect, DrawLoginWindow, "EOS Authentication");
      }
      else
      {
        _windowRect = GUILayout.Window(1, _windowRect, DrawLobbyWindow, "Lobby Management");
      }
    }

    private void DrawStatusHUD()
    {
      float width = 320;
      float height = 200;
      Rect hudRect = new Rect(Screen.width - width - 20, 20, width, height);

      GUI.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.85f);
      GUILayout.BeginArea(hudRect);
      GUILayout.BeginVertical(GUI.skin.box);

      GUILayout.BeginHorizontal();
      GUILayout.Space(12);
      GUILayout.BeginVertical();
      GUILayout.Space(8);

      GUILayout.Label("<size=13><b>EOS STATUS</b></size>");
      GUILayout.Space(4);

      if (IsLoggedIn())
      {
        GUILayout.Label($"<color=#50C878>● ONLINE</color>");

        Lobby current = lobbyManager.GetCurrentLobby();
        if (current != null && current.IsValid())
        {
          GUILayout.Label($"<color=#A9A9A9>Lobby:</color> {GetLobbyName(current)}");
          GUILayout.Label($"<color=#A9A9A9>Players:</color> {current.Members.Count}/{current.MaxNumLobbyMembers}");
          GUILayout.Label($"<size=10>ID: {current.Id}</size>");
        }
        else
        {
          GUILayout.Label("<color=#FFD700>Status: Ready (No Lobby)</color>");
        }
      }
      else
      {
        GUILayout.Label("<color=#FF4444>● OFFLINE</color>");
      }

      GUILayout.Space(10);
      GUILayout.EndVertical();
      GUILayout.Space(12);
      GUILayout.EndHorizontal();

      GUILayout.EndVertical();
      GUILayout.EndArea();

      GUI.backgroundColor = Color.white;
    }

    private void DrawLoginWindow(int id)
    {
      GUILayout.BeginVertical();
      GUILayout.Space(10);
      GUILayout.Label($"<b>Status:</b> {_statusMessage}", GUI.skin.box);

      if (GUILayout.Button($"Flow: {currentFlow}"))
      {
        currentFlow = (currentFlow == AuthFlow.EpicAccount) ? AuthFlow.DeviceID : AuthFlow.EpicAccount;
      }

      GUILayout.Space(10);

      if (currentFlow == AuthFlow.EpicAccount)
      {
        // Toggle through available credential types
        if (GUILayout.Button($"Type: {loginType}"))
        {
          var types = (Epic.OnlineServices.Auth.LoginCredentialType[])System.Enum.GetValues(typeof(Epic.OnlineServices.Auth.LoginCredentialType));
          int nextIndex = ((int)System.Array.IndexOf(types, loginType) + 1) % types.Length;
          loginType = types[nextIndex];
        }

        // Show fields based on selected login type
        if (loginType == Epic.OnlineServices.Auth.LoginCredentialType.Developer ||
            loginType == Epic.OnlineServices.Auth.LoginCredentialType.Password)
        {
          devAuthAddress = EditorGUILayoutField("ID/Address:", devAuthAddress);
        }

        if (loginType != Epic.OnlineServices.Auth.LoginCredentialType.AccountPortal)
        {
          credentialName = EditorGUILayoutField("Token/Pass:", credentialName);
        }
        else
        {
          GUILayout.Label("<color=silver>Account Portal will open a browser window.</color>", GetCenteredLabel());
        }
      }
      else
      {
        GUILayout.Label("<color=silver>Device ID Flow: Hardware-tied persistent ID.</color>", GetCenteredLabel());
      }

      GUILayout.FlexibleSpace();
      GUI.enabled = !_isProcessing;
      if (GUILayout.Button("START AUTHENTICATION", GUILayout.Height(40))) StartAuthentication();
      GUI.enabled = true;
      GUILayout.EndVertical();
    }

    private void DrawLobbyWindow(int id)
    {
      GUILayout.BeginHorizontal();
      if (GUILayout.Toggle(_selectedTab == 0, "Search", "Button")) _selectedTab = 0;
      if (GUILayout.Toggle(_selectedTab == 1, "Create", "Button")) _selectedTab = 1;
      GUILayout.EndHorizontal();

      _scrollPos = GUILayout.BeginScrollView(_scrollPos);
      if (_selectedTab == 0) DrawSearchLobbyTab();
      else DrawCreateLobbyTab();
      GUILayout.EndScrollView();

      if (GUILayout.Button("Logout & Shutdown", GUILayout.Height(30)))
      {
        Lobby current = lobbyManager.GetCurrentLobby();
        if (current != null && current.IsValid())
        {
          lobbyManager.LeaveLobby((r) => { Netick.Unity.Network.Shutdown(); });
        }

      
      }
      GUI.DragWindow();
    }

    private void DrawCreateLobbyTab()
    {
      _lobbyName = EditorGUILayoutField("Lobby Name:", _lobbyName);
      _maxPlayersStr = EditorGUILayoutField("Max Players:", _maxPlayersStr);
      _bucketId = EditorGUILayoutField("Bucket ID:", _bucketId);

      if (GUILayout.Button("CREATE & HOST", GUILayout.Height(40))) CreateNewLobby();
    }

    private void DrawSearchLobbyTab()
    {
      GUILayout.BeginHorizontal();
      _searchValue = GUILayout.TextField(_searchValue);
      if (GUILayout.Button("Search By Name", GUILayout.Width(120))) BrowseLobbies(true);
      GUILayout.EndHorizontal();

      if (GUILayout.Button("Refresh All (Default Bucket)")) BrowseLobbies(false);

      var results = lobbyManager.GetSearchResults();
      foreach (var kvp in results)
      {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label($"{GetLobbyName(kvp.Key)} ({kvp.Key.Members.Count}/{kvp.Key.MaxNumLobbyMembers})");
        if (GUILayout.Button("Join", GUILayout.Width(60))) JoinLobby(kvp.Key.Id, kvp.Value);
        GUILayout.EndHorizontal();
      }
    }

    // --- SEARCH HELPERS ---

    void BrowseLobbies(bool useFilter)
    {
      var lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
      var searchOptions = new CreateLobbySearchOptions { MaxResults = 20 };
      lobbyInterface.CreateLobbySearch(ref searchOptions, out LobbySearch searchHandle);

      var bucketParam = new LobbySearchSetParameterOptions
      {
        Parameter = new AttributeData { Key = "bucket", Value = new AttributeDataValue { AsUtf8 = _bucketId } },
        ComparisonOp = ComparisonOp.Equal
      };
      searchHandle.SetParameter(ref bucketParam);

      if (useFilter)
      {
        var nameParam = new LobbySearchSetParameterOptions
        {
          Parameter = new AttributeData { Key = "LOBBYNAME", Value = new AttributeDataValue { AsUtf8 = _searchValue } },
          ComparisonOp = ComparisonOp.Equal
        };
        searchHandle.SetParameter(ref nameParam);
      }

      UpdateManagerSearchHandle(searchHandle);

      var findOptions = new LobbySearchFindOptions { LocalUserId = EOSManager.Instance.GetProductUserId() };
      searchHandle.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo data) =>
      {
        if (data.ResultCode == Result.Success) ProcessSearchResults(searchHandle);
      });
    }

    private void UpdateManagerSearchHandle(LobbySearch handle)
    {
      // Accessing internal CurrentSearch to ensure proper memory cleanup of previous handles
      var field = typeof(EOSLobbyManager).GetField("CurrentSearch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      var currentSearch = (LobbySearch)field.GetValue(lobbyManager);
      currentSearch?.Release();
      field.SetValue(lobbyManager, handle);
    }

    private void ProcessSearchResults(LobbySearch search)
    {
      var countOptions = new LobbySearchGetSearchResultCountOptions();
      var results = lobbyManager.GetSearchResults();
      results.Clear();
      uint count = search.GetSearchResultCount(ref countOptions);

      var indexOptions = new LobbySearchCopySearchResultByIndexOptions();
      for (uint i = 0; i < count; i++)
      {
        indexOptions.LobbyIndex = i;
        if (search.CopySearchResultByIndex(ref indexOptions, out LobbyDetails details) == Result.Success)
        {
          Lobby l = new Lobby();
          l.InitFromLobbyDetails(details);
          results.Add(l, details);
        }
      }
    }

    private string GetLobbyName(Lobby lobby)
    {
      foreach (var attr in lobby.Attributes) if (attr.Key == "LOBBYNAME") return attr.AsString;
      return "Unnamed Lobby";
    }

    private string EditorGUILayoutField(string label, string value)
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label(label, GUILayout.Width(100));
      string res = GUILayout.TextField(value);
      GUILayout.EndHorizontal();
      return res;
    }

    private GUIStyle GetCenteredLabel()
    {
      var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
      return style;
    }

    bool IsLoggedIn() => EOSManager.Instance.GetProductUserId() != null && EOSManager.Instance.GetProductUserId().IsValid();
  }
}