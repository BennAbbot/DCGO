using DCGO.Networking;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LobbyManager_FriendMatch;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PunNetworkProvider : MonoBehaviourPunCallbacks, INetworkProvider
{
    Player[] _player = null;

    public GameNetworkEvents GameEvents => _gameEvents;
    private GameNetworkEvents _gameEvents = new GameNetworkEvents();

    public LobbyNetworkEvents LobbyEvents => _lobbyEvents;
    private LobbyNetworkEvents _lobbyEvents = new LobbyNetworkEvents();

    public MatchmakingEvents MatchmakingEvents => _matchmakingEvents;
    private MatchmakingEvents _matchmakingEvents = new MatchmakingEvents();

    public GameState GameState => _gameState;
    private GameState _gameState = GameState.Menu;

    string RandomMatchKey = "RandomMatchKey";

    List<RoomInfo> _matchmakingRooms = new List<RoomInfo>();

    public void Awake()
    {
        DCGONetwork.Provider = this;
    }

    public void Initialise()
    {
        _gameState = GameState.Menu;
    }

    public void StartMatchmaking(MatchmakingEvents MatchmakingEvents)
    {
        _matchmakingEvents = MatchmakingEvents;
        _gameState = GameState.Matchmaking;
        StartCoroutine(MatchmakingCoroutine());
    }

    public IEnumerator MatchmakingCoroutine()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.LeaveLobby();
        }

        yield return new WaitWhile(() => PhotonNetwork.InLobby);

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        yield return new WaitWhile(() => PhotonNetwork.IsConnected);

        yield return ContinuousController.instance.StartCoroutine(PhotonUtility.ConnectToLobbyCoroutine());

        yield return ContinuousController.instance.StartCoroutine(PhotonUtility.SignUpBattleDeckData());

        if (_matchmakingEvents.OnConnected != null)
        {
            _matchmakingEvents.OnConnected(true);
        }

        StartRandomMatch();

        yield return new WaitWhile(() => !PhotonNetwork.InRoom);

        yield return new WaitWhile(() => PhotonNetwork.CurrentRoom.PlayerCount != PhotonNetwork.CurrentRoom.MaxPlayers);

        _gameState = GameState.InGame;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        if (_matchmakingEvents.OnMatchFound != null)
        {
            _matchmakingEvents.OnMatchFound();
        }
    }

    #region Callback on room list update
    bool m;
    bool n;
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (this.gameObject.activeSelf)
        {
            if (!PhotonNetwork.InRoom && PhotonNetwork.InLobby && !m)
            {
                m = true;
                PhotonNetwork.LeaveLobby();
            }

            if (!PhotonNetwork.InRoom && PhotonNetwork.InLobby && n)
            {
                GetRandomMatcingRoom(roomList);
            }
        }

    }
    #endregion

    #region Search for random matching rooms available
    public void GetRandomMatcingRoom(List<RoomInfo> roomInfo)
    {
        if (GameState != GameState.Matchmaking)
        {
            return;
        }

        m = false;
        n = false;

        if (roomInfo == null || roomInfo.Count == 0)
        {
            return;
        }

        _matchmakingRooms = new List<RoomInfo>();

        for (int i = 0; i < roomInfo.Count; i++)
        {
            int p = roomInfo[i].PlayerCount;
            string n = roomInfo[i].Name;
            int m = roomInfo[i].MaxPlayers;
            object c = roomInfo[i].CustomProperties["RoomCreator"];

            if (p != 0 && m != 0 && c != null)
            {
                if (n.Contains(RandomMatchKey))
                {
                    _matchmakingRooms.Add(roomInfo[i]);
                }
            }
        }
    }
    #endregion

    #region Callback when leaving the lobby
    public override void OnLeftLobby()
    {
        if (this.gameObject.activeSelf)
        {
            if (m)
            {
                n = true;
                PhotonNetwork.JoinLobby();
            }
        }

    }
    #endregion

    #region Callback when joining a room fails
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (this.gameObject.activeSelf && GameState == GameState.Matchmaking)
        {
            Debug.Log($"[RandomMatch] Join room failed: [{returnCode}] {message}, retrying...");
            _matchmakingRooms = new List<RoomInfo>();
            m = false;
            n = false;

            if (!PhotonNetwork.InLobby)
            {
                PhotonNetwork.JoinLobby();
            }

            StartRandomMatch();
        }
    }
    #endregion

    #region Process to create a room
    public IEnumerator CreateRoomCoroutine(bool isRandomMatch)
    {
        yield return new WaitWhile(() => !PhotonNetwork.IsConnectedAndReady);
        yield return new WaitWhile(() => !PhotonNetwork.InLobby);

        //Setting up the room to be created
        RoomOptions roomOptions = new RoomOptions
        {
            IsVisible = true,   //Make the room visible in the lobby.
            IsOpen = true,      //Allow other players to enter the room
            PublishUserId = true,

            MaxPlayers = 2,

            //To display room creator in room custom properties, store creator's name
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable()
            {
                { "RoomCreator",PhotonNetwork.NickName },

            },

            //Display custom property information in the lobby
            CustomRoomPropertiesForLobby = new string[]
            {
                "RoomCreator",
            }
        };

        string RoomName = Guid.NewGuid().ToString();

        if (isRandomMatch)
        {
            RoomName += RandomMatchKey;
        }

        //Room Creation
        PhotonNetwork.CreateRoom(RoomName, roomOptions, null);

        while (!PhotonNetwork.InRoom)
        {
            yield return null;
        }
    }
    #endregion

    #region Random match starts after entering the lobby
    public void StartRandomMatch()
    {
        //If there is no room, make room.
        if (_matchmakingRooms.Count == 0)
        {
            StartCoroutine(CreateRoomCoroutine(true));
        }

        //If there's room, I'll go in.
        else
        {
            PhotonNetwork.JoinRoom(_matchmakingRooms[UnityEngine.Random.Range(0, _matchmakingRooms.Count)].Name);
        }
    }
    #endregion

    public void InitGame(Player[] players, GameNetworkEvents gameNetworkEvents)
    {
        _gameEvents = gameNetworkEvents;
        _player = players;
    }

    private Player GetPlayerFromID(int playerID)
    {
        foreach (var player in _player)
        {
            if (player.PlayerID == playerID)
            {
                return player;
            }
        }

        return null;
    }

    public void SendMainPhaseAction(Player player, MainPhaseAction action)
    {
        photonView.RPC("QueueMainPhaseAction_Internal", RpcTarget.All, player.PlayerID, GamePacketFactory.GetId(action.GetType()), action.Serialize());
    }

    [PunRPC]
    void QueueMainPhaseAction_Internal(int playerID, byte packetId, byte[] bytes)
    {
        Player player = GetPlayerFromID(playerID);

        if (player == null)
        {
            return;
        }

        MainPhaseAction action = GamePacketFactory.Create(packetId, bytes) as MainPhaseAction;
        GameEvents.OnMainPhaseAction(player, action);
    }

    public void SendPlayerSelection(Player player, IPlayerSelection playerSelection)
    {
        photonView.RPC("QueueMainPhaseAction_Internal", RpcTarget.All, player.PlayerID, GamePacketFactory.GetId(playerSelection.GetType()), playerSelection.Serialize());
    }

    [PunRPC]
    void QueuePlayerSelection_Internal(int playerID, byte packetId, byte[] bytes)
    {
        Player player = GetPlayerFromID(playerID);

        if (player == null)
        {
            return;
        }

        IPlayerSelection playerSelection = GamePacketFactory.Create(packetId, bytes) as IPlayerSelection;
        GameEvents.OnPlayerSelecton(player, playerSelection);
    }

    public void SendSurrender()
    {
       
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1))
        {
            _showPhotonDebug = !_showPhotonDebug;
        }
