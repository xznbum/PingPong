using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Pong.PongHandler
{
    /// <summary>
    /// http обработчик запросов сокета
    /// </summary>
    public class PongHttpHandler : IHttpHandler
    {
        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (context.IsWebSocketRequest)
            {
                // создать нового игрока
                var player = new PongPlayer();
                PongApp.JoinPlayer(player);

                // начать получать сообщения от сокета
                context.AcceptWebSocketRequest(player.Receiver);
            }
        }
    }
}