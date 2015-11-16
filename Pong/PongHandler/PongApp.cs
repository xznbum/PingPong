using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Pong.PongHandler
{
    /// <summary>
    /// Содержит все игры
    /// </summary>
    public static class PongApp
    {
        private static object _syncRoot = new object();

        private static List<PongGame> _games;

        static PongApp()
        {
            _games = new List<PongGame>();
        }
        
        /// <summary>
        /// Добавить игрока в существующую игру или создать новую игру 
        /// </summary>
        /// <param name="player">
        /// Игро
        /// </param>
        public static void JoinPlayer(PongPlayer player)
        {
            lock (_syncRoot)
            {
                var game = _games.Where(g => g.State == GameState.WaitingForPlayer).FirstOrDefault();
                if (game == null)
                {
                    game = new PongGame();
                    _games.Add(game);
                    game.GameOver += OnGameOver;
                }
                game.JoinPlayer(player);
            }
        }

        private static void OnGameOver(PongGame game)
        {
            lock (_syncRoot)
            {
                _games.Remove(game);
            }
        }
    }
}