#endif
    }

#if UNITY_EDITOR
    #region Photon Debug HUD
    bool _showPhotonDebug = false;
    Photon.Realtime.IConnectionCallbacks _connectionCallbacks;
    Photon.Realtime.IMatchmakingCallbacks _matchmakingCallbacks;
    Photon.Realtime.ILobbyCallbacks _lobbyCallbacks;

    void OnEnable()
    {
        var listener = new PhotonDebugListener();
        _connectionCallbacks = listener;
        _matchmakingCallbacks = listener;
        _lobbyCallbacks = listener;
        PhotonNetwork.AddCallbackTarget(listener);
    }

    void OnDisable()
    {
        if (_connectionCallbacks != null)
            PhotonNetwork.RemoveCallbackTarget(_connectionCallbacks);
    }

    class PhotonDebugListener :
        Photon.Realtime.IConnectionCallbacks,
        Photon.Realtime.IMatchmakingCallbacks,
        Photon.Realtime.ILobbyCallbacks
    {
        public void OnDisconnected(Photon.Realtime.DisconnectCause cause)
        {
            Debug.LogWarning($"[Photon Debug] Disconnected: {cause}");
        }

        public void OnConnected() { }
        public void OnConnectedToMaster()
        {
            Debug.Log("[Photon Debug] Connected to Master");
        }
        public void OnRegionListReceived(Photon.Realtime.RegionHandler handler) { }
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            Debug.LogError($"[Photon Debug] Auth failed: {debugMessage}");
        }

        public void OnJoinedLobby()
        {
            Debug.Log("[Photon Debug] Joined Lobby");
        }
        public void OnLeftLobby()
        {
            Debug.Log("[Photon Debug] Left Lobby");
        }
        public void OnLobbyStatisticsUpdate(List<Photon.Realtime.TypedLobbyInfo> lobbyStatistics) { }
        public void OnRoomListUpdate(List<Photon.Realtime.RoomInfo> roomList) { }

        public void OnJoinedRoom()
        {
            Debug.Log($"[Photon Debug] Joined Room: {PhotonNetwork.CurrentRoom?.Name}");
        }
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[Photon Debug] Join Room FAILED: [{returnCode}] {message}");
        }
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogError($"[Photon Debug] Join Random FAILED: [{returnCode}] {message}");
        }
        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[Photon Debug] Create Room FAILED: [{returnCode}] {message}");
        }
        public void OnCreatedRoom()
        {
            Debug.Log("[Photon Debug] Created Room");
        }
        public void OnLeftRoom()
        {
            Debug.Log("[Photon Debug] Left Room");
        }
        public void OnFriendListUpdate(List<Photon.Realtime.FriendInfo> friendList) { }
    }

    void OnGUI()
    {
        if (!string.IsNullOrEmpty(PhotonUtility.RetryStatus))
        {
            GUIStyle retryStyle = new GUIStyle(GUI.skin.label);
            retryStyle.fontSize = 20;
            retryStyle.richText = true;
            retryStyle.fontStyle = FontStyle.Bold;
            retryStyle.alignment = TextAnchor.MiddleCenter;

            float boxW = 500;
            float boxH = 40;
            float x = (Screen.width - boxW) / 2;
            float y = (Screen.height - boxH) / 2;

            GUI.Box(new Rect(x - 10, y - 10, boxW + 20, boxH + 20), "");
            GUI.Label(new Rect(x, y, boxW, boxH), $"<color=#FFAA00>{PhotonUtility.RetryStatus}</color>", retryStyle);
        }

        if (!_showPhotonDebug) return;

        string status;

        if (!PhotonNetwork.IsConnected)
        {
            status = "<color=#FF4444>[Photon] Disconnected</color>";
        }
        else if (PhotonNetwork.InRoom)
        {
            string roomName = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "?";
            int players = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
            status = $"<color=#44FF44>[Photon] Connected | Region: {PhotonNetwork.CloudRegion} | Room: {roomName} ({players} players)</color>";
        }
        else if (PhotonNetwork.InLobby)
        {
            status = $"<color=#44FF44>[Photon] Connected | Region: {PhotonNetwork.CloudRegion} | In Lobby</color>";
        }
        else
        {
            status = $"<color=#FFAA00>[Photon] Connected | Region: {PhotonNetwork.CloudRegion} | Not in Lobby/Room</color>";
        }

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.richText = true;
        style.fontStyle = FontStyle.Bold;

        GUI.Label(new Rect(10, 10, 800, 30), status, style);
    }
    #endregion
