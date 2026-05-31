using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCGO.Networking
{
    public enum GameState
    {
        Menu,
        Matchmaking,
        CustomRoom,
        BotMatch
    }

    public struct GameNetworkEvents
    {
        public Action<Player, IPlayerSelection> OnPlayerSelecton;
        public Action<Player, MainPhaseAction> OnMainPhaseAction;
        public Action<Player> OnSurrender;
    }

    public struct LobbyNetworkEvents
    {
        
    }

    public struct MatchmakingEvents
    {
        public Action<bool> OnConnected;
        public Action OnCanceled;
        public Action OnMatchFound;
        public Action OnGameStart;
    }

    public interface INetworkProvider
    {
        public GameNetworkEvents GameEvents { get; }
        public LobbyNetworkEvents LobbyEvents { get; }

        public GameState GameState { get; }

        public abstract void Initialise();

        public abstract void StartMatchmaking(MatchmakingEvents MatchmakingEvents);

        public abstract GameNetworkEvents InitGame(Player[] players);

        abstract void SendMainPhaseAction(Player player, MainPhaseAction action);
        abstract void SendPlayerSelection(Player player, IPlayerSelection playerSelection);

        abstract void SendSurrender();
    }
}