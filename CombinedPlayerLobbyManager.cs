using UnityEngine;
using TMPro;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class CombinedPlayerLobbyManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private TextMeshProUGUI playerStatusText;
    [SerializeField] private TextMeshProUGUI lobbyStatusText;
    [SerializeField] private TextMeshProUGUI playerListText;
    [SerializeField] private TextMeshProUGUI playerCountText; // Add a TextMeshProUGUI for player count

    private FirebaseFirestore firestore;
    private DocumentReference playerProfileRef;
    private bool firebaseInitialized = false;

    private void Start()
    {
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                firestore = FirebaseFirestore.DefaultInstance;
                firebaseInitialized = true;
                Debug.Log("Firebase initialized successfully.");
                ConnectToPhoton();
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + task.Result);
            }
        });
    }

    private void ConnectToPhoton()
    {
        Debug.Log("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public void InitializeUpdatePlayerStatus()
    {
        if (!firebaseInitialized)
        {
            Debug.LogError("Firebase is not initialized.");
            return;
        }

        string userID = AR_CloudData.instance.user_ID;
        Debug.Log("Initializing Firebase...");

        playerProfileRef = firestore.Collection(userID).Document(AR_CloudData.instance.Playerprofiel);
        Debug.Log(@$"Setting player status to online for user: {userID}");
        UpdatePlayerStatus(userID, true);

        GetPlayersList(userID);
    }

    public void UpdatePlayerStatus(string userId, bool isOnline)
    {
        if (playerProfileRef == null)
        {
            Debug.LogError("Player profile reference is not initialized.");
            return;
        }

        Debug.Log(@$"Updating player status in Firestore: UserId = {userId}, IsOnline = {isOnline}");

        Dictionary<string, object> playerStatus = new Dictionary<string, object>
        {
            { "online", isOnline }
        };

        playerProfileRef.SetAsync(playerStatus, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log(@$"Player status updated successfully for UserId = {userId}");
            }
            else
            {
                Debug.LogError($"Failed to update player status for UserId = {userId}: {task.Exception}");
            }
        });
    }

    public void GetPlayersList(string userId)
    {
        if (!firebaseInitialized)
        {
            Debug.LogError("Firebase is not initialized.");
            return;
        }

        Debug.Log("Fetching players list from Firestore...");

        firestore.Collection("players").WhereEqualTo("online", true).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                QuerySnapshot snapshot = task.Result;
                string playerStatusTextContent = "Online Players:\n";

                foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
                {
                    string username = documentSnapshot.Id;
                    playerStatusTextContent += $"Username: {username}\n";
                }

                if (playerStatusText != null)
                {
                    playerStatusText.text = playerStatusTextContent;
                }

                Debug.Log("Online players fetched and displayed successfully: " + playerStatusTextContent);
            }
            else
            {
                Debug.LogError($"Failed to fetch players list: {task.Exception}");
            }
        });
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master server.");
        string userId = AR_CloudData.instance.user_ID;
        Debug.Log(@$"Setting player status to online for user: {userId}");
        UpdatePlayerStatus(userId, true);

        GetPlayersList(userId);
        JoinLobby();
    }

    private void JoinLobby()
    {
        Debug.Log("Joining lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined lobby successfully.");
        UpdateLobbyStatus("Joined lobby successfully.");
        UpdatePlayerList();
        UpdatePlayerCount(); // Update player count when joined lobby
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log("Room list updated.");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} entered the room.");
        UpdatePlayerList();
        UpdatePlayerCount(); // Update player count when a player enters the room
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left the room.");
        UpdatePlayerList();
        UpdatePlayerCount(); // Update player count when a player leaves the room
    }

    private void UpdateLobbyStatus(string status)
    {
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = status;
        }
    }

    private void UpdatePlayerList()
    {
        if (playerListText != null)
        {
            string playerListContent = "Players in Lobby:\n";
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                string nickname = string.IsNullOrEmpty(player.NickName) ? "Unnamed" : player.NickName;
                string userId = string.IsNullOrEmpty(player.UserId) ? "No ID" : player.UserId;
                playerListContent += $"Nickname: {nickname} (ID: {userId})\n";
            }
            playerListText.text = playerListContent;
        }
    }

    private void UpdatePlayerCount()
    {
        if (playerCountText != null)
        {
            int playerCount = PhotonNetwork.PlayerList.Length;
            playerCountText.text = $"Players in Lobby: {playerCount}";
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Disconnected from Photon server: {cause}");

        string userId = AR_CloudData.instance.user_ID;
        Debug.Log(@$"Setting player status to offline for user: {userId}");
        UpdatePlayerStatus(userId, false);

        GetPlayersList(userId);
        UpdateLobbyStatus($"Disconnected from Photon server: {cause}");
    }
}
