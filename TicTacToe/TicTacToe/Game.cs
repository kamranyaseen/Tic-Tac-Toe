using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;

namespace TicTacToe
{

    public class Client
    {
        public string Name { get; set; }
        public Client Opponent { get; set; }

        public bool IsPlaying { get; set; }
        public bool WaitingForMove { get; set; }
        public bool LookingForOpponent { get; set; }

        public string connectionId { get; set; }
    }

    public class GameInformation
    {
        public string OpponentName { get; set; }
        public string Winner { get; set; }
        public int MarkerPosition { get; set; }
    }
    public class Game : Hub
    {
        private static int _gamesPlayed = 0;
        public static List<Client> _client = new List<Client>();
        public static List<TicTacToe> _games = new List<TicTacToe>();
        public object _syncRoot = new object();
        public void RegisterClient(string data)
        {
            lock (_syncRoot)
            {
                var client = _client.FirstOrDefault(x=> x.connectionId == Context.ConnectionId);
                if (client == null)
                {
                    client = new Client { connectionId = Context.ConnectionId,Name = data};
                    _client.Add(client);
                }
                client.IsPlaying = false;
            }
            Clients.Client(Context.ConnectionId).registerComplete();
        }

        private Random random = new Random();
        public void FindOpponent()
        {
            var player = _client.FirstOrDefault(x => x.connectionId == Context.ConnectionId);
            if (player == null) return;

            player.LookingForOpponent = true;

            var opponent = _client.Where(x => x.connectionId != Context.ConnectionId && x.LookingForOpponent && !x.IsPlaying).OrderBy(x => Guid.NewGuid()).FirstOrDefault();
            if (opponent == null)
            {
                Clients.Client(Context.ConnectionId).noOpponents();
                return;
            }
            player.IsPlaying = true;
            player.LookingForOpponent = false;
            opponent.IsPlaying = true;
            opponent.LookingForOpponent = false;

            player.Opponent = opponent;
            opponent.Opponent = player;

            Clients.Client(Context.ConnectionId).foundOpponent(opponent.Name);
            Clients.Client(opponent.connectionId).foundOpponent(player.Name);

            if (random.Next(0, 5000) % 2 == 0)
            {
                player.WaitingForMove = false;
                opponent.WaitingForMove = true;

                Clients.Client(player.connectionId).waitingForMarkerPlacement(opponent.Name);
                Clients.Client(opponent.connectionId).waitingForOpponent(opponent.Name);
            }
            else
            {
                player.WaitingForMove = true;
                opponent.WaitingForMove = false;
                Clients.Client(opponent.connectionId).waitingForMarkerPlacement(opponent.Name);
                Clients.Client(player.connectionId).waitingForOpponent(opponent.Name);
            }

            lock (_syncRoot)
            {
                _games.Add(new TicTacToe { Player1 = player, Player2 = opponent });
            }
        }

        public void Play(int position)
        {
            var game = _games.FirstOrDefault(x=>x.Player1.connectionId == Context.ConnectionId || x.Player2.connectionId  == Context.ConnectionId);
            if (game == null || game.IsGameOver) return;
            int marker = 0;

            if (game.Player2.connectionId == Context.ConnectionId)
            {
                marker = 1;
            }
            var player = marker == 0 ? game.Player1 : game.Player2;

            if (player.WaitingForMove) return;

            Clients.Client(game.Player1.connectionId).addMarkerPlacement(new GameInformation { OpponentName = player.Name, MarkerPosition = position });
            Clients.Client(game.Player2.connectionId).addMarkerPlacement(new GameInformation { OpponentName = player.Name, MarkerPosition = position });

            // Place the marker and look for a winner
            if (game.Play(marker, position))
            {
                _games.Remove(game);
                Clients.Client(game.Player1.connectionId).gameOver(player.Name);
                Clients.Client(game.Player2.connectionId).gameOver(player.Name);
            }

            // If it's a draw notify the players that the game is over
            if (game.IsGameOver && game.IsDraw)
            {
                _games.Remove(game);
                Clients.Client(game.Player1.connectionId).gameOver("It's a draw!");
                Clients.Client(game.Player2.connectionId).gameOver("It's a draw!");
            }

            if (!game.IsGameOver)
            {
                player.WaitingForMove = !player.WaitingForMove;
                player.Opponent.WaitingForMove = !player.Opponent.WaitingForMove;

                Clients.Client(player.Opponent.connectionId).waitingForMarkerPlacement(player.Name);
            }
        }

        //public override Task OnDisconnected()
        //{
        //    var game = _games.FirstOrDefault(x => x.Player1.connectionId == Context.ConnectionId || x.Player2.connectionId == Context.ConnectionId);
        //    if (game == null)
        //    {
        //        // Client without game?
        //        var clientWithoutGame = _client.FirstOrDefault(x => x.connectionId == Context.ConnectionId);
        //        if (clientWithoutGame != null)
        //        {
        //            _client.Remove(clientWithoutGame);

        //            SendStatsUpdate();
        //        }
        //        return null;
        //    }

        //    if (game != null)
        //    {
        //        _games.Remove(game);
        //    }

        //    var client = game.Player1.connectionId == Context.ConnectionId ? game.Player1 : game.Player2;

        //    if (client == null) return null;

        //    _client.Remove(client);
        //    if (client.Opponent != null)
        //    {
        //        SendStatsUpdate();
        //        return Clients.Client(client.Opponent.connectionId).opponentDisconnected(client.Name);
        //    }
        //    return null;
        //}

        public Task SendStatsUpdate()
        {
            return Clients.All.refreshAmountOfPlayers(new { totalGamesPlayed = _gamesPlayed, amountOfGames = _games.Count, amountOfClients = _client.Count });
        }
    }
}