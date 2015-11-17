function Pong(canvasSelector, messageSelector, scoreSelector, applicationUrl) {
    
    this.canvas = $(canvasSelector)[0];
    this.messageSelector = messageSelector;
    this.scoreSelector = scoreSelector;

    this.playerHeight = 50;
    this.playerWidth = 6;
    this.ballRadius = 3;
    this.score = [0, 0];

    this.myPlayer = -1;

    this.players = [
    {
        color: "cyan",
        xPos: 5 + this.playerWidth / 2,
        yPos: this.canvas.height / 2 
    },
    {
        color: "pink",
        xPos: this.canvas.width - 5 - this.playerWidth / 2,
        yPos: this.canvas.height / 2,
    }
    ];

    this.ball = {
        color: "white",
        xPos: this.canvas.width / 2,
        yPos: this.canvas.height / 2
    };

    // реагирование на движения компьютерной мыши
    $(canvasSelector).mousemove($.proxy(function (e) {
        if (this.myPlayer == -1)
            return;

        // пересчет вертикальной позиции относительно поля
        var offset = $(this.canvas).offset(); //возвращает координаты элемента
        var mouseY = e.pageY - offset.top;

        // ограничения движения, чтобы не вышло за границы поля
        if (mouseY < this.playerHeight / 2)
            mouseY = this.playerHeight / 2;
        if (mouseY > this.canvas.height - this.playerHeight / 2)
            mouseY = this.canvas.height - this.playerHeight / 2;

        this.players[this.myPlayer].yPos = mouseY;

        // отправить сообщение серверу с новой позицией
        this.sendMessage({ YPos: mouseY });

        this.draw();
    }, this));

    this.draw();
    this.displayScore();

    // запуск websocket
    if(typeof(MozWebSocket) == "function") {
        this.socket = new MozWebSocket(applicationUrl);
        this.openStateConst = MozWebSocket.OPEN;
    } else {
        this.socket = new WebSocket(applicationUrl);
        this.openStateConst = WebSocket.OPEN;
    }

    // регистрация событий сокета
    this.socket.onopen = $.proxy(function () {
        this.info("Подключение...");
    }, this);
    this.socket.onclose = $.proxy(function () {
        this.info("Другой игрок отключился! Обновите страницу");
    }, this);
    this.socket.onerror = $.proxy(function () {
        this.info("Вы отключены! Обновите страницу");
    }, this);
    this.socket.onmessage = $.proxy(function (msg) {
        this.processMessage(msg);
    }, this);
}
//создание объекта
Pong.prototype = {
    initialize : function() {
        
    },

    // перерисовка игроков и мяча
    draw : function () {
        var ctx = this.canvas.getContext('2d'); //Canvas 2D API

        ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);//очищает пиксели

        for (var i in this.players) {
            ctx.beginPath();
            var player = this.players[i];
            ctx.rect(player.xPos - this.playerWidth / 2, player.yPos - this.playerHeight / 2, this.playerWidth, this.playerHeight);//рисует прямоугольник
            ctx.fillStyle = player.color;
            ctx.fill();
        }

        ctx.beginPath();
        ctx.arc(this.ball.xPos /*- радиус*/, this.ball.yPos /*- радиус*/, this.ballRadius, 0, 2 * Math.PI);//рисует мяч
        ctx.fillStyle = this.ball.color;
        ctx.fill();

    },

    // информационное сообщение для пользователя
    info: function (text) {
        $(this.messageSelector).html(text);
    },

    // показать текущий счет
    displayScore: function () {
        $(this.scoreSelector).html("Счет " + this.score[0] + ":" + this.score[1]);
    },

    // другой игрок
    otherPlayer: function() {
        return this.players[this.myPlayer == 0 ? 1 : 0];
    },

    // сериализовать и отправить сообщение серверу
    sendMessage: function (msg) {
        if (this.socket != "undefined" && this.socket.readyState == this.openStateConst) {
            var msgText = JSON.stringify(msg);
            this.socket.send(msgText);
        }
    },

    // анализ полученного сообщения от сервера
    processMessage: function (msg) {
        var data = JSON.parse(msg.data);

        switch (data.Type) {
            case "PlayerNumberMessage":
                this.myPlayer = data.PlayerNumber;
                if (this.myPlayer == 0) {
                    this.info("<span style='color:cyan'>CYAN</span>");
                } else {
                    this.info("<span style='color:pink'>PINK</span>");
                }
                break;

            case "PlayerPositionMessage":
                this.otherPlayer().yPos = data.YPos;
                this.draw();
                break;

            case "BallPositionMessage":
                this.ball.xPos = data.XPos;
                this.ball.yPos = data.YPos;
                this.draw();
                break;

            case "ScoreMessage":
                this.score = data.Score;
                this.displayScore();
                break;
        }
    }


};

