// server.js
const express = require('express');
const mysql = require('mysql2');
const cors = require('cors');
const bodyParser = require('body-parser');

const app = express();
app.use(cors());

app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));

// ✅ MySQL 연결
const db = mysql.createConnection({
    host: 'localhost',   // DB 주소
    user: 'root',        // MySQL 계정
    password: '112233',        // 비밀번호 (잠깐 수정함)
    database: 'devilrundb'
});

db.connect(err => {
    if (err) console.log('❌ DB 연결 실패:', err);
    else console.log('✅ MySQL 연결 성공');
});

// ✅ 회원가입
app.post('/register', (req, res) => {
    const { username, password } = req.body;

    const successMsg = '회원가입 성공';
    const failMsg = '이미 존재하거나 오류';

    db.query(
        'INSERT INTO users (username, password) VALUES (?, ?)',
        [username, password],
        (err, result) => {
            if (err) {
                // ❌ 실패 로그
                console.log(`❌ 회원가입 실패: ${username} (${failMsg})`);
                return res.json({
                    success: false,
                    message: failMsg
                });
            }

            // ✅ 성공 로그 (괄호 포함)
            console.log(`✅ 회원가입 성공: ${username} (${successMsg})`);

            res.json({
                success: true,
                message: successMsg
            });
        }
    );
});

// ✅ 로그인
app.post('/login', (req, res) => {
    const { username, password } = req.body;

    db.query(
        'SELECT * FROM users WHERE username=? AND password=?',
        [username, password],
        (err, results) => {
            if (err) {
                console.log('❌ 로그인 오류:', err);
                return res.json({ success: false, message: '서버 오류' });
            }

            if (results.length > 0) {
                console.log(`✅ 로그인 성공: ${username}`);
                res.json({
                    success: true,
                    message: '로그인 성공'
                });
            } else {
                console.log(`❌ 로그인 실패: ${username} (아이디나 비밀번호 틀림) `);
                res.json({
                    success: false,
                    message: '아이디나 비밀번호가 틀림'
                });
            }
        }
    );
});

// [추가됨 3] 로비 및 방 관리 로직
let rooms = {}; 

// 1. 방 목록 가져오기 (GET)
app.get('/room_list', (req, res) => {
    let roomData = [];
    for(let key in rooms) {
        roomData.push({ name: key, count: rooms[key].players.length });
    }
    res.json(roomData);
});

// 2. 방 만들기 (POST)
app.post('/create_room', (req, res) => {
    const { roomName, nickName } = req.body;

    // 메모리에 이미 존재하는지 먼저 체크
    if (rooms[roomName]) {
        return res.json({ success: false, message: "이미 있는 방입니다." });
    }

    // 1) DB에 방 추가
    const insertRoomQuery = "INSERT INTO rooms (roomName) VALUES (?)";
    db.query(insertRoomQuery, [roomName], (err) => {
        if (err) {
            console.log(err);
            return res.json({ success: false, message: "방 생성 실패(DB)" });
        }

        // 2) DB에 방장 추가
        const insertPlayerQuery = `
            INSERT INTO room_players (roomName, nickName, isReady, isHost)
            VALUES (?, ?, ?, ?)
        `;
        db.query(insertPlayerQuery, [roomName, nickName, true, true], (err2) => {
            if (err2) {
                console.log(err2);
                return res.json({ success: false, message: "플레이어 저장 실패" });
            }

            // 3) Node.js 메모리에 저장 (기존 로직)
            rooms[roomName] = {
                state: 'waiting',
                players: [{ nick: nickName, isReady: true, isHost: true }]
            };

            res.json({ success: true, message: "방 생성 완료 (DB + 메모리)" });
        });
    });
});

// 3. 방 들어가기 (POST)
app.post('/join_room', (req, res) => {
    const { roomName, nickName } = req.body;

    if (!rooms[roomName]) {
        return res.json({ success: false, message: "없는 방입니다." });
    }

    if (rooms[roomName].players.length >= 3) {
        return res.json({ success: false, message: "방이 꽉 찼습니다." });
    }

    if (rooms[roomName].state !== 'waiting') {
        return res.json({ success: false, message: "게임이 시작된 방입니다." });
    }

    // DB에도 저장
    const sql = `
        INSERT INTO room_players (roomName, nickName, isReady, isHost)
        VALUES (?, ?, ?, ?)
    `;
    db.query(sql, [roomName, nickName, false, false], (err) => {
        if (err) {
            console.log(err);
            return res.json({ success: false, message: "DB 저장 실패" });
        }

        // 메모리에도 저장
        rooms[roomName].players.push({
            nick: nickName,
            isReady: false,
            isHost: false
        });

        res.json({ success: true, message: "입장 성공" });
    });
});

