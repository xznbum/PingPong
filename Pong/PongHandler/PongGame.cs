using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Pong.PongHandler
{
    /// <summary>
    /// Состояния игры
    /// </summary>
    public enum GameState
    {
        NotInitiated,
        WaitingForPlayer,
        InProgress
    }
    
    /// <summary>
    /// Игра понг
    /// </summary>
    public class PongGame
    {
        public const int LeftPlayer = 0;
        public const int RightPlayer = 1;
        
        public const int FieldWidth = 400;
        public const int FieldHeight = 300;
        public const int PlayerToEdgeDistance = 5;
        public const int PlayerWidth = 6;
        public const int PlayerHeight = 50;
        public const int PlayerReach = PlayerToEdgeDistance + PlayerWidth;
        public const int BallRadius = 3;
        public const double BallStartingSpeedPixPerSecond = 40;
        public const double BallSpeedIncrease = 15;
        
        private object _syncRoot = new object();
        private PongPlayer[] _players = new PongPlayer[2];
        private Vector _ballPosition = new Vector(FieldWidth / 2, FieldHeight / 2);
        private Vector _ballDirection = Vector.SW;
        private double _ballSpeed = BallStartingSpeedPixPerSecond;
        private int[] _score = new int[2];

        /// <summary>
        /// Служит для отмены движения мяча
        /// </summary>
        private CancellationTokenSource _ballCancellationTokenSource;
        
        public GameState State { get; private set; }

        /// <summary>
        /// Игра закончена
        /// </summary>
        public event Action<PongGame> GameOver;


        public PongGame()
        {
            State = GameState.NotInitiated;
        }

        public int GetPlayerIndex(PongPlayer player)
        {
            return Array.IndexOf(_players, player);
        }

        public PongPlayer OtherPlayer(PongPlayer thisPlayer)
        {
            var index = GetPlayerIndex(thisPlayer);
            return _players[index == LeftPlayer ? RightPlayer : LeftPlayer];
        }

        /// <summary>
        /// Добавляет второго игрока в игру
        /// </summary>
        /// <param name="player">
        /// Игрок
        /// </param>
        public void JoinPlayer(PongPlayer player)
        {
            lock (_syncRoot)
            {
                if (_players[LeftPlayer] != null && _players[RightPlayer] != null)
                    throw new InvalidOperationException();

                player.SetGame(this);
                player.PlayerDisconnected += OnPlayerDisconnected;
                player.PlayerMoved += OnPlayerMoved;

                if (_players[LeftPlayer] == null)
                {
                    _players[LeftPlayer] = player;
                    State = GameState.WaitingForPlayer;
                }
                else
                {
                    _players[RightPlayer] = player;
                    // начало игры
                    State = GameState.InProgress; 
                    StartBall();
                }
            }
        }

        /// <summary>
        /// Старт мяча
        /// </summary>
        private void StartBall()
        {
            //отвечает за движение мяча, отбивание от стенок и подсчет очков
            Action<CancellationToken> ballMover = (cancellationToken) =>
                {
                    var lastTime = DateTime.Now;

                    while(true)
                    {
                        var thisTime = DateTime.Now;
                        // сколько секунд прошло
                        var secondsElapsed = (thisTime - lastTime).TotalMilliseconds / 1000.0; 

                        MoveBall(secondsElapsed);
                        lastTime = thisTime;

                        Thread.Sleep(10);

                        // завершить задачу, если пришла отмена
                        if (cancellationToken.IsCancellationRequested)
                            break;
                    }
                };

            // подготовить отмену и запуск новой задачи
            _ballCancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => ballMover(_ballCancellationTokenSource.Token), _ballCancellationTokenSource.Token, 
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Движение мяча
        /// </summary>
        /// <param name="secondsElapsed">
        /// Сколько секунд прошло
        /// </param>
        private void MoveBall(double secondsElapsed)
        {
            lock (_syncRoot)
            {
                // вычисление новой позиции
                _ballPosition += _ballDirection * (_ballSpeed * secondsElapsed);

                // проверка столкновений со стенками
                // верхняя граница
                if(_ballPosition.Y < BallRadius)
                {
                    _ballPosition += new Vector(0, -(_ballPosition.Y - BallRadius));
                    _ballDirection = _ballDirection.MirrorY();
                }
                // нижняя граница
                if (_ballPosition.Y > FieldHeight - BallRadius) 
                {
                    _ballPosition += new Vector(0, -(_ballPosition.Y - (FieldHeight - BallRadius)));
                    _ballDirection = _ballDirection.MirrorY();
                }
                // левый игрок
                if (_ballPosition.X < PlayerReach + BallRadius &&
                    _ballPosition.Y <= _players[LeftPlayer].YPos + (PlayerHeight / 2) && _ballPosition.Y >= _players[LeftPlayer].YPos - (PlayerHeight / 2))
                {
                    _ballPosition += new Vector(-(_ballPosition.X - (BallRadius + PlayerReach)), 0);
                    _ballDirection = _ballDirection.MirrorX();
                    // speed things up to make them more interesing
                    _ballSpeed += BallSpeedIncrease;
                }
                // правый игрок
                if (_ballPosition.X > FieldWidth - (BallRadius + PlayerReach) &&
                    _ballPosition.Y <= _players[RightPlayer].YPos + (PlayerHeight / 2) && _ballPosition.Y >= _players[RightPlayer].YPos - (PlayerHeight / 2))
                {
                    _ballPosition += new Vector(-(_ballPosition.X - (FieldWidth - (BallRadius + PlayerReach))), 0);
                    _ballDirection = _ballDirection.MirrorX();
                    // ускорение мяча
                    _ballSpeed += BallSpeedIncrease;
                }

                // проверка счета
                if (_ballPosition.X < 0 || _ballPosition.X > FieldWidth)
                {
                    _score[_ballPosition.X < 0 ? RightPlayer : LeftPlayer]++;
                    // отправить игрокам сообщение с текущим счетом
                    BroadcastMessage(new ScoreMessage { Score = _score });

                    //'перезагрузить мяч'
                    var random = new Random();
                    _ballPosition = new Vector(FieldWidth / 2, BallRadius + random.Next(FieldHeight - 2 * BallRadius));
                    _ballDirection = Vector.Directions[random.Next(Vector.Directions.Length - 1)];
                    _ballSpeed = BallStartingSpeedPixPerSecond;
                }

                // отправить игрокам сообщение с новой позицией мяча
                BroadcastMessage(new BallPositionMessage { XPos = (int)_ballPosition.X, YPos = (int)_ballPosition.Y });
            }
        }

        /// <summary>
        /// Отправляет сообщение двум игрокам
        /// </summary>
        /// <param name="message">
        /// Сообщение
        /// </param>
        private void BroadcastMessage(object message)
        {
            foreach (var player in _players)
            {
                player.SendMessage(message);
            }
        }

        /// <summary>
        /// Реагирует на движение игрока
        /// </summary>
        /// <param name="player">
        /// Игрок
        /// </param>
        /// <param name="position">
        /// Сообщение с новой позицией игрока
        /// </param>
        private void OnPlayerMoved(PongPlayer player, PlayerPositionMessage position)
        {
            var otherPlayer = OtherPlayer(player);

            if (otherPlayer != null)
            {
                // отправить игроку поицию другого игрока
                otherPlayer.SendMessage(new PlayerPositionMessage { YPos = player.YPos });
            }
        }

        /// <summary>
        /// С игроком прервано соединение
        /// </summary>
        /// <param name="player">
        /// Игрок
        /// </param>
        private void OnPlayerDisconnected(PongPlayer player)
        {
            lock (_syncRoot)
            {
                // остановить мяч
                _ballCancellationTokenSource.Cancel();
                var otherPlayer = OtherPlayer(player);

                if (otherPlayer != null)
                {
                    // закрыть соединение с другим игроком, что означает окончание игры
                    otherPlayer.Close();
                }
                if (GameOver != null)
                    GameOver(this);
            }
        }
    }
}