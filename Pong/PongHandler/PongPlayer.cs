using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;
using Newtonsoft.Json;

namespace Pong.PongHandler
{
    /// <summary>
    /// Свойства Игрока и методы его сокета
    /// </summary>
    public class PongPlayer
    {
        private PongGame _game;
        private object _syncRoot = new object();
        private AspNetWebSocketContext _context;

        public event Action<PongPlayer, PlayerPositionMessage> PlayerMoved;
        public event Action<PongPlayer> PlayerDisconnected;

        // позиция игрока по вертикали
        public int YPos { get; private set; }

        public void SetGame(PongGame game)
        {
            if (_game != null)
                throw new InvalidOperationException();
            _game = game;
        }

        /// <summary>
        /// Принимает соединения сокета
        /// </summary>
        public async Task Receiver(AspNetWebSocketContext context)
        {
            _context = context;
            var socket = _context.WebSocket as AspNetWebSocket;
            // подготовить буфер для чтения сообщений
            var inputBuffer = new ArraySegment<byte>(new byte[1024]);

            // отправить номер игрока другому игроку
            SendMessage(new PlayerNumberMessage { PlayerNumber = _game.GetPlayerIndex(this) });

            try
            {
                while (true)
                {
                    // чтение из сокета
                    var result = await socket.ReceiveAsync(inputBuffer, CancellationToken.None);
                    if (socket.State != WebSocketState.Open)
                    {
                        if (PlayerDisconnected != null)
                            PlayerDisconnected(this);
                        break;
                    }

                    // конвертация bytes в string
                    var messageString = Encoding.UTF8.GetString(inputBuffer.Array, 0, result.Count);
                    // десериализует толькоPlayerPositionMessage
                    var positionMessage = JsonConvert.DeserializeObject<PlayerPositionMessage>(messageString);
                    
                    //сохранить новую позицию и отправить в игру
                    YPos = positionMessage.YPos;
                    if (PlayerMoved != null)
                        PlayerMoved(this, positionMessage);

                }
            }
            catch (Exception ex)
            {
                if (PlayerDisconnected != null)
                    PlayerDisconnected(this);
            }
        }

        /// <summary>
        /// Отправляет сообщение по сокету игрока
        /// </summary>
        /// <param name="message">
        /// Сообщение
        /// </param>
        public async Task SendMessage(object message)
        {
            // сериализация и отправка
            var messageString = JsonConvert.SerializeObject(message);
            if (_context != null && _context.WebSocket.State == WebSocketState.Open)
            {
                var outputBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
                await _context.WebSocket.SendAsync(outputBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Закрытие сокета игрока
        /// </summary>
        public void Close()
        {
            if (_context != null && _context.WebSocket.State == WebSocketState.Open)
            {
                _context.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing...", CancellationToken.None).Wait();
            }
        }
    }
}