// ✅ 서버 실행
const PORT = 3000;
app.listen(PORT, '0.0.0.0', () => {
    console.log(`🌐 HTTP 서버 실행 중: http://0.0.0.0:${PORT}`);
});
// --- [생존신고(Heartbeat) 시스템] ---

// 유저들의 마지막 생존 시간을 기록하는 장부
let lastHeartbeat = {}; 

// 4. 생존신고 받기 (Unity가 1초마다 보냄)
app.post('/ping', (req, res) => {
    const { nickName } = req.body;
    lastHeartbeat[nickName] = Date.now(); // 현재 시간 기록
    res.end();
});

// 5. 청소부 (2초마다 돌면서 죽은 유저 내쫓기)
setInterval(() => {
    const now = Date.now();
    
    for (let roomName in rooms) {

        let room = rooms[roomName];
        let players = room.players;

        for (let i = players.length - 1; i >= 0; i--) {
            let p = players[i];
            let nick = p.nick;

            if (!lastHeartbeat[nick] || (now - lastHeartbeat[nick] > 4000)) {
                console.log(`💀 [삭제] ${nick}`);

                // 🔥 DB에서도 삭제
                const sql = "DELETE FROM room_players WHERE roomName=? AND nickName=?";
                db.query(sql, [roomName, nick], () => {});

                players.splice(i, 1);
                delete lastHeartbeat[nick];
            }
        }

        // 방에 유저가 0명 → 방 삭제 (DB도 삭제)
        if (players.length === 0) {
            console.log(`🗑 [방 폭파] ${roomName}`);

            // 🔥 DB 방 삭제
            db.query("DELETE FROM rooms WHERE roomName=?", [roomName], () => {});

            // 🔥 DB room_players도 삭제
            db.query("DELETE FROM room_players WHERE roomName=?", [roomName], () => {});

            delete rooms[roomName];
        }
    }
}, 2000);

// 6. 플레이어 목록 가져오기 (GET) - 대기실 UI 갱신용
app.get('/room_players', (req, res) => {
    const { roomName } = req.query;

    if (!rooms[roomName]) {
        return res.json({ isStarted: false, players: [] });
    }
    
    // Unity 클라이언트가 원하는 JSON 구조 [{nickName: 'A', isReady: true}, ...]로 변환
    const playersForUnity = rooms[roomName].players.map(p => ({
        nickName: p.nick, 
        isReady: p.isReady
    }));

    const isGameStarted = (rooms[roomName].state === 'playing');

    res.json({
        isStarted: isGameStarted,
        players: playersForUnity
    });
});

// 7. 준비 상태 토글 (POST)
app.post('/toggle_ready', (req, res) => {
    const { roomName, nickName, isReady } = req.body;

    if (!rooms[roomName]) {
        return res.json({ success: false, message: "방이 없습니다." });
    }

    const player = rooms[roomName].players.find(p => p.nick === nickName);

    if (player && !player.isHost) {

        // DB UPDATE
        const sql = `
            UPDATE room_players 
            SET isReady = ? 
            WHERE roomName = ? AND nickName = ?
        `;
        db.query(sql, [isReady, roomName, nickName], (err) => {
            if (err) {
                console.log(err);
                return res.json({ success: false, message: "DB 업데이트 실패" });
            }

            player.isReady = isReady;
            res.json({ success: true, message: "준비 상태 갱신" });
        });

        return;
    }

    res.json({ success: false, message: "플레이어를 찾을 수 없거나 방장입니다." });
});