#endif

    #region Manage connections to Photon
    public class PhotonUtility
    {
        public static string RetryStatus { get; set; } = null;

        #region Disconnected from Photon
        public static IEnumerator DisconnectCoroutine()
        {
            #region Exit Room
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            yield return new WaitWhile(() => PhotonNetwork.InRoom);
            #endregion

            #region Exit from the lobby
            if (PhotonNetwork.InLobby)
            {
                PhotonNetwork.LeaveLobby();
            }

            yield return new WaitWhile(() => PhotonNetwork.InLobby);
            #endregion

            #region Disconnected from Photon
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }

            yield return new WaitWhile(() => PhotonNetwork.IsConnected);
            #endregion
        }
        #endregion

        #region Connect to Photon server
        public static IEnumerator ConnectToMasterServerCoroutine()
        {
            int maxRetries = 5;
            float retryDelay = 3f;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (!PhotonNetwork.IsConnected || ContinuousController.instance.LastConnectServerRegion != ContinuousController.instance.serverRegion)
                {
                    if (PhotonNetwork.IsConnected)
                    {
                        yield return ContinuousController.instance.StartCoroutine(DisconnectCoroutine());

                        yield return new WaitWhile(() => PhotonNetwork.IsConnected);
                    }

                    PhotonNetwork.NetworkingClient.AppId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime;
                    PhotonNetwork.ConnectToRegion(ContinuousController.instance.serverRegion);
                    PhotonNetwork.NickName = ContinuousController.instance.PlayerName;
                    PhotonNetwork.GameVersion = ContinuousController.instance.GameVerString;
                    ContinuousController.instance.LastConnectServerRegion = ContinuousController.instance.serverRegion;
                }

                yield return new WaitUntil(() =>
                    PhotonNetwork.IsConnectedAndReady ||
                    PhotonNetwork.NetworkingClient.State == Photon.Realtime.ClientState.Disconnected);

                if (PhotonNetwork.IsConnectedAndReady)
                {
                    RetryStatus = null;
                    yield break;
                }

                var cause = PhotonNetwork.NetworkingClient.DisconnectedCause;
                Debug.LogWarning($"[Photon] Connection failed: {cause} (attempt {attempt + 1}/{maxRetries + 1})");

                if (cause == Photon.Realtime.DisconnectCause.MaxCcuReached && attempt < maxRetries)
                {
                    RetryStatus = LocalizeUtility.GetLocalizedString(
                        EngMessage: $"Server full. Retrying... ({attempt + 1}/{maxRetries})",
                        JpnMessage: $"サーバーが満員です。再接続中... ({attempt + 1}/{maxRetries})"
                    );
                    Debug.Log($"[Photon] Server full, retrying in {retryDelay}s...");
                    yield return new WaitForSeconds(retryDelay);
                    retryDelay += 2f;
                    continue;
                }

                RetryStatus = LocalizeUtility.GetLocalizedString(
                    EngMessage: "Connection failed. Please try again later.",
                    JpnMessage: "接続に失敗しました。後でもう一度お試しください。"
                );
                Debug.LogError($"[Photon] Connection failed permanently: {cause}");
                yield break;
            }
        }
        #endregion
        #region Connect to Photon Server and Lobby
        public static IEnumerator ConnectToLobbyCoroutine()
        {
            #region Connect to Photon server
            yield return ContinuousController.instance.StartCoroutine(ConnectToMasterServerCoroutine());
            #endregion

            #region Save player name to custom properties
            yield return ContinuousController.instance.StartCoroutine(SetPlayerName());
            #endregion

            #region Save the number of wins to a custom property
            Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

            object value;

            hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (hash.TryGetValue(ContinuousController.WinCountKey, out value))
            {
                hash[ContinuousController.WinCountKey] = ContinuousController.instance.WinCount;
            }

            else
            {
                hash.Add(ContinuousController.WinCountKey, ContinuousController.instance.WinCount);
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            while (true)
            {
                Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

                if (_hash.TryGetValue(ContinuousController.WinCountKey, out value))
                {
                    if ((int)value == ContinuousController.instance.WinCount)
                    {
                        break;
                    }

                }

                yield return null;
            }
            #endregion

            #region Connect to Lobby
            if (!PhotonNetwork.InLobby)
            {
                PhotonNetwork.JoinLobby();
            }

            yield return new WaitWhile(() => !PhotonNetwork.InLobby);

            yield return new WaitUntil(() => PhotonNetwork.InLobby && PhotonNetwork.IsConnectedAndReady);
            #endregion
        }
        #endregion

        #region Save player name to properties
        public static IEnumerator SetPlayerName()
        {
            Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

            object value;

            hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (hash.TryGetValue(ContinuousController.PlayerNameKey, out value))
            {
                hash[ContinuousController.PlayerNameKey] = ContinuousController.instance.PlayerName;
            }

            else
            {
                hash.Add(ContinuousController.PlayerNameKey, ContinuousController.instance.PlayerName);
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            while (true)
            {
                Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

                if (_hash.TryGetValue(ContinuousController.PlayerNameKey, out value))
                {
                    if ((string)value == ContinuousController.instance.PlayerName)
                    {
                        break;
                    }
                }

                yield return null;
            }
        }
        #endregion

        #region Save deck data to custom properties
        public static IEnumerator SignUpBattleDeckData()
        {
            Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out object value))
            {
                hash[ContinuousController.DeckDataPropertyKey] = ContinuousController.instance.BattleDeckData.GetThisDeckCode();
            }

            else
            {
                hash.Add(ContinuousController.DeckDataPropertyKey, ContinuousController.instance.BattleDeckData.GetThisDeckCode());
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            while (true)
            {
                Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

                if (_hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out value))
                {
                    if ((string)value == ContinuousController.instance.BattleDeckData.GetThisDeckCode())
                    {
                        break;
                    }
                }

                yield return null;
            }
        }
        #endregion

        #region Remove custom properties from deck data
        public static IEnumerator DeleteBattleDeckData()
        {
            Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out object value))
            {
                hash.Remove(ContinuousController.DeckDataPropertyKey);
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            while (true)
            {
                Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

                if (!_hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out value))
                {
                    break;
                }

                yield return null;
            }
        }
        #endregion
    }
    #endregion

}
