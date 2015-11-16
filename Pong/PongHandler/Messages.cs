﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Pong.PongHandler
{
    public class BaseMessage
    {
        /// <summary>
        /// Сериализованный тип сообщения для JS
        /// </summary>
        public string Type
        {
            get { return this.GetType().Name; }
        }
    }
    
    public class PlayerNumberMessage : BaseMessage
    {
        public int PlayerNumber { get; set; }
    }

    public class PlayerPositionMessage : BaseMessage
    {
        public int YPos { get; set; }
    }

    public class BallPositionMessage : BaseMessage
    {
        public int XPos { get; set; }
        public int YPos { get; set; }
    }

    public class ScoreMessage : BaseMessage
    {
        public int[] Score { get; set; }
    }
}