// 8. 게임 시작 요청 (POST) - 호스트 전용
app.post('/start_game', (req, res) => {
    const { roomName } = req.body;

    const room = rooms[roomName];

    if (!room || room.state !== 'waiting') {
        return res.json({ success: false, message: "방이 없거나 이미 시작됨." });
    }

    const players = room.players;
    const isFull = players.length === 2;
    const allReady = players.every(p => p.isReady);

    if (isFull && allReady) {

        // DB UPDATE
        const sql = "UPDATE rooms SET state = 'playing' WHERE roomName = ?";
        db.query(sql, [roomName], (err) => {
            if (err) console.log(err);
        });

        // 메모리 업데이트
        room.state = 'playing';

        console.log(`🚀 [GAME START] Room: ${roomName}`);
        return res.json({ success: true, message: "게임 시작!" });
    }

    res.json({ success: false, message: "인원 부족 또는 준비 안됨." });
});

const gameStates = {}; // roomName → game state

// 게임 상태 초기화
app.post('/init_game', (req, res) => {
  const { roomName } = req.body;
  
  gameStates[roomName] = {
    currentTurnIndex: 0,
    diceValue: 0,
    playerPositions: {}, // nickName → currentIndex
    bossPosition: 0,
    bossActive: false,
    eliminatedPlayers: [],
    gameOver: false,
    winner: null
  };
  
  res.json({ success: true, gameState: gameStates[roomName] });
});

// 주사위 굴리기 (서버에서 결정!)
app.post('/roll_dice', (req, res) => {
  const { roomName } = req.body;
  
  if (!gameStates[roomName]) {
    return res.json({ success: false, error: "Game not found" });
  }
  
  // 서버에서 주사위 굴림 (1~6)
  const diceValue = Math.floor(Math.random() * 6) + 1;
  gameStates[roomName].diceValue = diceValue;
  
  res.json({ success: true, diceValue: diceValue });
});

// 플레이어 이동
app.post('/move_player', (req, res) => {
  const { roomName, nickName, steps } = req.body;
  const game = gameStates[roomName];
  
  if (!game) {
    return res.json({ success: false, error: "Game not found" });
  }
  
  // 현재 위치 가져오기
  if (!game.playerPositions[nickName]) {
    game.playerPositions[nickName] = 0;
  }
  
  // 새 위치 계산 (61칸)
  game.playerPositions[nickName] = (game.playerPositions[nickName] + steps) % 61;
  
  // 0칸 도달 체크 (승리)
  let winner = null;
  if (game.playerPositions[nickName] === 0) {
    winner = nickName;
    game.gameOver = true;
    game.winner = nickName;
  }
  
  res.json({
    success: true,
    playerPosition: game.playerPositions[nickName],
    winner: winner
  });
});

// 보스 이동
app.post('/move_boss', (req, res) => {
  const { roomName, steps } = req.body;
  const game = gameStates[roomName];
  
  if (!game) {
    return res.json({ success: false, error: "Game not found" });
  }
  
  game.bossPosition = (game.bossPosition + steps) % 61;
  
  // 같은 칸에 있는 플레이어 탈락
  const caught = [];
  for (let nickName in game.playerPositions) {
    if (game.playerPositions[nickName] === game.bossPosition) {
      game.eliminatedPlayers.push(nickName);
      caught.push(nickName);
    }
  }
  
  // 모두 탈락 체크
  const players = Object.keys(game.playerPositions);
  const alive = players.filter(p => !game.eliminatedPlayers.includes(p)).length;
  
  if (alive === 0) {
    game.gameOver = true;
    game.winner = "BOSS";
  }
  
  res.json({
    success: true,
    bossPosition: game.bossPosition,
    caught: caught
  });
});

// 게임 상태 조회
app.get('/game_state', (req, res) => {
  const { roomName } = req.query;
  const game = gameStates[roomName];
  
  if (!game) {
    return res.json({ success: false, error: "Game not found" });
  }
  
  res.json({ success: true, gameState: game });
});

// 턴 진행
app.post('/next_turn', (req, res) => {
  const { roomName } = req.body;
  const game = gameStates[roomName];
  
  if (!game) {
    return res.json({ success: false, error: "Game not found" });
  }
  
  game.currentTurnIndex++;
  // 플레이어 수에 따라 리셋
  const players = Object.keys(game.playerPositions);
  if (game.currentTurnIndex >= players.length) {
    game.currentTurnIndex = 0;
  }
  
  res.json({ success: true, currentTurn: game.currentTurnIndex });
});